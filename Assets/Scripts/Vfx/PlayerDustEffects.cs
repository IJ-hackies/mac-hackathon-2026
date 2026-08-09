using Player;
using UnityEngine;

namespace Vfx
{
    /// <summary>Procedural dust puffs at the astronaut's feet: a light continuous trail while
    /// walking/running on the ground, and one-shot bursts on jump takeoff and landing. Uses a
    /// single ParticleSystem (continuous emission for the walk trail, Emit() bursts for
    /// jump/land) with a runtime-generated soft circular sprite, matching the planet's warm
    /// clay/ochre surface color - no imported dust texture/prefab dependency. Add anywhere on the
    /// player rig; it resolves PlayerController itself.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerDustEffects : MonoBehaviour
    {
        [SerializeField] private PlayerController playerController;
        [Tooltip("Fraction of moveSpeed above which walking dust starts emitting.")]
        [SerializeField, Range(0f, 1f)] private float walkSpeedThreshold = 0.15f;
        [SerializeField] private Color dustColor = new Color(0.72f, 0.56f, 0.36f, 0.55f);

        private ParticleSystem _particles;
        private ParticleSystem.EmissionModule _emission;
        private bool _wasGrounded;

        private void Awake()
        {
            if (playerController == null) playerController = GetComponentInParent<PlayerController>();
            BuildParticleSystem();
        }

        private void LateUpdate()
        {
            if (playerController == null || _particles == null) return;

            transform.position = playerController.transform.position;
            transform.up = playerController.transform.up;

            bool grounded = playerController.IsGrounded;
            bool moving = playerController.CurrentHorizontalSpeed > playerController.EffectiveMoveSpeed * walkSpeedThreshold;

            _emission.rateOverTime = grounded && moving ? 14f : 0f;

            if (grounded && !_wasGrounded)
            {
                EmitBurst(10);
            }

            if (playerController.JumpTriggeredThisFrame)
            {
                EmitBurst(6);
            }

            _wasGrounded = grounded;
        }

        private void EmitBurst(int count)
        {
            var emitParams = new ParticleSystem.EmitParams
            {
                position = transform.position,
                applyShapeToPosition = true
            };
            _particles.Emit(emitParams, count);
        }

        private void BuildParticleSystem()
        {
            var go = new GameObject("DustParticles", typeof(ParticleSystem));
            go.transform.SetParent(transform, false);
            _particles = go.GetComponent<ParticleSystem>();

            var main = _particles.main;
            main.loop = true;
            main.startLifetime = 0.6f;
            main.startSpeed = 0.6f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            main.startColor = dustColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            main.maxParticles = 200;

            _emission = _particles.emission;
            _emission.rateOverTime = 0f;

            var shape = _particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.35f;
            shape.rotation = new Vector3(90f, 0f, 0f);

            var colorOverLifetime = _particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(dustColor, 0f), new GradientColorKey(dustColor, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(dustColor.a, 0.15f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = _particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.4f));

            var renderer = _particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = BuildDustMaterial();
        }

        // Sprites/Default - the same shader ImportedVfxUtility.FixUrpMaterials assigns to
        // ParticleSystemRenderers, proven alpha-blended in this URP project (BossProjectile's
        // trail/tracer materials) rather than hand-configuring a URP transparent material's
        // surface-type keywords, which SetFloat alone does not correctly set up.
        private static Material BuildDustMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default");
            var material = new Material(shader);
            Texture2D texture = BuildSoftCircleTexture(32);
            material.SetTexture("_MainTex", texture);
            material.SetColor("_Color", Color.white);
            return material;
        }

        private static Texture2D BuildSoftCircleTexture(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float radius = size * 0.5f;
            Vector2 center = new Vector2(radius, radius);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    float alpha = Mathf.Clamp01(1f - distance / radius);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
                }
            }
            texture.Apply(false, false);
            return texture;
        }
    }
}
