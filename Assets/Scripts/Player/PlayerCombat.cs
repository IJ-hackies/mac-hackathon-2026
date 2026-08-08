using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Combat;
using Enemies;
using UnityEngine;
using UnityEngine.InputSystem;
using Vfx;

namespace Player
{
    public class PlayerCombat : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private Transform muzzle;
        [SerializeField] private Camera aimCamera;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private PlayerAmmo playerAmmo;
        [SerializeField] private PlayerUltimate playerUltimate;

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
        [Tooltip("Viewport Y the aim raycast/crosshair use, in place of dead-center (0.5) - a " +
                 "taller character/camera rig leaves less visible ground below a center-screen " +
                 "crosshair, so this is moved up (toward 1) to open up the view ahead. " +
                 "CrosshairUI's own screen position (PlayerSceneSetup.BuildCrosshair) must be " +
                 "kept in sync with this value or the visual reticle and the actual aim point " +
                 "drift apart.")]
        [SerializeField, Range(0f, 1f)] private float aimViewportY = 0.5f;
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

        [Header("Tracer (fallback when no projectileVisualPrefab is set)")]
        [SerializeField] private float tracerDuration = 0.05f;
        [SerializeField] private float tracerWidth = 0.03f;
        [SerializeField] private Color tracerColor = new Color(0.4f, 0.85f, 1f);

        [Header("Visual Assets (optional imports)")]
        [Tooltip("Imported flash sprite played at the muzzle each shot, alongside the point " +
                 "light. Null = light only.")]
        [SerializeField] private GameObject muzzleFlashEffectPrefab;
        [SerializeField] private float muzzleFlashEffectScale = 0.3f;
        [Tooltip("Imported burst played where the travelling projectile actually connects (e.g. " +
                 "Lana Studio's Hit_dark_magic). Null = procedural spark.")]
        [SerializeField] private GameObject impactEffectPrefab;
        [SerializeField] private float impactEffectScale = 0.4f;
        [Tooltip("Imported projectile visual (e.g. Lana Studio's Projectiles_dark_magic) that " +
                 "IS the travelling, damage-dealing shot - see FireProjectile/BossProjectile. " +
                 "Null falls back to the plain LineRenderer tracer, which is purely cosmetic.")]
        [SerializeField] private GameObject projectileVisualPrefab;
        [SerializeField] private float projectileVisualScale = 0.4f;
        [SerializeField] private float projectileVisualSpeed = 160f;
        [Tooltip("Lana Studio's Range_attack prefabs are authored with their effect travelling " +
                 "along local +Y (a vertical pillar/beam by default), not +Z - BossProjectile's " +
                 "LookRotation only aligns local Z to the travel direction, so without this the " +
                 "beam renders as a near-vertical column at the muzzle regardless of aim (looked " +
                 "like it was \"shooting straight down\"). (90,0,0) rotates authored +Y onto +Z. " +
                 "Fine-tune by eye in Play mode if a specific prefab differs.")]
        [SerializeField] private Vector3 projectileVisualRotationOffsetEuler = new Vector3(0f, 90f, 0f);
        [SerializeField] private float projectileHitRadius = 0.25f;
        [SerializeField] private LayerMask enemyHitMask = ~0;

        [Header("Melee Hit VFX")]
        [Tooltip("Imported hit-spark (e.g. Lana Studio's Hit_stone) played on the enemy where a " +
                 "landed melee swing connects.")]
        [SerializeField] private GameObject meleeHitEffectPrefab;
        [SerializeField] private float meleeHitEffectScale = 1f;

        [Header("Ultimate Primary - Electric Machine Guns")]
        [Tooltip("Both fire together every shot beat (\"two projectiles constantly shooting out " +
                 "of the two machine guns\"), not alternating - see FireElectricBolts.")]
        [SerializeField] private Transform mechMuzzleLeft;
        [SerializeField] private Transform mechMuzzleRight;
        [SerializeField] private GameObject electricProjectilePrefab;
        [SerializeField] private float electricProjectileScale = 0.4f;
        // Same Lana Studio Range_attack authored-forward-is-+Y convention as
        // projectileVisualRotationOffsetEuler above.
        [SerializeField] private Quaternion electricProjectileRotationOffset = Quaternion.Euler(0f, 90f, 0f);
        [SerializeField] private GameObject electricImpactPrefab;
        [SerializeField] private float electricImpactScale = 1f;
        [SerializeField] private float electricDamage = 10f;
        [SerializeField] private float electricProjectileSpeed = 150f;
        [Tooltip("Placeholder balance meant to be scaled by a future powerups/upgrades system - " +
                 "base value confirmed at 5%.")]
        [SerializeField, Range(0f, 100f)] private float electricSlowPercent = 5f;
        [SerializeField] private float electricSlowDuration = 2.5f;

