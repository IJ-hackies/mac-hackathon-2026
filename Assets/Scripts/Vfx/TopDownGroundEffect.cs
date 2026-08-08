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
        ///
        /// hideChargeVisual additionally disables the pack's own shot_controller child (the
        /// "charging" half of the prefab, active from frame 0 independently of telegraphDelay) so
        /// only hit_controller (the actual impact) ever plays - for attacks that should read as an
        /// instant strike with no wind-up telegraph at all, not just a zero-length wait.
        ///
        /// skipFraction instead seeks every ParticleSystem on the instantiated VFX forward by that
        /// fraction of its own duration (0-1) before the first frame ever renders - not hiding any
        /// part of the prefab, just starting mid-timeline so whatever the pack authored as its
        /// early "build-up" portion is skipped and playback opens already further into the effect.
        public static IEnumerator Play(GameObject prefab, Vector3 point, float telegraphDelay,
            float lingerAfterHit, Action onImpact, bool hideChargeVisual = false, float skipFraction = 0f)
        {
            if (prefab == null)
            {
                if (telegraphDelay > 0f && !hideChargeVisual) yield return new WaitForSeconds(telegraphDelay);
                onImpact?.Invoke();
                yield break;
            }

            var instance = UnityEngine.Object.Instantiate(prefab, point, Quaternion.identity);
            ImportedVfxUtility.FixUrpMaterials(instance);
            ImportedVfxUtility.ForceHierarchyParticleScaling(instance);

            if (skipFraction > 0f)
            {
                FastForward(instance, Mathf.Clamp01(skipFraction));
            }

            Transform hitController = FindChildByName(instance.transform, "hit_controller");

            if (hideChargeVisual)
            {
                Transform shotController = FindChildByName(instance.transform, "shot_controller");
                if (shotController != null) shotController.gameObject.SetActive(false);
            }

            if (telegraphDelay > 0f && !hideChargeVisual)
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

        // Advances every particle system on the instance to `fraction` of its own duration
        // (Simulate with restart:true resets to t=0 first, then advances to exactly `time`) and
        // resumes normal playback from there - skips whatever the pack authored as the first
        // portion of each system's timeline (a slow charge build-up, typically) without touching
        // any GameObject's active state.
        private static void FastForward(GameObject instance, float fraction)
        {
            foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                float duration = ps.main.duration;
                if (duration <= 0f) continue;

                ps.Simulate(duration * fraction, withChildren: false, restart: true, fixedTimeStep: true);
                ps.Play(withChildren: false);
            }
        }

        // Excludes Player/Enemy so the downward raycast below doesn't just hit the target's own
        // collider (e.g. its head, being directly under the raycast origin) instead of the actual
        // ground - that was landing the "grounded" VFX point on top of whatever character it was
        // cast at rather than passing through them to the floor. Lazily resolved (LayerMask.GetMask
        // needs the layers to exist at call time) and cached since layer indices don't change at
        // runtime.
        private static int? _groundRaycastMask;
        private static int GroundRaycastMask =>
            _groundRaycastMask ??= ~LayerMask.GetMask("Player", "Enemy");

        // Projects a target position straight down onto whatever's actually beneath it, for VFX
        // placement only - a flying enemy (or a jumping player) has its body well above the floor,
        // and spawning the ground-impact VFX at that height left it floating midair instead of
        // hitting the ground below. Falls back to the original point unchanged if nothing's hit
        // (e.g. no ground within range), rather than guessing.
        public static Vector3 GroundedPoint(Vector3 target, float raycastHeight = 6f, float maxDistance = 40f)
        {
            Vector3 origin = target + Vector3.up * raycastHeight;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxDistance, GroundRaycastMask, QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }
            return target;
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
