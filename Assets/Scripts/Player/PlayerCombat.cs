using System.Collections;
using Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    public class PlayerCombat : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private Transform muzzle;
        [SerializeField] private Camera aimCamera;

        [Header("Melee")]
        [SerializeField] private float meleeCooldown = 0.6f;
        [SerializeField] private float meleeDamage = 20f;
        [SerializeField] private float meleeRange = 1.4f;
        [SerializeField] private float meleeRadius = 0.9f;
        [SerializeField] private float meleeHitDelay = 0.25f;

        [Header("Shooting")]
        [Tooltip("The Attack action is already bound to left click / gamepad west in " +
                 "InputSystem_Actions; reused here for firing rather than adding a new action. " +
                 "Held (not just clicked) to support continuous fire.")]
        [SerializeField] private float fireDamage = 15f;
        [Tooltip("How far (0-1) into whichever Arms-layer Shoot state is currently playing the " +
                 "hit/muzzle-flash lands. Driven directly off the Animator's own " +
                 "AnimatorStateInfo.normalizedTime for the Arms layer every frame (see Update/" +
                 "CheckShootBeat) rather than an estimated wall-clock timer, so it's locked " +
                 "exactly to that state's real clip length/playback speed with zero drift over a " +
                 "sustained hold. Used for Arms_Idle_Shoot, Arms_Shoot_Run and Arms_Jump_Shoot - " +
                 "see shootBeatFractionWalk for Arms_Shoot_Walk specifically.")]
        [SerializeField, Range(0f, 1f)] private float shootBeatFraction = 0.5f;
        [Tooltip("Same as shootBeatFraction, but only for Arms_Shoot_Walk. Kept separate because " +
                 "Arms_Shoot_Walk plays the same Run_Gun_Shoot clip slowed down (see " +
                 "PlayerSceneSetup.WalkShootAnimSpeed), which stretches out its actual recoil arc " +
                 "into only the first portion of the loop with a long settle tail after - firing " +
                 "at the same fraction as the full-speed states reads as \"after the animation " +
                 "already finished\" there. Tune independently to land on the visible recoil.")]
        [SerializeField, Range(0f, 1f)] private float shootBeatFractionWalk = 0.2f;
        [SerializeField] private float maxAimDistance = 100f;
        [SerializeField] private LayerMask aimMask = ~0;
        [Tooltip("How long the Arms layer/loop stays on after the Attack input is released " +
                 "before actually winding down. Without this, spam-clicking Fire re-triggers " +
                 "FireStart on every single click (each click is its own started/canceled pair), " +
                 "restarting the loop from frame 0 every time - the same flicker sustained fire " +
                 "was fixed for. As long as the next click lands inside this window, the loop " +
                 "just keeps playing through the gap instead of resetting.")]
        [SerializeField] private float armsStopGrace = 0.3f;

        [Header("Muzzle Flash")]
        [SerializeField] private float muzzleFlashDuration = 0.05f;
        [SerializeField] private float muzzleFlashIntensity = 10f;
        [SerializeField] private float muzzleFlashRange = 1.5f;
        [SerializeField] private Color muzzleFlashColor = new Color(0.15f, 0.45f, 1f);

        [Header("Tracer")]
        [Tooltip("Purely cosmetic - the hit/miss result is already decided by the instant " +
                 "raycast in FireHitscan before this ever draws.")]
        [SerializeField] private float tracerDuration = 0.05f;
        [SerializeField] private float tracerWidth = 0.03f;
        [SerializeField] private Color tracerColor = new Color(0.4f, 0.85f, 1f);

        private static readonly int MeleeParam = Animator.StringToHash("Melee");
        private static readonly int FireStartParam = Animator.StringToHash("FireStart");
        private static readonly int FiringParam = Animator.StringToHash("Firing");
        private static readonly int ArmsIdleHash = Animator.StringToHash("Arms_Idle");
        private static readonly int ArmsShootWalkHash = Animator.StringToHash("Arms_Shoot_Walk");

        private InputSystem_Actions _actions;
        private float _lastMeleeTime = -999f;
        private float _attackingUntil = -999f;
        private bool _isFiring;
        private int _armsLayerIndex = -1;
        private bool _armsActive;
        private float _stopArmsAt = float.PositiveInfinity;
        private float _lastArmsNormalizedTime;
        private bool _hasLastArmsNormalizedTime;
        private int _lastArmsStateHash;

        // _isFiring covers the whole held-fire duration (not just the instant of a shot beat, now
        // that shots are event-driven rather than an evenly spaced timer - see CheckShootBeat) so
        // the emote wheel still can't be opened mid-burst between individual shot beats.
        public bool IsAttacking => _isFiring || Time.time < _attackingUntil;

        private void Awake()
        {
            _actions = new InputSystem_Actions();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (aimCamera == null) aimCamera = Camera.main;
            if (animator != null) _armsLayerIndex = animator.GetLayerIndex("Arms");
        }

        private void OnEnable()
        {
            _actions.Player.Enable();
            _actions.Player.Melee.performed += OnMeleePerformed;
            _actions.Player.Attack.started += OnFireStarted;
            _actions.Player.Attack.canceled += OnFireCanceled;
        }

        private void OnDisable()
        {
            _actions.Player.Melee.performed -= OnMeleePerformed;
            _actions.Player.Attack.started -= OnFireStarted;
            _actions.Player.Attack.canceled -= OnFireCanceled;
            _actions.Player.Disable();

            _isFiring = false;
            // Otherwise a frozen Arms-layer shoot pose keeps overriding the base layer's arms
            // (e.g. Death's full-body pose) after PlayerDeathHandler disables this component.
            StopArmsImmediately();
        }

        private void Update()
        {
            if (_armsActive && Time.time >= _stopArmsAt)
            {
                StopArmsImmediately();
            }

            if (_isFiring) CheckShootBeat();
        }

        // Fires exactly once per loop of whichever Arms-layer Shoot state is currently playing,
        // at shootBeatFraction into that loop - detected by watching AnimatorStateInfo.
        // normalizedTime for the Arms layer cross that fraction each frame, rather than running
        // an independent wall-clock timer. A timer re-armed every shot as "now + cooldown" drifts
        // over a sustained hold (each frame's unavoidable rounding between "cooldown elapsed" and
        // Update() actually noticing compounds shot over shot, since the next target is rebased
        // off the late "now" instead of a fixed schedule) - reading the Animator's own playback
        // position instead has no such drift, since Mecanim advances normalizedTime by real
        // elapsed time every frame with nothing to compound.
        private void CheckShootBeat()
        {
            if (_armsLayerIndex < 0 || animator == null) return;

            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(_armsLayerIndex);
            int stateHash = state.shortNameHash;

            // Arms_Idle (the non-shooting default) also loops (Idle_Gun) - excluded so the beat
            // can't fire off it, e.g. for the single frame right after OnFireStarted before the
            // FireStart-triggered transition has actually taken effect on the Animator side.
            if (stateHash == ArmsIdleHash)
            {
                _hasLastArmsNormalizedTime = false;
                _lastArmsStateHash = stateHash;
                return;
            }

            // normalizedTime keeps counting up across loops (1.2, 2.4, ...) rather than wrapping
            // on its own, so take the fractional part to get position-within-the-current-loop.
            float normalizedTime = state.normalizedTime - Mathf.Floor(state.normalizedTime);

            // Also reset on any state change (e.g. the live Idle_Shoot<->Shoot_Walk/Shoot_Run
            // switch when Speed crosses a threshold mid-burst), not just coming from Arms_Idle -
            // each state's own timeline starts fresh on entry, so comparing its normalizedTime
            // against the *previous* state's leftover value could read as a spurious wrap/wrong-
            // direction crossing (or silently swallow the real one) right at the switch.
            if (!_hasLastArmsNormalizedTime || stateHash != _lastArmsStateHash)
            {
                _lastArmsNormalizedTime = normalizedTime;
                _hasLastArmsNormalizedTime = true;
                _lastArmsStateHash = stateHash;
                return;
            }

            // Arms_Shoot_Walk plays the same clip as Arms_Shoot_Run, just slowed (see
            // PlayerSceneSetup.WalkShootAnimSpeed) - its recoil arc ends up compressed into an
            // earlier fraction of the loop with a long settle tail after, so it gets its own beat
            // fraction rather than sharing shootBeatFraction with the full-speed states.
            float targetFraction = stateHash == ArmsShootWalkHash ? shootBeatFractionWalk : shootBeatFraction;

            bool crossed = _lastArmsNormalizedTime <= normalizedTime
                ? _lastArmsNormalizedTime < targetFraction && normalizedTime >= targetFraction
                : _lastArmsNormalizedTime < targetFraction || normalizedTime >= targetFraction; // wrapped this frame

            _lastArmsNormalizedTime = normalizedTime;
            _lastArmsStateHash = stateHash;

            if (!crossed) return;

            FireHitscan();
            SpawnMuzzleFlash();
        }

        private void OnMeleePerformed(InputAction.CallbackContext context)
        {
            if (Time.time - _lastMeleeTime < meleeCooldown) return;
            _lastMeleeTime = Time.time;
            _attackingUntil = Time.time + meleeCooldown;

            if (animator != null)
            {
                animator.SetTrigger(MeleeParam);
            }

            StartCoroutine(MeleeDamageWindow());
        }

        private IEnumerator MeleeDamageWindow()
        {
            yield return new WaitForSeconds(meleeHitDelay);

            Vector3 origin = transform.position + Vector3.up + transform.forward * meleeRange;
            var hits = Physics.OverlapSphere(origin, meleeRadius, ~0, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (hit.transform.root == transform.root) continue;

                var damageable = hit.GetComponentInParent<IDamageable>();
                if (damageable == null || damageable.IsDead) continue;

                damageable.ApplyDamage(meleeDamage, hit.ClosestPoint(origin), gameObject);
            }
        }

        // Which shoot clip plays (Idle_Shoot/Run_Gun_Shoot/Jump_Shoot) is decided entirely by the
        // Animator off the existing Grounded/Speed parameters. FireStart is a one-shot trigger
        // fired once per firing *bout* - not once per click/shot (see _armsActive/armsStopGrace
        // below) - the Shoot clips loop on their own for the rest of the hold, and CheckShootBeat
        // fires once per loop off the Animator's own playback position, so sustained fire reads
        // as one smooth continuous cycle instead of restarting/re-blending on every damage tick
        // or every individual click of a spam-clicked burst.
        // The Arms layer (upper-body-masked, see PlayerSceneSetup.BuildArmsLayer) sits at weight
        // 0 the rest of the time so it doesn't fight Melee/Emotes/HitReact/Death's full-body base
        // layer poses; it's only switched on for the duration of firing.
        private void OnFireStarted(InputAction.CallbackContext context)
        {
            _isFiring = true;
            _hasLastArmsNormalizedTime = false;
            _stopArmsAt = float.PositiveInfinity;

            // Attack.started/canceled fire once per click, so spam-clicking would otherwise
            // re-trigger FireStart (and reset the loop to frame 0) on every single click. Only
            // (re)start the loop if it isn't already running/winding down from a previous click
            // still inside its grace window - see OnFireCanceled.
            if (_armsActive) return;

            _armsActive = true;
            if (_armsLayerIndex >= 0) animator.SetLayerWeight(_armsLayerIndex, 1f);
            if (animator != null)
            {
                animator.SetBool(FiringParam, true);
                animator.SetTrigger(FireStartParam);
            }
        }

        private void OnFireCanceled(InputAction.CallbackContext context)
        {
            _isFiring = false;
            // Don't wind the arms down immediately - give a grace window so the next click of a
            // spam-clicked burst (if it lands in time) can keep the same loop running instead of
            // restarting it. Update() actually applies the stop once the window elapses.
            _stopArmsAt = Time.time + armsStopGrace;
        }

        private void StopArmsImmediately()
        {
            _armsActive = false;
            _stopArmsAt = float.PositiveInfinity;
            if (animator != null) animator.SetBool(FiringParam, false);
            if (_armsLayerIndex >= 0 && animator != null) animator.SetLayerWeight(_armsLayerIndex, 0f);
        }

        private void SpawnMuzzleFlash()
        {
            if (muzzle == null) return;

            var flashGo = new GameObject("MuzzleFlash");
            flashGo.transform.SetParent(muzzle, false);

            var flashLight = flashGo.AddComponent<Light>();
            flashLight.type = LightType.Point;
            flashLight.color = muzzleFlashColor;
            flashLight.intensity = muzzleFlashIntensity;
            flashLight.range = muzzleFlashRange;

            Destroy(flashGo, muzzleFlashDuration);
        }

        // No traveling projectile: whatever the crosshair (screen center) is over resolves
        // damage immediately with a single raycast (called from CheckShootBeat, already timed to
        // the animation's shot beat). The old traveling-particle projectile was finicky (timing/
        // collision edge cases meant shots could visually connect but miss, or vice versa) -
        // resolving the hit against the crosshair's raycast removes that gap entirely, at the
        // cost of no travel time/lead requirement. The tracer below is purely a "shot fired"
        // visual, drawn after the hit is already resolved.
        private void FireHitscan()
        {
            if (muzzle == null) return;

            Vector3 hitPoint;

            if (aimCamera != null)
            {
                Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                if (Physics.Raycast(ray, out RaycastHit hit, maxAimDistance, aimMask, QueryTriggerInteraction.Ignore))
                {
                    hitPoint = hit.point;

                    var damageable = hit.collider.GetComponentInParent<IDamageable>();
                    if (damageable != null && !damageable.IsDead)
                    {
                        damageable.ApplyDamage(fireDamage, hit.point, gameObject);
                    }
                }
                else
                {
                    hitPoint = ray.origin + ray.direction * maxAimDistance;
                }
            }
            else
            {
                hitPoint = muzzle.position + muzzle.forward * maxAimDistance;
            }

            SpawnTracer(muzzle.position, hitPoint);
        }

        private void SpawnTracer(Vector3 start, Vector3 end)
        {
            var tracerGo = new GameObject("FireTracer");
            var line = tracerGo.AddComponent<LineRenderer>();
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.textureMode = LineTextureMode.Stretch;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.startWidth = tracerWidth;
            line.endWidth = tracerWidth;
            line.startColor = tracerColor;
            line.endColor = tracerColor;

            Destroy(tracerGo, tracerDuration);
        }
    }
}