        [Header("Secondary Attack (right click)")]
        [Tooltip("Base (non-ultimate) secondary: single top-down beam-dot-purple on the nearest " +
                 "enemy. Not specified by design - placeholder tunable.")]
        [SerializeField] private GameObject topDownBeamDotPurplePrefab;
        [SerializeField] private float secondaryDamage = 25f;
        [SerializeField] private float secondaryCooldown = 4f;
        // Lowered 0.8s -> 0.25s -> 0.1s -> 0 - enemies kept walking out of the mark before the
        // telegraph resolved, since nothing slows/roots them during this window; even 0.1s wasn't
        // reliable. 0 makes Vfx.TopDownGroundEffect.Play skip the wait/telegraph entirely and
        // apply VFX + damage in the same frame as the click - "instant vfx and damage."
        [SerializeField] private float secondaryTelegraphDelay = 0f;
        [SerializeField] private float secondaryHitRadius = 3.2f;

        [Header("Ultimate Secondary - Lightning Circles")]
        [SerializeField] private GameObject lightningCirclePrefab;
        [Tooltip("Placeholder balance meant to be scaled by a future powerups/upgrades system - " +
                 "base value confirmed at 2.")]
        [SerializeField] private int lightningCircleCount = 2;
        [SerializeField] private float ultimateSecondaryDamage = 30f;
        [SerializeField] private float ultimateSecondaryCooldown = 7f;
        // Lowered 0.8s -> 0.25s -> 0.1s -> 0 - same reasoning as secondaryTelegraphDelay above.
        [SerializeField] private float ultimateSecondaryTelegraphDelay = 0f;
        [SerializeField] private float ultimateSecondaryHitRadius = 3.5f;

        private static readonly int MeleeParam = Animator.StringToHash("Melee");
        private static readonly int FireStartParam = Animator.StringToHash("FireStart");
        private static readonly int FiringParam = Animator.StringToHash("Firing");
        // Mech-only one-shot ("shoot_big" clip) - the astronaut's controller doesn't declare this
        // param, but SetTrigger on an unknown param name is a silent no-op, not an error, and
        // this only ever fires while playerUltimate.IsActive (the mech is the active animator).
        private static readonly int ShootBigParam = Animator.StringToHash("ShootBig");
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
        private bool _ultimateActive;
        private float _secondaryCooldownEndsAt = -999f;
        private readonly Dictionary<EnemyBase, int> _lightningOccurrences = new Dictionary<EnemyBase, int>();

        // _isFiring covers the whole held-fire duration (not just the instant of a shot beat, now
        // that shots are event-driven rather than an evenly spaced timer - see CheckShootBeat) so
        // the emote wheel still can't be opened mid-burst between individual shot beats.
        public bool IsAttacking => _isFiring || Time.time < _attackingUntil;
        public bool IsUltimateActive => _ultimateActive;
        public float SecondaryCooldownDuration => _ultimateActive ? ultimateSecondaryCooldown : secondaryCooldown;
        public float SecondaryCooldownRemaining => Mathf.Max(0f, _secondaryCooldownEndsAt - Time.time);
        public event System.Action SecondaryCooldownChanged;

        /// Called by PlayerUltimate on activate/end - swaps which attack profile FireProjectile/
        /// OnSecondaryPerformed use. Does not touch anything else (visual swap, shield reset,
        /// etc. are PlayerUltimate's own responsibility).
        public void SetUltimateActive(bool active)
        {
            _ultimateActive = active;
            _lightningOccurrences.Clear();
        }

