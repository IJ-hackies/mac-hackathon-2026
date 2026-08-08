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
        [Tooltip("Minimum real time between automatic HitReact/Hit firings. Sustained fire " +
                 "(rapid melee, hitscan spam, projectile bursts) would otherwise re-trigger the " +
                 "reaction on literally every hit - since that AnyState transition has no exit-time " +
                 "gate by design (see AC_Player's own HitReact), the target could visibly never " +
                 "finish any other animation. A short cooldown keeps hits feeling responsive " +
                 "without permanently locking the target into flinch poses.")]
        [SerializeField] private float hitReactCooldown = 0.35f;

        private static readonly int HitReactParam = Animator.StringToHash("HitReact");
        private static readonly int DeathParam = Animator.StringToHash("Death");

        private float _currentHealth = -1f;
        private float _lastHitReactTime = -999f;

        public float MaxHealth => maxHealth;
        // Falls back to maxHealth until Awake actually runs. Editor setup scripts
        // (PlayerSceneSetup/EnemySceneSetup) call Bind() on the HUD/health-bar right after
        // AddComponent<Health>(), at edit time - Awake never fires outside Play mode, so
        // without this fallback CurrentHealth read as the C# default 0 at bind time (bars
        // showing 0%/invisible from the start instead of full).
        public float CurrentHealth => _currentHealth < 0f ? maxHealth : _currentHealth;
        public bool IsDead { get; private set; }
        // Lets an AI script (BossAstronautAI) skip the automatic HitReact trigger while it's mid-
        // attack, without Health needing to know why. Without this, sustained player fire re-fires
        // HitReact on literally every hit, and since that transition preempts whatever full-body
        // state is currently playing (no exit-time gate, by design - see AddOneShot/AC_Player's
        // own HitReact), a boss taking continuous damage could never visibly finish an attack
        // animation, reading as "the enemy can't attack at all." Hit still fires either way -
        // this only gates the automatic Animator trigger below.
        public bool SuppressHitReact { get; set; }

        public event Action<float, float> HealthChanged;
        public event Action Died;
        // Fired alongside the automatic HitReact trigger below, carrying which kind of attack
        // landed. Existing listeners (player, the three basic enemies) don't need this - Health
        // still fires the plain HitReact trigger itself for them, unchanged. BossMechAI is the
        // first subscriber: it wants HitRecieve_1 (ranged) vs HitRecieve_2 (melee) instead of a
        // single generic reaction, and this event lets it pick without Health knowing about
        // per-enemy animator param names.
        public event Action<DamageType> Hit;

        private void Awake()
        {
            if (_currentHealth < 0f) _currentHealth = maxHealth;
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        public void ApplyDamage(float amount, Vector3 hitPoint, GameObject instigator, DamageType damageType = DamageType.Generic)
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

            if (Time.time - _lastHitReactTime >= hitReactCooldown)
            {
                _lastHitReactTime = Time.time;
                Hit?.Invoke(damageType);
                if (animator != null && !SuppressHitReact) animator.SetTrigger(HitReactParam);
            }
        }
    }
}
