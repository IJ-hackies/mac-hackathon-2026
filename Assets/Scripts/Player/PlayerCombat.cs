using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    public class PlayerCombat : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private Transform muzzle;
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private Camera aimCamera;
        [SerializeField] private Collider ownCollider;

        [Header("Melee")]
        [SerializeField] private float meleeCooldown = 0.6f;

        [Header("Shooting")]
        [Tooltip("The Attack action is already bound to left click / gamepad west in " +
                 "InputSystem_Actions; reused here for firing rather than adding a new action.")]
        [SerializeField] private float fireCooldown = 0.35f;
        [SerializeField] private float projectileSpeed = 30f;
        [SerializeField] private float maxAimDistance = 100f;
        [SerializeField] private LayerMask aimMask = ~0;

        [Header("Muzzle Flash")]
        [SerializeField] private float muzzleFlashDuration = 0.05f;
        [SerializeField] private float muzzleFlashIntensity = 10f;
        [SerializeField] private float muzzleFlashRange = 1.5f;
        [SerializeField] private Color muzzleFlashColor = new Color(0.15f, 0.45f, 1f);

        private static readonly int MeleeParam = Animator.StringToHash("Melee");
        private static readonly int FireParam = Animator.StringToHash("Fire");

        private InputSystem_Actions _actions;
        private float _lastMeleeTime = -999f;
        private float _lastFireTime = -999f;
        private float _attackingUntil = -999f;

        public bool IsAttacking => Time.time < _attackingUntil;

        private void Awake()
        {
            _actions = new InputSystem_Actions();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (aimCamera == null) aimCamera = Camera.main;
            if (ownCollider == null) ownCollider = GetComponentInParent<Collider>();
        }

        private void OnEnable()
        {
            _actions.Player.Enable();
            _actions.Player.Melee.performed += OnMeleePerformed;
            _actions.Player.Attack.performed += OnFirePerformed;
        }

        private void OnDisable()
        {
            _actions.Player.Melee.performed -= OnMeleePerformed;
            _actions.Player.Attack.performed -= OnFirePerformed;
            _actions.Player.Disable();
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
        }

        private void OnFirePerformed(InputAction.CallbackContext context)
        {
            if (Time.time - _lastFireTime < fireCooldown) return;
            _lastFireTime = Time.time;
            _attackingUntil = Time.time + fireCooldown;

            // Always play Run_Gun_Shoot regardless of movement: the neutral Idle pose has the
            // arms hanging down (no gun mesh to read as "aiming"), which reads as broken when
            // firing. The pack has no dedicated idle-shoot clip, so the running pose is reused
            // for every shot rather than showing an unarmed-looking idle.
            if (animator != null)
            {
                animator.SetTrigger(FireParam);
            }

            SpawnProjectile();
            SpawnMuzzleFlash();
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

        private void SpawnProjectile()
        {
            if (projectilePrefab == null || muzzle == null) return;

            Vector3 aimPoint;
            if (aimCamera != null)
            {
                Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                aimPoint = Physics.Raycast(ray, out RaycastHit hit, maxAimDistance, aimMask, QueryTriggerInteraction.Ignore)
                    ? hit.point
                    : ray.origin + ray.direction * maxAimDistance;
            }
            else
            {
                aimPoint = muzzle.position + muzzle.forward * maxAimDistance;
            }

            Vector3 direction = aimPoint - muzzle.position;
            GameObject instance = Instantiate(projectilePrefab, muzzle.position, Quaternion.LookRotation(direction));

            if (instance.TryGetComponent(out Projectile projectile))
            {
                projectile.Launch(direction, projectileSpeed);
                projectile.IgnoreCollisionWith(ownCollider);
            }
        }
    }
}