        // Called by PlayerUltimate alongside SetUltimateActive - both the astronaut's and the
        // mech's AnimatorControllers share the same param/state-name contract (Melee, FireStart,
        // Firing, an "Arms" layer with an "Arms_Idle" state), so retargeting which one this
        // component drives is just swapping the reference and re-resolving the Arms layer index.
        public void SetAnimator(Animator target)
        {
            StopArmsImmediately();
            animator = target;
            _armsLayerIndex = animator != null ? animator.GetLayerIndex("Arms") : -1;
        }

        private void Awake()
        {
            _actions = new InputSystem_Actions();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (aimCamera == null) aimCamera = Camera.main;
            if (animator != null) _armsLayerIndex = animator.GetLayerIndex("Arms");
            if (playerController == null) playerController = GetComponent<PlayerController>();
            if (playerAmmo == null) playerAmmo = GetComponent<PlayerAmmo>();
            if (playerUltimate == null) playerUltimate = GetComponent<PlayerUltimate>();
        }

        private void OnEnable()
        {
            _actions.Player.Enable();
            _actions.Player.Melee.performed += OnMeleePerformed;
            _actions.Player.Attack.started += OnFireStarted;
            _actions.Player.Attack.canceled += OnFireCanceled;
            _actions.Player.Reload.performed += OnReloadPerformed;
            _actions.Player.Attack2.performed += OnSecondaryPerformed;
        }

        private void OnDisable()
        {
            _actions.Player.Melee.performed -= OnMeleePerformed;
            _actions.Player.Attack.started -= OnFireStarted;
            _actions.Player.Attack.canceled -= OnFireCanceled;
            _actions.Player.Reload.performed -= OnReloadPerformed;
            _actions.Player.Attack2.performed -= OnSecondaryPerformed;
            _actions.Player.Disable();

            _isFiring = false;
            // Otherwise a frozen Arms-layer shoot pose keeps overriding the base layer's arms
            // (e.g. Death's full-body pose) after PlayerDeathHandler disables this component.
            StopArmsImmediately();
        }

        private void Update()
        {
            // A stagger (e.g. BossMechAI's ground-slam) can land mid-hold - cut firing off
            // immediately rather than waiting for the player to release Attack themselves, so
            // "cannot continue shooting" holds even for an already-in-progress burst.
            if (playerController != null && playerController.IsStaggered && _isFiring)
            {
                _isFiring = false;
                StopArmsImmediately();
            }

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

            // Ammo is drawn per beat, not per click - a held burst still consumes one round per
            // shot. TryConsumeRound auto-starts a reload once the magazine empties, and a reload
            // in progress simply withholds the shot without cancelling the Shoot animation loop.
            if (playerAmmo != null && !playerAmmo.TryConsumeRound()) return;

            FireProjectile();
            SpawnMuzzleFlash();
        }

        private void OnReloadPerformed(InputAction.CallbackContext context)
        {
            if (playerController != null && playerController.IsStaggered) return;
            playerAmmo?.StartReload();
        }

        private void OnMeleePerformed(InputAction.CallbackContext context)
        {
            if (playerController != null && playerController.IsStaggered) return;
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

            Vector3 origin = transform.position + transform.up + transform.forward * meleeRange;
            var hits = Physics.OverlapSphere(origin, meleeRadius, ~0, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (hit.transform.root == transform.root) continue;

                var damageable = hit.GetComponentInParent<IDamageable>();
                if (damageable == null || damageable.IsDead) continue;

                Vector3 hitPoint = hit.ClosestPoint(origin);
                damageable.ApplyDamage(meleeDamage, hitPoint, gameObject, DamageType.Melee);
                SpawnMeleeHitEffect(hitPoint);
            }
        }

        private void SpawnMeleeHitEffect(Vector3 point)
        {
            if (meleeHitEffectPrefab == null) return;

            var effect = Instantiate(meleeHitEffectPrefab, point, Quaternion.identity);
            effect.transform.localScale = Vector3.one * meleeHitEffectScale;
            ImportedVfxUtility.FixUrpMaterials(effect);
            ImportedVfxUtility.ForceHierarchyParticleScaling(effect);
            Destroy(effect, 2f);
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
            if (playerController != null && playerController.IsStaggered) return;

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

            if (muzzleFlashEffectPrefab != null)
            {
                var flashVisual = Instantiate(muzzleFlashEffectPrefab, muzzle.position, muzzle.rotation, muzzle);
                flashVisual.transform.localScale = Vector3.one * muzzleFlashEffectScale;
                ImportedVfxUtility.FixUrpMaterials(flashVisual);
                Destroy(flashVisual, 1f);
            }

            Destroy(flashGo, muzzleFlashDuration);
        }

