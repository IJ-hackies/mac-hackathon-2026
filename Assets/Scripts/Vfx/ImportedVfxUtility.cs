using UnityEngine;

namespace Vfx
{
    /// Shared helpers for dressing up runtime-instantiated imported particle-effect assets (the
    /// Creepy Cat effects pack, Gabriel Aguiar's Free Quick Effects Vol.1) safely. Used by both
    /// Player (PlayerCombat) and Enemies (BossProjectile/BossFightController) scripts.
    public static class ImportedVfxUtility
    {
        // Asset Store packs authored against the Built-in Render Pipeline render broken in this
        // URP project until their materials are converted - solid meshes go fully magenta, while
        // particle effects using a built-in particle shader (e.g. "Particles/Alpha Blended") lose
        // their alpha blending under a naively-assigned opaque URP/Lit and render as solid
        // rectangular cards instead of a transparent frame. Branches by renderer type: a
        // ParticleSystemRenderer/TrailRenderer/LineRenderer gets Sprites/Default (proven alpha-
        // blended elsewhere in this project - BossProjectile's own trail/tracer materials) -
        // TrailRenderer/LineRenderer are also Renderer subclasses, so a comet-tail trail on an
        // imported projectile was previously falling into the opaque-mesh branch below and
        // rendering as a solid black streak instead of a transparent one. Anything else (real mesh
        // renderers) gets an opaque URP/Lit carrying over the main texture. Always instance copies
        // (renderer.materials, not sharedMaterial), so the source asset itself is never modified
        // on disk. Packs already shipped as native URP assets (e.g. Free Quick Effects Vol.1's URP
        // build) simply won't match any of the "broken shader" conditions below and pass through
        // untouched.
        public static void FixUrpMaterials(GameObject root)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            Shader spriteShader = Shader.Find("Sprites/Default");

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                bool isParticle = renderer is ParticleSystemRenderer or TrailRenderer or LineRenderer;
                Shader targetShader = isParticle ? spriteShader : urpLit;
                if (targetShader == null) continue;

                var materials = renderer.materials;
                bool changed = false;

                for (int i = 0; i < materials.Length; i++)
                {
                    var mat = materials[i];
                    if (mat == null || mat.shader == null) continue;
                    if (mat.shader.name.StartsWith("Universal Render Pipeline")) continue;
                    if (mat.shader.name == "Sprites/Default") continue;
                    if (mat.shader.name.StartsWith("Shader Graphs/")) continue;
                    // Custom-but-already-URP-safe shaders (a plain ShaderLab alpha-blended shader
                    // that isn't Built-in-RP Standard/Particles/Legacy) don't hit the magenta-
                    // fallback problem this method exists to fix, and replacing them would only
                    // throw away their authored look.
                    if (mat.shader.name.StartsWith("HunFX/")) continue;

                    var replacement = new Material(targetShader) { name = mat.name + " (Fixed)" };
                    Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                    Color tint = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                    if (isParticle)
                    {
                        if (mainTex != null) replacement.SetTexture("_MainTex", mainTex);
                        replacement.SetColor("_Color", tint);
                    }
                    else
                    {
                        if (mainTex != null) replacement.SetTexture("_BaseMap", mainTex);
                        replacement.SetColor("_BaseColor", tint);
                    }

                    materials[i] = replacement;
                    changed = true;
                }

                if (changed) renderer.materials = materials;
            }
        }

        // Many effect packs set a ParticleSystem's Scaling Mode to "Shape" or "Local" - meaning
        // particle SIZE ignores the transform hierarchy's scale entirely, only the emission shape
        // follows it. Forcing every ParticleSystem under the instance to Hierarchy scaling mode
        // makes particle size respect the parent scale like everything else, so a uniform
        // transform.localScale on the instance actually shrinks/grows the whole effect.
        public static void ForceHierarchyParticleScaling(GameObject root)
        {
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
        }
    }
}
