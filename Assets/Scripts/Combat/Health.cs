using System;
using System.Collections.Generic;
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
        // Incoming mitigation is composable. The Ultimate shield and purchased defense must not
        // overwrite one another: a shield can still reduce the final result to zero while the
        // defense upgrade remains in effect before/after it ends.
        private static readonly object LegacyIncomingDamageSource = new object();
        private readonly Dictionary<object, float> _incomingDamageModifiers =
            new Dictionary<object, float>();

        public float IncomingDamageMultiplier
        {
            get => EffectiveIncomingDamageMultiplier;
            set => SetIncomingDamageModifier(LegacyIncomingDamageSource, value);
        }

        public float EffectiveIncomingDamageMultiplier { get; private set; } = 1f;

        public event Action<float, float> HealthChanged;
        public event Action Died;
        // Fired alongside the automatic HitReact trigger below, carrying which kind of attack
        // landed. Existing listeners (player, the three basic enemies) don't need this - Health
        // still fires the plain HitReact trigger itself for them, unchanged. BossMechAI is the
        // first subscriber: it wants HitRecieve_1 (ranged) vs HitRecieve_2 (melee) instead of a
        // single generic reaction, and this event lets it pick without Health knowing about
        // per-enemy animator param names.
        public event Action<DamageType> Hit;
        // Fired when an incoming-damage modifier (e.g. Player.PlayerShield's full block) reduces
        // an otherwise-real hit to zero, carrying the amount that would have landed. Distinct
        // from Hit, which never fires for a fully-blocked amount at all - this is the hook for
        // "you blocked N damage" feedback (see Player.PlayerShield) without Health needing to
        // know anything about shields, UI, or who's listening.
        public event Action<float> MitigatedDamage;

        private void Awake()
        {
            if (_currentHealth < 0f) _currentHealth = maxHealth;
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        // GetComponentInChildren only ever finds the model that was active at Awake, which for
        // the player is permanently the astronaut - Player.PlayerUltimate calls this on activate/
        // end so a mid-Ultimate death plays the mech's own Death clip instead.
        public void SetAnimator(Animator target)
        {
            animator = target;
        }

        public void ApplyDamage(float amount, Vector3 hitPoint, GameObject instigator, DamageType damageType = DamageType.Generic)
        {
            if (IsDead || amount <= 0f) return;

            float originalAmount = amount;
            amount *= EffectiveIncomingDamageMultiplier;
            if (amount <= 0f)
            {
                MitigatedDamage?.Invoke(originalAmount);
                return;
            }

            if (_currentHealth < 0f) _currentHealth = maxHealth;

            _currentHealth = Mathf.Max(0f, _currentHealth - amount);
            HealthChanged?.Invoke(_currentHealth, maxHealth);

            if (_currentHealth <= 0f)
            {
                IsDead = true;
                if (animator != null) animator.SetTrigger(DeathParam);

                // Stops any looping SFX this entity currently owns (e.g. BossMechAI's sustained
                // machine-gun loop) unconditionally, regardless of whether the coroutine that
                // started it gets to run its own cleanup - dying via base.HandleDeath()'s
                // StopAllCoroutines() abandons that coroutine mid-loop without ever reaching its
                // StopLoop call, which is exactly what left loops playing forever after death.
                Audio.AudioManager.Instance.StopAllLoopsFor(gameObject);

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

        /// Restores health (e.g. a pickup). No-ops once dead - death isn't reversible here.
        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            if (_currentHealth < 0f) _currentHealth = maxHealth;

            _currentHealth = Mathf.Min(maxHealth, _currentHealth + amount);
            HealthChanged?.Invoke(_currentHealth, maxHealth);
        }

        public void FullyHeal()
        {
            Heal(maxHealth);
        }

        /// <summary>
        /// Updates the health ceiling. When <paramref name="addToCurrentHealth"/> is true the
        /// exact increase is granted immediately, which makes a Max HP purchase useful at once.
        /// </summary>
        public void SetMaxHealth(float value, bool addToCurrentHealth = false)
        {
            value = Mathf.Max(0f, value);
            if (_currentHealth < 0f) _currentHealth = maxHealth;

            float previous = maxHealth;
            maxHealth = value;
            if (addToCurrentHealth && value > previous)
            {
                _currentHealth = Mathf.Min(maxHealth, _currentHealth + (value - previous));
            }
            else
            {
                _currentHealth = Mathf.Min(_currentHealth, maxHealth);
            }

            HealthChanged?.Invoke(_currentHealth, maxHealth);
        }

        public void SetIncomingDamageModifier(object source, float multiplier)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(multiplier), multiplier,
                    "Incoming damage multipliers must be finite and non-negative.");
            }

            _incomingDamageModifiers[source] = multiplier;
            RecalculateIncomingDamageMultiplier();
        }

        public void RemoveIncomingDamageModifier(object source)
        {
            if (source != null && _incomingDamageModifiers.Remove(source))
            {
                RecalculateIncomingDamageMultiplier();
            }
        }

        private void RecalculateIncomingDamageMultiplier()
        {
            float result = 1f;
            foreach (float modifier in _incomingDamageModifiers.Values)
            {
                result *= modifier;
            }

            EffectiveIncomingDamageMultiplier = result;
        }
    }
}