        // Spawns a real travelling BossProjectile aimed at whatever the crosshair (screen center)
        // is currently over - damage now resolves on the projectile's own collision (see
        // BossProjectile.OnTriggerEnter), not here. Only the aim DIRECTION is decided instantly by
        // this raycast; a fast-moving target can still duck under/behind the shot after it's
        // fired, unlike the old instant hitscan. Reuses Enemies.BossProjectile - the same
        // generic travelling-projectile component the bosses/flying enemy use - rather than a
        // player-specific duplicate.
        private void FireProjectile()
        {
            if (_ultimateActive)
            {
                FireElectricBolts();
                return;
            }

            if (muzzle == null) return;

            Vector3 aimDirection = ComputeAimDirection(muzzle.position);

            if (projectileVisualPrefab != null)
            {
                float lifetime = Mathf.Max(maxAimDistance / projectileVisualSpeed, 0.5f);
                var visuals = new BossProjectileVisuals
                {
                    ImportedVisualPrefab = projectileVisualPrefab,
                    ImportedVisualScale = projectileVisualScale,
                    ExtraRotationOffset = Quaternion.Euler(projectileVisualRotationOffsetEuler),
                    ImpactEffectPrefab = impactEffectPrefab,
                    ImpactEffectScale = impactEffectScale,
                };
                BossProjectile.Create(muzzle.position, aimDirection, null, projectileVisualSpeed,
                    fireDamage, false, lifetime, enemyHitMask, tracerColor, projectileHitRadius,
                    ProjectileVisualStyle.Bolt, visuals: visuals);
            }
            else
            {
                // Fallback when no imported projectile prefab is assigned: keep the previous
                // instant-hitscan-with-tracer behavior so ranged combat still works out of the box.
                if (Physics.Raycast(muzzle.position, aimDirection, out RaycastHit fallbackHit, maxAimDistance, aimMask, QueryTriggerInteraction.Ignore))
                {
                    var damageable = fallbackHit.collider.GetComponentInParent<IDamageable>();
                    if (damageable != null && !damageable.IsDead)
                    {
                        damageable.ApplyDamage(fireDamage, fallbackHit.point, gameObject, DamageType.Ranged);
                    }

                    SpawnTracer(muzzle.position, fallbackHit.point);
                }
                else
                {
                    SpawnTracer(muzzle.position, muzzle.position + aimDirection * maxAimDistance);
                }
            }
        }

