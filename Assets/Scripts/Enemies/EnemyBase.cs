using System.Collections;
using System.Collections.Generic;
using Combat;
using UnityEngine;

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

        private static readonly int DissolveAmountId = Shader.PropertyToID("_DissolveAmount");
        private static Shader _dissolveShader;

        protected Health health;
        protected Transform player;
        protected Health playerHealth;
        protected bool isDead;

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
        }

        protected virtual void OnDisable()
        {
            health.Died -= HandleDeath;
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
    }
}
