using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Audio;
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
        [Tooltip("The Attack action is already bound to left click in " +
                 "InputSystem_Actions; reused here for firing rather than adding a new action. " +
                 "Each click fires one shot until the Hold to Fire upgrade is purchased.")]
        [SerializeField] private float fireDamage = 15f;
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
        [Tooltip("The base interval between pistol shots, whether clicked or held. Purchased " +
                  "fire-rate upgrades multiply this cadence without changing the rest of the " +
                  "Animator's playback speed.")]
        [SerializeField, Min(0.01f)] private float holdFireInterval = 0.5f;

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

        [Header("Special Skills")]
        [SerializeField, Min(0f)] private float bulletBounceRange = 15f;
        [SerializeField, Range(1, 3)] private int bulletBounceMaximumTargets = 3;
        [SerializeField, Min(0f)] private float explosiveBulletRadius = 3f;
        [SerializeField, Range(0f, 1f)] private float explosiveBulletDamageFraction = 0.5f;
        [SerializeField, Range(0f, 1f)] private float vampireDamageFraction = 0.02f;

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

        private InputSystem_Actions _actions;
        private float _lastMeleeTime = -999f;
        private float _attackingUntil = -999f;
        private bool _isFiring;
        private int _armsLayerIndex = -1;
        private bool _armsActive;
        private float _stopArmsAt = float.PositiveInfinity;
        private bool _ultimateActive;
        private AudioHandle _mechFireLoopHandle;
        private bool _holdToFireUnlocked;
        private bool _minigunEnabled;
        private bool _bulletBounceEnabled;
        private bool _explosiveBulletsEnabled;
        private bool _headshotEnabled;
        private bool _vampireEnabled;
        private int _ordinaryPistolRoundsFired;
        private float _nextPrimaryFireTime;
        private readonly Dictionary<object, float> _rangedDamageModifiers = new Dictionary<object, float>();
        private readonly Dictionary<object, float> _rangedDamageBonuses = new Dictionary<object, float>();
        private readonly Dictionary<object, float> _meleeDamageModifiers = new Dictionary<object, float>();
        private readonly Dictionary<object, float> _meleeDamageBonuses = new Dictionary<object, float>();
        private readonly Dictionary<object, float> _fireRateModifiers = new Dictionary<object, float>();
        private float _secondaryCooldownEndsAt = -999f;
        private readonly Dictionary<EnemyBase, int> _lightningOccurrences = new Dictionary<EnemyBase, int>();
        private Health _playerHealth;

        // _isFiring covers the whole held-fire duration rather than only individual shot events,
        // so the emote wheel cannot be opened partway through an automatic burst.
        public bool IsAttacking => _isFiring || Time.time < _attackingUntil;
        public bool IsUltimateActive => _ultimateActive;
        public float SecondaryCooldownDuration => _ultimateActive ? ultimateSecondaryCooldown : secondaryCooldown;
        public float SecondaryCooldownRemaining => Mathf.Max(0f, _secondaryCooldownEndsAt - Time.time);
        public float BaseRangedDamage => fireDamage;
        public float BaseMeleeDamage => meleeDamage;
        public float EffectiveRangedDamage => (fireDamage + RangedDamageBonus) * RangedDamageMultiplier;
        /// <summary>
        /// The ordinary pistol's final hit damage. Minigun subtracts 20 raw damage before every
        /// general ranged multiplier (including the secret skill), and never lets the result fall
        /// below one damage.
        /// </summary>
        public float EffectivePistolDamage => Mathf.Max(1f,
            (fireDamage + RangedDamageBonus - (_minigunEnabled ? 20f : 0f)) * RangedDamageMultiplier);
        public float EffectiveMeleeDamage => (meleeDamage + MeleeDamageBonus) * MeleeDamageMultiplier;
        public float EffectiveSecondaryDamage => (secondaryDamage + RangedDamageBonus) * RangedDamageMultiplier;
        public float EffectiveElectricDamage => (electricDamage + RangedDamageBonus) * RangedDamageMultiplier;
        public float EffectiveUltimateSecondaryDamage => (ultimateSecondaryDamage + RangedDamageBonus) * RangedDamageMultiplier;
        public float RangedDamageMultiplier { get; private set; } = 1f;
        public float RangedDamageBonus { get; private set; }
        public float MeleeDamageMultiplier { get; private set; } = 1f;
        public float MeleeDamageBonus { get; private set; }
        public float FireRateMultiplier { get; private set; } = 1f;
        public bool HoldToFireUnlocked => _holdToFireUnlocked;
        public bool MinigunEnabled => _minigunEnabled;
        public bool BulletBounceEnabled => _bulletBounceEnabled;
        public bool ExplosiveBulletsEnabled => _explosiveBulletsEnabled;
        public bool HeadshotEnabled => _headshotEnabled;
        public bool VampireEnabled => _vampireEnabled;
        public int OrdinaryPistolRoundsFired => _ordinaryPistolRoundsFired;
        public event System.Action SecondaryCooldownChanged;

        /// Called by PlayerUltimate on activate/end - swaps which attack profile FireProjectile/
        /// OnSecondaryPerformed use. Does not touch anything else (visual swap, shield reset,
        /// etc. are PlayerUltimate's own responsibility).
        public void SetUltimateActive(bool active)
        {
            _ultimateActive = active;
            _lightningOccurrences.Clear();
            if (active && _actions != null && _actions.Player.Attack.IsPressed())
            {
                // An Ultimate can be activated while the player is already holding Attack. Its
                // next Update happens after PlayerUltimate grants infinite ammo, so this starts
                // the electric-gun hold without spending an ordinary magazine round.
                _isFiring = true;
                _nextPrimaryFireTime = Time.time;
                EnsureArmsFiringPose();
            }
            if (!active && !_holdToFireUnlocked && _isFiring)
            {
                // If Ultimate expires while Attack is still held, return to the ordinary
                // click-only pistol immediately instead of leaving an invisible burst active.
                _isFiring = false;
                _stopArmsAt = Time.time + armsStopGrace;
            }
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
            EnsureRuntimeState();
        }

        private void EnsureRuntimeState()
        {
            if (_actions == null) _actions = PlayerInputBindings.CreateActions();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (aimCamera == null) aimCamera = Camera.main;
            if (animator != null) _armsLayerIndex = animator.GetLayerIndex("Arms");
            if (playerController == null) playerController = GetComponent<PlayerController>();
            if (playerAmmo == null) playerAmmo = GetComponent<PlayerAmmo>();
            if (playerUltimate == null) playerUltimate = GetComponent<PlayerUltimate>();
            if (_playerHealth == null) _playerHealth = GetComponent<Health>();
        }

        private void OnEnable()
        {
            EnsureRuntimeState();
            _actions.Player.Enable();
            _actions.Player.Melee.performed += OnMeleePerformed;
            _actions.Player.Attack.started += OnFireStarted;
            _actions.Player.Attack.canceled += OnFireCanceled;
            _actions.Player.Reload.performed += OnReloadPerformed;
            _actions.Player.Attack2.performed += OnSecondaryPerformed;
        }

        private void OnDisable()
        {
            if (_actions != null)
            {
                _actions.Player.Melee.performed -= OnMeleePerformed;
                _actions.Player.Attack.started -= OnFireStarted;
                _actions.Player.Attack.canceled -= OnFireCanceled;
                _actions.Player.Reload.performed -= OnReloadPerformed;
                _actions.Player.Attack2.performed -= OnSecondaryPerformed;
                _actions.Player.Disable();
            }

            _isFiring = false;
            // Otherwise a frozen Arms-layer shoot pose keeps overriding the base layer's arms
            // (e.g. Death's full-body pose) after PlayerDeathHandler disables this component.
            StopArmsImmediately();

            if (_mechFireLoopHandle.IsValid)
            {
                AudioManager.Instance.StopLoop(_mechFireLoopHandle);
            }
        }

        private void OnDestroy()
        {
            PlayerInputBindings.ReleaseActions(_actions);
            _actions = null;
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

            if (_isFiring && (_holdToFireUnlocked || _ultimateActive))
            {
                TryFirePrimaryAtCurrentCadence();
            }
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

        // Mech (ultimate) melee scales the swing by the SAME factor the mech visual itself scales
        // by (PlayerUltimate.mechScale, 1.4x) - PlayerUltimate only resizes the visual mesh
        // (mechVisualRoot.localScale), not the physical CapsuleCollider (PlayerController._capsule
        // stays fixed regardless of ultimate state), so the swing needs to grow by the same amount
        // the arms visually do or it reads as too short/thin for the bigger model. Real, justified
        // multiplier - not a guessed one - since it's the exact factor already driving the mech's
        // own visual scale (see PlayerUltimate.VfxScaleMultiplier).
        private float MeleeScale => playerUltimate != null ? playerUltimate.VfxScaleMultiplier : 1f;

        private IEnumerator MeleeDamageWindow()
        {
            yield return new WaitForSeconds(meleeHitDelay);

            float scale = MeleeScale;
            float range = meleeRange * scale;
            float radius = meleeRadius * scale;

            // A real swept capsule (near the body out to the reach point) instead of a single
            // sphere sitting only at the tip - a swing covers the space in between, not just its
            // furthest point.
            Vector3 start = transform.position + transform.up;
            Vector3 end = start + transform.forward * range;
            var hits = Physics.OverlapCapsule(start, end, radius, ~0, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (hit.transform.root == transform.root) continue;

                var damageable = hit.GetComponentInParent<IDamageable>();
                if (damageable == null || damageable.IsDead) continue;

                Vector3 hitPoint = hit.ClosestPoint(end);
                float appliedDamage = DealPlayerDamage(damageable, EffectiveMeleeDamage, hitPoint, DamageType.Melee);
                if (appliedDamage > 0f) Combat.DamageNumberSpawner.Spawn(hitPoint, appliedDamage);
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

        // The Arms layer still provides the shooting pose for a click or a held burst. Gameplay
        // shots themselves are input-driven: each click requests one round, while Hold to Fire
        // keeps requesting rounds automatically. Both paths share the same fire-rate-scaled gate,
        // so repeated clicks cannot bypass the player's current attack speed.
        // The Arms layer (upper-body-masked, see PlayerSceneSetup.BuildArmsLayer) sits at weight
        // 0 the rest of the time so it doesn't fight Melee/Emotes/HitReact/Death's full-body base
        // layer poses; it's only switched on for the duration of firing.
        private void OnFireStarted(InputAction.CallbackContext context)
        {
            if (playerController != null && playerController.IsStaggered) return;

            // Ultimate's dual-cannon fire is a sustained loop (first half of the clip, restarted -
            // see AudioManager.PlayLoop), not a per-shot one-shot like the base rifle - started
            // here alongside the first shot rather than per-shot in FireProjectile/
            // FireElectricBolts. Guarded by IsValid so spam-clicking (each click re-enters
            // OnFireStarted) doesn't restart the loop from frame 0 while it's already running.
            if (_ultimateActive && !_mechFireLoopHandle.IsValid)
            {
                _mechFireLoopHandle = AudioManager.Instance.PlayLoop(SfxId.MechShootPrimaryLoop, transform);
            }

            _isFiring = _holdToFireUnlocked || _ultimateActive;
            TryFirePrimaryAtCurrentCadence();
            EnsureArmsFiringPose();
        }

        private void OnFireCanceled(InputAction.CallbackContext context)
        {
            _isFiring = false;
            // Don't wind the arms down immediately - give a grace window so the next click of a
            // spam-clicked burst (if it lands in time) can keep the same loop running instead of
            // restarting it. Update() actually applies the stop once the window elapses.
            _stopArmsAt = Time.time + armsStopGrace;

            if (_mechFireLoopHandle.IsValid)
            {
                AudioManager.Instance.StopLoop(_mechFireLoopHandle);
            }
        }

        /// <summary>Enables the one-time Hold to Fire upgrade.</summary>
        public void SetHoldToFireUnlocked(bool unlocked)
        {
            _holdToFireUnlocked = unlocked;
            if (!unlocked && !_ultimateActive) _isFiring = false;
        }

        /// <summary>Enables Minigun's pistol-only damage/cadence changes.</summary>
        public void SetMinigunEnabled(bool enabled) => _minigunEnabled = enabled;

        public void SetBulletBounceEnabled(bool enabled) => _bulletBounceEnabled = enabled;

        public void SetExplosiveBulletsEnabled(bool enabled) => _explosiveBulletsEnabled = enabled;

        public void SetHeadshotEnabled(bool enabled) => _headshotEnabled = enabled;

        public void SetVampireEnabled(bool enabled) => _vampireEnabled = enabled;

        /// <summary>Called when beginning a fresh run; cadence survives reloads and waves only.</summary>
        public void ResetOrdinaryPistolRoundCount() => _ordinaryPistolRoundsFired = 0;

        public void SetRangedDamageModifier(object source, float multiplier) =>
            SetMultiplier(_rangedDamageModifiers, source, multiplier, value => RangedDamageMultiplier = value);

        /// <summary>
        /// Adds a raw ranged-damage amount to every player shooting attack: pistol, normal
        /// secondary, Ultimate bolts, and Ultimate lightning. Kept separate from multipliers so
        /// future temporary effects can still compose multiplicatively.
        /// </summary>
        public void SetRangedDamageBonus(object source, float amount) =>
            SetAdditiveModifier(_rangedDamageBonuses, source, amount, value => RangedDamageBonus = value);

        public void SetMeleeDamageModifier(object source, float multiplier) =>
            SetMultiplier(_meleeDamageModifiers, source, multiplier, value => MeleeDamageMultiplier = value);

        /// <summary>Adds raw melee damage without replacing independent multiplier effects.</summary>
        public void SetMeleeDamageBonus(object source, float amount) =>
            SetAdditiveModifier(_meleeDamageBonuses, source, amount, value => MeleeDamageBonus = value);

        public void SetFireRateModifier(object source, float multiplier) =>
            SetMultiplier(_fireRateModifiers, source, multiplier, value => FireRateMultiplier = value);

        public void RemoveRangedDamageModifier(object source) =>
            RemoveMultiplier(_rangedDamageModifiers, source, value => RangedDamageMultiplier = value);

        public void RemoveRangedDamageBonus(object source) =>
            RemoveAdditiveModifier(_rangedDamageBonuses, source, value => RangedDamageBonus = value);

        public void RemoveMeleeDamageModifier(object source) =>
            RemoveMultiplier(_meleeDamageModifiers, source, value => MeleeDamageMultiplier = value);

        public void RemoveMeleeDamageBonus(object source) =>
            RemoveAdditiveModifier(_meleeDamageBonuses, source, value => MeleeDamageBonus = value);

        public void RemoveFireRateModifier(object source) =>
            RemoveMultiplier(_fireRateModifiers, source, value => FireRateMultiplier = value);

        private static void SetMultiplier(Dictionary<object, float> modifiers, object source, float multiplier,
            System.Action<float> apply)
        {
            if (source == null) throw new System.ArgumentNullException(nameof(source));
            if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier < 0f)
                throw new System.ArgumentOutOfRangeException(nameof(multiplier));
            modifiers[source] = multiplier;
            apply(ResolveMultiplier(modifiers));
        }

        private static void RemoveMultiplier(Dictionary<object, float> modifiers, object source,
            System.Action<float> apply)
        {
            if (source != null && modifiers.Remove(source)) apply(ResolveMultiplier(modifiers));
        }

        private static float ResolveMultiplier(Dictionary<object, float> modifiers)
        {
            float result = 1f;
            foreach (float modifier in modifiers.Values) result *= modifier;
            return result;
        }

        private static void SetAdditiveModifier(Dictionary<object, float> modifiers, object source, float amount,
            System.Action<float> apply)
        {
            if (source == null) throw new System.ArgumentNullException(nameof(source));
            if (float.IsNaN(amount) || float.IsInfinity(amount))
                throw new System.ArgumentOutOfRangeException(nameof(amount));
            modifiers[source] = amount;
            apply(ResolveAdditiveModifier(modifiers));
        }

        private static void RemoveAdditiveModifier(Dictionary<object, float> modifiers, object source,
            System.Action<float> apply)
        {
            if (source != null && modifiers.Remove(source)) apply(ResolveAdditiveModifier(modifiers));
        }

        private static float ResolveAdditiveModifier(Dictionary<object, float> modifiers)
        {
            float result = 0f;
            foreach (float modifier in modifiers.Values) result += modifier;
            return result;
        }

        private bool TryFireShot()
        {
            if (playerAmmo != null && !playerAmmo.TryConsumeRound()) return false;

            // A Headshot round is determined only after a real ordinary pistol round is consumed.
            // Ultimate beats consume no normal ammunition and therefore never advance this count.
            bool headshot = !_ultimateActive && _headshotEnabled && ++_ordinaryPistolRoundsFired % 4 == 0;
            if (!_ultimateActive && !_headshotEnabled) _ordinaryPistolRoundsFired++;

            FireProjectile(headshot);
            SpawnMuzzleFlash();
            return true;
        }

        private bool TryFirePrimaryAtCurrentCadence()
        {
            if (Time.time < _nextPrimaryFireTime) return false;

            _nextPrimaryFireTime = Time.time + EffectivePrimaryFireInterval;
            return TryFireShot();
        }

        // Fire-rate upgrades apply to the ordinary pistol. Ultimate has its own fixed electric
        // machine-gun profile, and remains held-fire capable even before Hold to Fire is owned.
        private float EffectivePrimaryFireInterval => holdFireInterval /
            Mathf.Max(0.01f, _ultimateActive ? 1f : FireRateMultiplier * (_minigunEnabled ? 2f : 1f));

        private void EnsureArmsFiringPose()
        {
            _stopArmsAt = float.PositiveInfinity;
            // Preserve the pose through quick successive clicks instead of restarting the Arms
            // animation every time; every started event has already fired its own round.
            if (_armsActive) return;

            _armsActive = true;
            if (_armsLayerIndex >= 0) animator.SetLayerWeight(_armsLayerIndex, 1f);
            if (animator != null)
            {
                animator.SetBool(FiringParam, true);
                animator.SetTrigger(FireStartParam);
            }
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
        private void FireProjectile(bool headshot)
        {
            if (_ultimateActive)
            {
                // Sustained MechShootPrimaryLoop (started in OnFireStarted/stopped in
                // OnFireCanceled) already covers this beat's audio - no per-shot one-shot here.
                FireElectricBolts();
                return;
            }

            AudioManager.Instance.PlaySfx(SfxId.PlayerShootPrimary, muzzle != null ? muzzle.position : transform.position);

            if (muzzle == null) return;

            Vector3 aimDirection = ComputeAimDirection(muzzle.position);
            var shot = new PistolShotState(
                EffectivePistolDamage * (headshot ? 2f : 1f),
                _bulletBounceEnabled ? bulletBounceMaximumTargets : 1,
                _explosiveBulletsEnabled,
                headshot);

            if (projectileVisualPrefab != null)
            {
                SpawnPistolProjectile(muzzle.position, aimDirection, shot);
            }
            else
            {
                FirePistolHitscan(muzzle.position, aimDirection, shot);
            }
        }

        private void SpawnPistolProjectile(Vector3 origin, Vector3 direction, PistolShotState shot)
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
            BossProjectile.Create(origin, direction, null, projectileVisualSpeed,
                shot.ImpactDamage, false, lifetime, enemyHitMask, tracerColor, projectileHitRadius,
                ProjectileVisualStyle.Bolt, visuals: visuals,
                onDamage: hit => HandlePistolProjectileImpact(hit, shot));
        }

        // The no-import fallback keeps all gameplay identical to projectiles: a chain still
        // visits no more than three targets and each impact still resolves splash/lifesteal.
        private void FirePistolHitscan(Vector3 origin, Vector3 direction, PistolShotState shot)
        {
            if (Physics.Raycast(origin, direction, out RaycastHit hit, maxAimDistance, aimMask,
                    QueryTriggerInteraction.Ignore))
            {
                SpawnTracer(origin, hit.point);
                var damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null && !damageable.IsDead)
                {
                    ResolvePistolImpact(damageable, hit.point, shot);
                }
                return;
            }

            SpawnTracer(origin, origin + direction * maxAimDistance);
        }

        private void HandlePistolProjectileImpact(ProjectileHit hit, PistolShotState shot)
        {
            if (hit.Damageable == null) return;
            HandlePistolImpact(hit.Damageable, hit.Point, hit.AppliedDamage, shot);
        }

        private void ResolvePistolImpact(IDamageable target, Vector3 point, PistolShotState shot)
        {
            if (target == null || target.IsDead || !shot.RegisterTarget(target)) return;
            float appliedDamage = DealPlayerDamage(target, shot.ImpactDamage, point, DamageType.Ranged);
            FinishPistolImpact(target, point, appliedDamage, shot);
        }

        private void HandlePistolImpact(IDamageable target, Vector3 point, float appliedDamage,
            PistolShotState shot)
        {
            if (!shot.RegisterTarget(target)) return;
            ApplyVampireHealing(appliedDamage);
            FinishPistolImpact(target, point, appliedDamage, shot);
        }

        private void FinishPistolImpact(IDamageable directTarget, Vector3 point, float appliedDamage,
            PistolShotState shot)
        {
            if (appliedDamage > 0f) Combat.DamageNumberSpawner.Spawn(point + Vector3.up, appliedDamage);

            if (shot.Explosive)
            {
                ApplyPistolExplosion(point, directTarget, shot.ImpactDamage * explosiveBulletDamageFraction);
            }

            if (!shot.CanBounce) return;

            EnemyBase nextTarget = FindNextBounceTarget(point, shot.VisitedTargets);
            if (nextTarget == null) return;

            IDamageable nextDamageable = nextTarget.GetComponent<IDamageable>();
            if (nextDamageable == null) return;

            Vector3 nextPoint = nextTarget.transform.position + Vector3.up;
            Vector3 direction = nextPoint - point;
            if (direction.sqrMagnitude <= 0.0001f) return;

            if (projectileVisualPrefab != null)
            {
                // A small forward offset avoids re-hitting the just-struck target's collider.
                SpawnPistolProjectile(point + direction.normalized * 0.08f, direction.normalized, shot);
            }
            else
            {
                SpawnTracer(point, nextPoint);
                ResolvePistolImpact(nextDamageable, nextPoint, shot);
            }
        }

        private void ApplyPistolExplosion(Vector3 point, IDamageable directTarget, float intendedDamage)
        {
            if (intendedDamage <= 0f) return;

            var uniqueTargets = new HashSet<IDamageable>();
            foreach (Collider collider in Physics.OverlapSphere(point, explosiveBulletRadius, enemyHitMask,
                         QueryTriggerInteraction.Ignore))
            {
                if (collider.transform.root == transform.root) continue;
                IDamageable target = collider.GetComponentInParent<IDamageable>();
                if (target == null || target == directTarget || target.IsDead || !uniqueTargets.Add(target)) continue;

                Vector3 hitPoint = collider.ClosestPoint(point);
                float appliedDamage = DealPlayerDamage(target, intendedDamage, hitPoint, DamageType.Ranged);
                if (appliedDamage > 0f) Combat.DamageNumberSpawner.Spawn(hitPoint + Vector3.up, appliedDamage);
            }
        }

        private EnemyBase FindNextBounceTarget(Vector3 point, HashSet<IDamageable> visitedTargets)
        {
            return FindObjectsByType<EnemyBase>(FindObjectsSortMode.None)
                .Where(enemy => enemy != null)
                .Where(enemy =>
                {
                    IDamageable damageable = enemy.GetComponent<IDamageable>();
                    return damageable != null && !damageable.IsDead && !visitedTargets.Contains(damageable);
                })
                .Where(enemy => Vector3.Distance(point, enemy.transform.position) <= bulletBounceRange)
                .OrderBy(enemy => Vector3.Distance(point, enemy.transform.position))
                .FirstOrDefault(enemy => HasClearBounceLineOfSight(point, enemy));
        }

        private bool HasClearBounceLineOfSight(Vector3 origin, EnemyBase target)
        {
            Vector3 targetPoint = target.transform.position + Vector3.up;
            Vector3 offset = targetPoint - origin;
            float distance = offset.magnitude;
            if (distance <= 0.001f) return true;

            Vector3 direction = offset / distance;
            RaycastHit[] hits = Physics.RaycastAll(origin + direction * 0.08f, direction,
                Mathf.Max(0f, distance - 0.08f), aimMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.root == transform.root) continue;
                return hit.collider.GetComponentInParent<EnemyBase>() == target;
            }

            // The target may use a CharacterController rather than a raycastable collider. No
            // intervening hit still constitutes clear line of sight in that case.
            return true;
        }

        private float DealPlayerDamage(IDamageable target, float intendedDamage, Vector3 point,
            DamageType damageType)
        {
            if (target == null || target.IsDead || intendedDamage <= 0f) return 0f;

            float appliedDamage;
            if (target is Health health)
            {
                appliedDamage = health.ApplyDamageAndGetApplied(intendedDamage, point, gameObject, damageType);
            }
            else
            {
                // Health is the shared enemy implementation. Retain compatibility with any
                // future IDamageable implementation while treating its requested hit as the best
                // available actual-damage value.
                target.ApplyDamage(intendedDamage, point, gameObject, damageType);
                appliedDamage = intendedDamage;
            }

            ApplyVampireHealing(appliedDamage);
            return appliedDamage;
        }

        private void ApplyVampireHealing(float appliedDamage)
        {
            if (!_vampireEnabled || appliedDamage <= 0f) return;
            if (_playerHealth == null) _playerHealth = GetComponent<Health>();
            _playerHealth?.Heal(appliedDamage * vampireDamageFraction);
        }

        private sealed class PistolShotState
        {
            public readonly float ImpactDamage;
            public readonly bool Explosive;
            public readonly bool Headshot;
            public readonly int MaximumTargets;
            public readonly HashSet<IDamageable> VisitedTargets = new HashSet<IDamageable>();

            public bool CanBounce => VisitedTargets.Count < MaximumTargets;

            public PistolShotState(float impactDamage, int maximumTargets, bool explosive, bool headshot)
            {
                ImpactDamage = impactDamage;
                MaximumTargets = Mathf.Clamp(maximumTargets, 1, 3);
                Explosive = explosive;
                Headshot = headshot;
            }

            public bool RegisterTarget(IDamageable target) => target != null && VisitedTargets.Add(target);
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
                EffectiveElectricDamage, false, lifetime, enemyHitMask, tracerColor, projectileHitRadius,
                ProjectileVisualStyle.Bolt, visuals: visuals,
                onHit: hitGameObject =>
                {
                    var enemy = hitGameObject.GetComponentInParent<EnemyBase>();
                    enemy?.ApplySlow(slowMultiplier, electricSlowDuration);
                },
                onDamage: hit =>
                {
                    ApplyVampireHealing(hit.AppliedDamage);
                    if (hit.AppliedDamage > 0f)
                        Combat.DamageNumberSpawner.Spawn(hit.Point + Vector3.up, hit.AppliedDamage);
                });
        }

        private void OnSecondaryPerformed(InputAction.CallbackContext context)
        {
            if (playerController != null && playerController.IsStaggered) return;
            if (SecondaryCooldownRemaining > 0f) return;

            _secondaryCooldownEndsAt = Time.time + SecondaryCooldownDuration;
            SecondaryCooldownChanged?.Invoke();

            // SFX no longer fires on cast - moved to DamageIfStillNear so it only plays once the
            // hit actually lands on an enemy, not on every cast regardless of outcome.
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
            // skipFraction 0.99 - seeks the VFX's own particle timeline 99% of the way forward
            // before it ever renders, cutting the pack's authored "charging" portion without
            // hiding the beam-strike visual itself (see TopDownGroundEffect.FastForward).
            // The VFX itself spawns at the raycasted ground point below the target (so it doesn't
            // hang in midair under a flying enemy) - the damage check below still uses the
            // target's real position (`point`), unchanged, so hit detection isn't affected by this
            // purely visual placement fix.
            Vector3 vfxPoint = TopDownGroundEffect.GroundedPoint(point);
            StartCoroutine(TopDownGroundEffect.Play(topDownBeamDotPurplePrefab, vfxPoint,
                secondaryTelegraphDelay, 1f, () => DamageIfStillNear(target, point, secondaryHitRadius, EffectiveSecondaryDamage, SfxId.PlayerShootSecondary),
                skipFraction: 0.99f));
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

                float damage = EffectiveUltimateSecondaryDamage * Mathf.Max(0.2f, 1f - 0.2f * occurrence);
                Vector3 point = target.transform.position;
                // skipFraction 0.5 - same particle-timeline seek as FireSingleTopDownBeam above,
                // confirmed working well at the halfway point for this prefab. VFX grounded below
                // the target the same way - damage check keeps using the real target position.
                Vector3 vfxPoint = TopDownGroundEffect.GroundedPoint(point);
                StartCoroutine(TopDownGroundEffect.Play(lightningCirclePrefab, vfxPoint,
                    ultimateSecondaryTelegraphDelay, 1f, () => DamageIfStillNear(target, point, ultimateSecondaryHitRadius, damage, SfxId.MechShootSecondary),
                    skipFraction: 0.5f));
            }
        }

        private void DamageIfStillNear(EnemyBase target, Vector3 point, float radius, float damage, SfxId hitSfx)
        {
            if (target == null) return;
            var damageable = target.GetComponent<IDamageable>();
            if (damageable == null || damageable.IsDead) return;
            if (Vector3.Distance(target.transform.position, point) > radius) return;

            float appliedDamage = DealPlayerDamage(damageable, damage, point, DamageType.Ranged);
            AudioManager.Instance.PlaySfx(hitSfx, point);
            if (appliedDamage > 0f) Combat.DamageNumberSpawner.Spawn(point + Vector3.up, appliedDamage);
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
