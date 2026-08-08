using System.Collections;
using System.Collections.Generic;
using Audio;
using Combat;
using UnityEngine;
using Vfx;

namespace Enemies
{
    [RequireComponent(typeof(Health))]
    public abstract class EnemyBase : MonoBehaviour
    {
        [Header("Enemy Base")]
        [SerializeField] protected Animator animator;
        [SerializeField] private float faceRotationDegreesPerSecond = 220f;

        [Header("Death")]
        [Tooltip("How long the Death animation gets to play, undisturbed, before the dissolve " +
                 "starts eating the model away.")]
        [SerializeField] private float deathAnimationHold = 1.2f;
        [SerializeField] private float dissolveDuration = 1f;
        [SerializeField] private float deathFallGravity = 20f;

        [Header("Melee Hit VFX")]
        [Tooltip("Imported slash effect (e.g. Lana Studio's Slash_stone_once) played on the " +
                 "player where this enemy's melee attack connects.")]
        [SerializeField] private GameObject meleeHitVfxPrefab;
        [SerializeField] private float meleeHitVfxScale = 1f;

        [Header("Slow Debuff (e.g. Ultimate electric bolts)")]
        [Tooltip("Looping fog VFX (e.g. Lana Studio's Fog/Fog_electric) worn for the duration " +
                 "of an active slow, so the debuff is visible, not just a numeric multiplier.")]
        [SerializeField] private GameObject slowVfxPrefab;
        [SerializeField] private float slowVfxScale = 1f;

        private static readonly int DissolveAmountId = Shader.PropertyToID("_DissolveAmount");
        private static Shader _dissolveShader;

        protected Health health;
        protected Transform player;
        protected Health playerHealth;
        protected bool isDead;
        // Gates AI movement/attacks the same way isDead already does (subclasses add
        // `|| isFrozen` next to their existing `if (isDead) return;` in Update) - used by
        // BossFightController to guarantee no basic enemy can attack the player during the
        // Stage 1 -> Stage 2 cutscene, regardless of whether it happens to be active in the scene.
        protected bool isFrozen;

        // 1 = no slow. AI movement scripts (EnemyFlyingAI/EnemySmallAI/EnemyLargeAI) multiply
        // their own speed fields by this at their existing move call sites - simpler than a full
        // keyed-modifier dictionary (PlayerController's SetMovementSpeedModifier) since there's
        // only one debuff source today (the Ultimate's electric bolts).
        public float SpeedMultiplier { get; private set; } = 1f;
        private Coroutine _slowRoutine;
        private GameObject _slowVfxInstance;

        protected virtual void Awake()
        {
            health = GetComponent<Health>();
            if (animator == null) animator = GetComponentInChildren<Animator>();

            var playerController = FindFirstObjectByType<Player.PlayerController>();
            if (playerController != null)
            {
                player = playerController.transform;
                playerHealth = playerController.GetComponent<Health>();
            }
        }

        protected virtual void OnEnable()
        {
            health.Died += HandleDeath;
            health.Hit += HandleHit;
        }

        protected virtual void OnDisable()
        {
            health.Died -= HandleDeath;
            health.Hit -= HandleHit;
        }

        // BossAstronautAI/BossMechAI override this to play their own Boss1HitReact/MechHitReact
        // cue instead - this base implementation covers the three basic enemy types.
        protected virtual void HandleHit(DamageType damageType)
        {
            AudioManager.Instance.PlaySfx(SfxId.EnemyHitReact, transform.position);
        }

        // Cancels any in-flight attack coroutine outright (not paused/resumed) rather than
        // leaving it to finish later - StopAllCoroutines() kills it mid-flight, which would leave
        // a subclass's own private "_isAttacking" flag stuck true forever (nothing left to ever
        // clear it) unless that subclass resets it here via the OnFrozen override below.
        public void SetFrozen(bool frozen)
        {
            if (isDead) return;

            isFrozen = frozen;
            if (frozen)
            {
                StopAllCoroutines();
                OnFrozen();
            }
        }

        /// Subclasses with their own private "is currently mid-attack" flag (EnemyFlyingAI/
        /// EnemySmallAI/EnemyLargeAI) override this to reset it - see SetFrozen's comment.
        protected virtual void OnFrozen()
        {
        }

        // Movement/attack scripts check isDead themselves rather than being disabled outright,
        // so the Death animator trigger (already fired by Health) plays out undisturbed instead
        // of being interrupted by this component going away mid-transition. StopAllCoroutines
        // matters here too: a subclass's in-flight attack coroutine (e.g. EnemyFlyingAI's
        // multi-second Headbutt telegraph) doesn't check isDead mid-sequence, so dying during an
        // attack could otherwise leave that coroutine still running/animating over the top of
        // the Death state - killing it here is what guarantees Death is the only thing playing
        // from this point on.
        protected virtual void HandleDeath()
        {
            isDead = true;
            AudioManager.Instance.PlaySfx(SfxId.EnemyDeath, transform.position);
            StopAllCoroutines();
            StartCoroutine(DissolveAndDestroy());
        }

