using UnityEngine;

namespace Combat
{
    public interface IDamageable
    {
        bool IsDead { get; }

        void ApplyDamage(float amount, Vector3 hitPoint, GameObject instigator);
    }
}
