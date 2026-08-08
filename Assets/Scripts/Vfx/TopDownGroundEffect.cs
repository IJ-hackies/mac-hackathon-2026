using System;
using System.Collections;
using UnityEngine;

namespace Vfx
{
    /// Shared telegraph-then-impact sequencing for the Lana Studio "Top_down_attack" pack, which
    /// splits every prefab into a shot_controller (ground telegraph) and hit_controller (the
    /// actual damage-dealing visual) child, with no delay baked in between them (both default
    /// active, startDelay 0). Originally built for BossMechAI's TopDownBeam/TopDownRocket attacks;
    /// extracted here once the player's base/ultimate secondary attacks needed the identical
    /// sequence, so there's one place that owns "disable hit_controller, wait, re-enable it".
    public static class TopDownGroundEffect
    {
        /// Instantiates prefab at point, hides its hit_controller child for telegraphDelay while
        /// the pack's own shot_controller telegraph plays, then reveals hit_controller and invokes
        /// onImpact (the caller's damage-application closure) at that exact moment. Cleans itself
        /// up after the longest particle system on the instance finishes, plus lingerAfterHit.
        /// telegraphDelay &lt;= 0 skips the wait entirely (no yield at all) rather than yielding on
        /// a zero-length WaitForSeconds, which would still push onImpact to the next frame - a
        /// coroutine's body runs synchronously up to its first yield, so a delay-free call
        /// finishes VFX + damage in the same frame/call as StartCoroutine, i.e. genuinely instant.
        public static IEnumerator Play(GameObject prefab, Vector3 point, float telegraphDelay,
            float lingerAfterHit, Action onImpact)
        {
            if (prefab == null)
            {
                if (telegraphDelay > 0f) yield return new WaitForSeconds(telegraphDelay);
                onImpact?.Invoke();
                yield break;
            }

            var instance = UnityEngine.Object.Instantiate(prefab, point, Quaternion.identity);
            ImportedVfxUtility.FixUrpMaterials(instance);
            ImportedVfxUtility.ForceHierarchyParticleScaling(instance);

            Transform hitController = FindChildByName(instance.transform, "hit_controller");

            if (telegraphDelay > 0f)
            {
                if (hitController != null) hitController.gameObject.SetActive(false);
                yield return new WaitForSeconds(telegraphDelay);
                if (instance == null) yield break; // destroyed externally (e.g. caster died) mid-wait
            }

            if (hitController != null) hitController.gameObject.SetActive(true);
            onImpact?.Invoke();

            float maxLifetime = 0.1f;
            foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>())
            {
                maxLifetime = Mathf.Max(maxLifetime, ps.main.duration + ps.main.startLifetime.constantMax);
            }
            UnityEngine.Object.Destroy(instance, maxLifetime + lingerAfterHit);
        }

        public static Transform FindChildByName(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name) return child;
            }
            return null;
        }
    }
}