        private Vector3 ComputeAimDirection(Vector3 origin)
        {
            if (aimCamera == null) return muzzle != null ? muzzle.forward : transform.forward;

            Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, aimViewportY, 0f));
            Vector3 aimPoint = Physics.Raycast(ray, out RaycastHit hit, maxAimDistance, aimMask, QueryTriggerInteraction.Ignore)
                ? hit.point
                : ray.origin + ray.direction * maxAimDistance;
            return (aimPoint - origin).normalized;
        }

        // Ultimate primary: both mech arm cannons fire together every shot beat (not
        // alternating like BossMechAI's Shoot_Small/Big) - "two projectiles constantly shooting
        // out of the two machine guns." Each bolt applies EnemyBase.ApplySlow on hit via
        // BossProjectile's onHit callback.
        private void FireElectricBolts()
        {
            FireElectricBolt(mechMuzzleLeft);
            FireElectricBolt(mechMuzzleRight);
        }

        private void FireElectricBolt(Transform fromMuzzle)
        {
            if (fromMuzzle == null) return;

            Vector3 aimDirection = ComputeAimDirection(fromMuzzle.position);
            float lifetime = Mathf.Max(maxAimDistance / electricProjectileSpeed, 0.5f);
            var visuals = new BossProjectileVisuals
            {
                ImportedVisualPrefab = electricProjectilePrefab,
                ImportedVisualScale = electricProjectileScale,
                ExtraRotationOffset = electricProjectileRotationOffset,
                ImpactEffectPrefab = electricImpactPrefab,
                ImpactEffectScale = electricImpactScale,
            };

            float slowMultiplier = 1f - Mathf.Clamp01(electricSlowPercent / 100f);
            BossProjectile.Create(fromMuzzle.position, aimDirection, null, electricProjectileSpeed,
                electricDamage, false, lifetime, enemyHitMask, tracerColor, projectileHitRadius,
                ProjectileVisualStyle.Bolt, visuals: visuals,
                onHit: hitGameObject =>
                {
                    var enemy = hitGameObject.GetComponentInParent<EnemyBase>();
                    enemy?.ApplySlow(slowMultiplier, electricSlowDuration);
                });
        }

        private void OnSecondaryPerformed(InputAction.CallbackContext context)
        {
            if (playerController != null && playerController.IsStaggered) return;
            if (SecondaryCooldownRemaining > 0f) return;

            _secondaryCooldownEndsAt = Time.time + SecondaryCooldownDuration;
            SecondaryCooldownChanged?.Invoke();

            if (_ultimateActive) FireLightningCircles();
            else FireSingleTopDownBeam();
        }

        // Base (non-ultimate) secondary: one top-down beam-dot-purple on the single nearest
        // live enemy.
        private void FireSingleTopDownBeam()
        {
            var nearest = FindNearestEnemies(transform.position, 1);
            if (nearest.Count == 0) return;

            EnemyBase target = nearest[0];
            Vector3 point = target.transform.position;
            StartCoroutine(TopDownGroundEffect.Play(topDownBeamDotPurplePrefab, point,
                secondaryTelegraphDelay, 1f, () => DamageIfStillNear(target, point, secondaryHitRadius, secondaryDamage)));
        }

        // Ultimate secondary: lightningCircleCount circles targeting the N nearest live enemies.
        // Extra circles beyond the live enemy count retarget the closest enemies round-robin,
        // with per-target repeat-hit damage falloff (100/80/60/40/20/20/... floored at 20%,
        // never zero) tracked via _lightningOccurrences for the duration of this single cast.
        private void FireLightningCircles()
        {
            var nearest = FindNearestEnemies(transform.position, Mathf.Max(1, lightningCircleCount));
            if (nearest.Count == 0) return;

            if (animator != null) animator.SetTrigger(ShootBigParam);

            _lightningOccurrences.Clear();
            for (int i = 0; i < lightningCircleCount; i++)
            {
                EnemyBase target = nearest[i % nearest.Count];
                int occurrence = _lightningOccurrences.TryGetValue(target, out int existing) ? existing : 0;
                _lightningOccurrences[target] = occurrence + 1;

                float damage = ultimateSecondaryDamage * Mathf.Max(0.2f, 1f - 0.2f * occurrence);
                Vector3 point = target.transform.position;
                StartCoroutine(TopDownGroundEffect.Play(lightningCirclePrefab, point,
                    ultimateSecondaryTelegraphDelay, 1f, () => DamageIfStillNear(target, point, ultimateSecondaryHitRadius, damage)));
            }
        }

        private static void DamageIfStillNear(EnemyBase target, Vector3 point, float radius, float damage)
        {
            if (target == null) return;
            var damageable = target.GetComponent<IDamageable>();
            if (damageable == null || damageable.IsDead) return;
            if (Vector3.Distance(target.transform.position, point) > radius) return;

            damageable.ApplyDamage(damage, point, target.gameObject, DamageType.Ranged);
        }

        private static List<EnemyBase> FindNearestEnemies(Vector3 origin, int count)
        {
            return FindObjectsByType<EnemyBase>(FindObjectsSortMode.None)
                .Where(e => e != null && !e.GetComponent<Health>().IsDead)
                .OrderBy(e => Vector3.Distance(origin, e.transform.position))
                .Take(count)
                .ToList();
        }

        // Tapered (thick at the muzzle, thin at the hit point) rather than a uniform-width line -
        // reads more like an actual laser bolt than a flat ruler stroke. Only used as the no-
        // imported-prefab fallback in FireProjectile.
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
            line.startWidth = tracerWidth * 1.8f;
            line.endWidth = tracerWidth * 0.4f;
            line.startColor = tracerColor;
            line.endColor = new Color(tracerColor.r, tracerColor.g, tracerColor.b, 0.3f);

            Destroy(tracerGo, tracerDuration);
        }
    }
}