        // True per-pixel dissolve (Custom/EnemyDissolve, Assets/Art/Shaders/S_EnemyDissolve.shader):
        // clips pixels away against 3D noise as _DissolveAmount rises 0->1, with a glowing edge -
        // a "Thanos snap" crumble rather than a uniform scale-down. Swaps each renderer onto a
        // per-instance material clone (copying the original's texture/color) instead of touching
        // the shared M_Enemy*.mat asset, so other live instances of the same enemy type are
        // unaffected. deathAnimationHold runs first so the Death animation always gets
        // uninterrupted screen time before the dissolve starts.
        private IEnumerator DissolveAndDestroy()
        {
            yield return FallToGround();

            if (_dissolveShader == null) _dissolveShader = Shader.Find("Custom/EnemyDissolve");

            var dissolveMaterials = new List<Material>();
            if (_dissolveShader != null)
            {
                foreach (var meshRenderer in GetComponentsInChildren<Renderer>())
                {
                    var source = meshRenderer.sharedMaterial;
                    var dissolveMaterial = new Material(_dissolveShader);
                    if (source != null)
                    {
                        if (source.HasProperty("_BaseMap")) dissolveMaterial.SetTexture("_BaseMap", source.GetTexture("_BaseMap"));
                        if (source.HasProperty("_BaseColor")) dissolveMaterial.SetColor("_BaseColor", source.GetColor("_BaseColor"));
                    }
                    meshRenderer.material = dissolveMaterial;
                    dissolveMaterials.Add(dissolveMaterial);
                }
            }

            float elapsed = 0f;
            while (elapsed < dissolveDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dissolveDuration);
                foreach (var dissolveMaterial in dissolveMaterials)
                {
                    dissolveMaterial.SetFloat(DissolveAmountId, t);
                }
                yield return null;
            }

            Destroy(gameObject);
        }

        // Flyers (EnemyFlyingAI/EnemySmallAI) freeze mid-air at whatever hover height they died
        // at, since their Update loops stop as soon as isDead is set - without this they'd
        // dissolve while still floating. Runs concurrently with the Death animation (gravity
        // fall, not a snap), and keeps going past deathAnimationHold if it hasn't landed yet, so
        // the dissolve never starts before the model is actually on the ground. Grounded enemies
        // (EnemyLargeAI) are already at y=0 and just wait out deathAnimationHold unaffected.
        private IEnumerator FallToGround()
        {
            float verticalVelocity = 0f;
            float elapsed = 0f;

            while (elapsed < deathAnimationHold || transform.position.y > 0f)
            {
                elapsed += Time.deltaTime;

                if (transform.position.y > 0f)
                {
                    verticalVelocity += deathFallGravity * Time.deltaTime;
                    Vector3 pos = transform.position;
                    pos.y = Mathf.Max(0f, pos.y - verticalVelocity * Time.deltaTime);
                    transform.position = pos;
                }

                yield return null;
            }

            Vector3 grounded = transform.position;
            grounded.y = 0f;
            transform.position = grounded;
        }

        protected void FacePlayer()
        {
            if (player == null) return;

            Vector3 toPlayer = player.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < 0.0001f) return;

            Quaternion targetRotation = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRotation, faceRotationDegreesPerSecond * Time.deltaTime);
        }

        protected float DistanceToPlayer()
        {
            return player == null ? Mathf.Infinity : Vector3.Distance(transform.position, player.position);
        }

        // Shared by every melee-capable subclass (EnemySmallAI/EnemyLargeAI) so the imported-
        // prefab instantiate/fix/destroy boilerplate isn't duplicated in each attack routine.
        // point should be an upward-offset hit point (e.g. player.position + Vector3.up), not the
        // bare feet-level position ApplyDamage is called with, so the effect reads as landing on
        // the body.
        protected void SpawnMeleeHitVfx(Vector3 point)
        {
            if (meleeHitVfxPrefab == null) return;

            var effect = Instantiate(meleeHitVfxPrefab, point, Quaternion.identity);
            effect.transform.localScale = Vector3.one * meleeHitVfxScale;
            ImportedVfxUtility.FixUrpMaterials(effect);
            ImportedVfxUtility.ForceHierarchyParticleScaling(effect);
            Destroy(effect, 2f);
        }

        // Refreshes duration on reapply rather than stacking - a sustained burst of slowing
        // shots should just keep the debuff topped up, not compound it below the intended floor.
        public void ApplySlow(float multiplier, float duration)
        {
            if (isDead) return;

            SpeedMultiplier = Mathf.Clamp01(multiplier);
            if (_slowRoutine != null) StopCoroutine(_slowRoutine);
            _slowRoutine = StartCoroutine(SlowRoutine(duration));

            if (_slowVfxInstance == null && slowVfxPrefab != null)
            {
                _slowVfxInstance = Instantiate(slowVfxPrefab, transform);
                _slowVfxInstance.transform.localPosition = Vector3.zero;
                _slowVfxInstance.transform.localScale = Vector3.one * slowVfxScale;
                ImportedVfxUtility.FixUrpMaterials(_slowVfxInstance);
                ImportedVfxUtility.ForceHierarchyParticleScaling(_slowVfxInstance);
            }
        }

        private IEnumerator SlowRoutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            SpeedMultiplier = 1f;
            _slowRoutine = null;
            if (_slowVfxInstance != null)
            {
                Destroy(_slowVfxInstance);
                _slowVfxInstance = null;
            }
        }
    }
}
