using System;
using UnityEngine;

namespace Combat
{
    /// Shared by the player and every enemy type. HitReact/Death are fired as Animator
    /// triggers only (AnyState transitions, same pattern as PlayerCombat's Melee/Fire) -
    /// this component never blocks its own caller, so movement/AI/input logic keeps running
    /// underneath a hit reaction instead of stalling on it.
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private Animator animator;

        private static readonly int HitReactParam = Animator.StringToHash("HitReact");
        private static readonly int DeathParam = Animator.StringToHash("Death");

        private float _currentHealth = -1f;

        public float MaxHealth => maxHealth;
        // Falls back to maxHealth until Awake actually runs. Editor setup scripts
        // (PlayerSceneSetup/EnemySceneSetup) call Bind() on the HUD/health-bar right after
        // AddComponent<Health>(), at edit time - Awake never fires outside Play mode, so
        // without this fallback CurrentHealth read as the C# default 0 at bind time (bars
        // showing 0%/invisible from the start instead of full).
        public float CurrentHealth => _currentHealth < 0f ? maxHealth : _currentHealth;
        public bool IsDead { get; private set; }

        public event Action<float, float> HealthChanged;
        public event Action Died;

        private void Awake()
        {
            if (_currentHealth < 0f) _currentHealth = maxHealth;
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        public void ApplyDamage(float amount, Vector3 hitPoint, GameObject instigator)
        {
            if (IsDead || amount <= 0f) return;
            if (_currentHealth < 0f) _currentHealth = maxHealth;

            _currentHealth = Mathf.Max(0f, _currentHealth - amount);
            HealthChanged?.Invoke(_currentHealth, maxHealth);

            if (_currentHealth <= 0f)
            {
                IsDead = true;
                if (animator != null) animator.SetTrigger(DeathParam);
                Died?.Invoke();
                return;
            }

            if (animator != null) animator.SetTrigger(HitReactParam);
        }
    }
}
