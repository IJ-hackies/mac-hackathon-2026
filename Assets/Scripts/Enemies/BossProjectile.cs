using System;
using Combat;
using UnityEngine;
using Vfx;

namespace Enemies
{
    /// Which procedural look Create() builds when no importedVisualPrefab is supplied. All still
    /// built-in primitives/Shuriken particles - no imported VFX asset - but shaped/dressed
    /// differently per role so a bolt doesn't read as "the same glowing ball as everything else."
    public enum ProjectileVisualStyle
    {
        Bolt,
        Bullet,
        BigProjectile,
        Fireball,
    }

    /// Optional imported-asset dressing for a projectile - everything here is nullable/optional so
    /// existing procedural-only callers keep working unchanged (default struct = "use the built-in
    /// primitive look"). Passed through from BossMechAI/BossAstronautAI, which get their prefab
    /// references wired at edit time by BossSceneSetup (AssetDatabase only works in the Editor, so
    /// these runtime scripts can't look the assets up themselves). Every imported prefab here is
    /// expected to be a self-contained particle effect (e.g. Gabriel Aguiar's Free Quick Effects
    /// Vol.1 - vfx_Projectile_XX, vfx_Flames_XX) rather than an oriented 3D mesh, so there's no
    /// axis-fitting step - just a uniform scale and, for radially-symmetric effects, no rotation
    /// concern at all.
    public struct BossProjectileVisuals
    {
        /// Replaces the procedural mesh entirely - our own Light/TrailRenderer are skipped when
        /// this is set, so the imported effect's own visuals aren't fought/doubled-up.
        public GameObject ImportedVisualPrefab;
        /// Uniform scale for the imported prefab - 0/unset leaves it at its own authored scale.
        public float ImportedVisualScale;
        /// Applied on top of plain travel-direction facing - fine-tune by eye in Play mode if an
        /// asymmetric effect (e.g. a directional projectile trail) looks backwards.
        public Quaternion ExtraRotationOffset;
        /// When true, every ParticleSystem on the imported prefab is forced into loop mode after
        /// instantiation, for prefabs authored as a one-shot that should instead persist for the
        /// whole flight/conjure window.
        public bool ForceLoopParticles;
        /// Spawned (unparented, at the hit point) the instant this projectile connects - e.g. the
        /// effects pack's explosion burst. Null = no impact effect.
        public GameObject ImpactEffectPrefab;
        public float ImpactEffectScale;

        public static BossProjectileVisuals None => new BossProjectileVisuals { ImpactEffectScale = 1f };
    }

    /// <summary>Details of a projectile hit after the target has resolved mitigation and overkill.</summary>
    public readonly struct ProjectileHit
    {
        public readonly GameObject HitObject;
        public readonly IDamageable Damageable;
        public readonly Vector3 Point;
        public readonly float AppliedDamage;

        public ProjectileHit(GameObject hitObject, IDamageable damageable, Vector3 point, float appliedDamage)
        {
            HitObject = hitObject;
            Damageable = damageable;
            Point = point;
            AppliedDamage = appliedDamage;
        }
    }

    /// Generic travelling projectile shared by BossMechAI's fireballs (homing, spawned during
    /// Dance), bullets (fast, straight, Shoot_Small), missiles (slow, straight, high-damage,
    /// Shoot_Big) and BossAstronautAI's ranged bolts - one component/factory, several
    /// speed/damage/homing/visual configs, rather than near-duplicate spawn code in every AI
    /// script. Non-homing still needs a live Transform to aim at spawn time only, homing keeps
    /// steering toward it every frame (turn-rate-limited, not an instant snap-to).
    [RequireComponent(typeof(SphereCollider))]
    public class BossProjectile : MonoBehaviour
    {
        [SerializeField] private float speed = 10f;
        [SerializeField] private float damage = 10f;
        [SerializeField] private float lifetime = 6f;
        [SerializeField] private bool homing;
        [SerializeField] private float turnDegreesPerSecond = 90f;
        [Tooltip("Homing only steers while within this distance of the target - beyond it the " +
                 "projectile just flies straight, so putting real distance between yourself and " +
                 "it is a valid way to dodge rather than it being an inescapable heat-seeker.")]
        [SerializeField] private float homingRange = 10f;
        [SerializeField] private LayerMask hitMask = ~0;

        private Transform _target;
        private Vector3 _direction;
        private float _spawnTime;
        private GameObject _impactEffectPrefab;
        private float _impactEffectScale = 1f;
        private Action<GameObject> _onHit;
        private Action<ProjectileHit> _onDamage;

        /// Builds a fully-dressed projectile and launches it in one call. Centralizing this here
        /// means every spawner (BossMechAI's bullets/missiles/fireballs, BossAstronautAI's bolts)
        /// automatically gets the same visual quality bar instead of each hand-rolling its own
        /// primitive, and each ProjectileVisualStyle gets a distinct shape/particle treatment
        /// instead of every projectile type looking like the same glowing sphere. Pass a non-
        /// default BossProjectileVisuals to swap in an imported model/prefab (e.g. the Missile
        /// pack) instead of the procedural shape, and/or an impact-effect prefab.
        public static BossProjectile Create(Vector3 origin, Vector3 initialDirection, Transform target,
            float projectileSpeed, float projectileDamage, bool isHoming, float lifetimeSeconds,
            LayerMask targetMask, Color color, float visualRadius, ProjectileVisualStyle style,
            float homingTurnDegreesPerSecond = 90f, float homingMaxRange = 10f,
            BossProjectileVisuals visuals = default, Action<GameObject> onHit = null,
            Action<ProjectileHit> onDamage = null)
        {
            var go = new GameObject($"Boss{style}");
            go.transform.position = origin;
            Vector3 aimDirection = initialDirection.sqrMagnitude > 0.0001f ? initialDirection.normalized : Vector3.forward;
            go.transform.rotation = SafeLookRotation(aimDirection);

            // Quaternion's C# default (all-zero) isn't a valid rotation (unlike Quaternion.identity)
            // - callers building the struct via `new BossProjectileVisuals { ... }` without setting
            // ExtraRotationOffset would otherwise apply a degenerate rotation to the imported model.
            Quaternion extraOffset = visuals.ExtraRotationOffset == default ? Quaternion.identity : visuals.ExtraRotationOffset;

            bool usedImportedVisual = visuals.ImportedVisualPrefab != null;
            if (usedImportedVisual)
            {
                var visualInstance = Instantiate(visuals.ImportedVisualPrefab, go.transform);
                visualInstance.name = "Visual";
                ImportedVfxUtility.FixUrpMaterials(visualInstance);
                // Some effect packs scale particle SIZE only via "Shape"/"Local" mode, ignoring the
                // parent transform - see ForceHierarchyParticleScaling. Without this, the scale
                // below wouldn't actually shrink/grow the visual.
                ImportedVfxUtility.ForceHierarchyParticleScaling(visualInstance);

                visualInstance.transform.localPosition = Vector3.zero;
                visualInstance.transform.localRotation = extraOffset;
                visualInstance.transform.localScale = Vector3.one * (visuals.ImportedVisualScale > 0f ? visuals.ImportedVisualScale : 1f);

                if (visuals.ForceLoopParticles)
                {
                    foreach (var ps in visualInstance.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        var main = ps.main;
                        main.loop = true;
                        if (!ps.isPlaying) ps.Play();
                    }
                }
            }
            else
            {
                switch (style)
                {
                    case ProjectileVisualStyle.BigProjectile:
                        BuildBigProjectileVisual(go, color, visualRadius);
                        break;
                    case ProjectileVisualStyle.Fireball:
                        BuildFireballVisual(go, color, visualRadius);
                        break;
                    case ProjectileVisualStyle.Bullet:
                        BuildStreakVisual(go, color, visualRadius, 3.2f);
                        break;
                    default:
                        BuildStreakVisual(go, color, visualRadius, 1.4f);
                        break;
                }

                // BuildFireballVisual adds its own tuned Light already.
                if (style != ProjectileVisualStyle.Fireball)
                {
                    var light = go.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = color;
                    light.range = visualRadius * (style == ProjectileVisualStyle.BigProjectile ? 18f : 12f);
                    light.intensity = 4f;
                }

                // Skipped for Fireball - a straight tapered ribbon looked bad trailing behind a
                // round blob of fire; the flame particle layers already leave their own soft trail
                // behind as they drift, which suits the shape far better.
                if (style != ProjectileVisualStyle.Fireball)
                {
                    var trail = go.AddComponent<TrailRenderer>();
                    trail.time = style == ProjectileVisualStyle.BigProjectile ? 0.5f : 0.2f;
                    trail.startWidth = visualRadius * (style == ProjectileVisualStyle.BigProjectile ? 1.1f : 1.6f);
                    trail.endWidth = 0f;
                    trail.material = new Material(Shader.Find("Sprites/Default"));
                    trail.startColor = color;
                    trail.endColor = new Color(color.r, color.g, color.b, 0f);
                    trail.minVertexDistance = 0.03f;
                    trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }

            var sphereCollider = go.AddComponent<SphereCollider>();
            sphereCollider.isTrigger = true;
            sphereCollider.radius = visualRadius;

            // Unity only raises OnTriggerEnter for a pair of colliders if at least one of the two
            // GameObjects has a Rigidbody - a trigger collider with no Rigidbody on either side is
            // never tested for overlap at all, regardless of collider size/position. This
            // projectile moves by direct transform.position assignment (see Update below), not
            // physics, so it never had one; several enemy types (EnemyFlyingAI/EnemySmallAI move
            // via transform too, and CharacterController-driven enemies don't count as a
            // Rigidbody either) also have none - so hits against them silently never fired, which
            // read as "bullets passing straight through." A kinematic Rigidbody here satisfies
            // Unity's requirement unconditionally, independent of whatever the target has.
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.None; // position is already set explicitly every frame

            var projectile = go.AddComponent<BossProjectile>();
            projectile._impactEffectPrefab = visuals.ImpactEffectPrefab;
            projectile._impactEffectScale = visuals.ImpactEffectScale > 0f ? visuals.ImpactEffectScale : 1f;
            projectile._onHit = onHit;
            projectile._onDamage = onDamage;
            projectile.Launch(origin, aimDirection, target, projectileSpeed, projectileDamage,
                isHoming, lifetimeSeconds, targetMask, homingTurnDegreesPerSecond, homingMaxRange);

            return projectile;
        }

        // Elongated body (a stretched capsule, nose pointed along travel direction) plus a
        // continuous exhaust particle system trailing off the tail - the fallback look for the
        // bigger projectile when no imported visual is assigned.
        private static void BuildBigProjectileVisual(GameObject go, Color color, float visualRadius)
        {
            var bodyGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            bodyGo.name = "Body";
            bodyGo.transform.SetParent(go.transform, false);
            bodyGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // capsule's long axis -> local Z (forward)
            bodyGo.transform.localScale = new Vector3(visualRadius * 1.4f, visualRadius * 3.2f, visualRadius * 1.4f);
            Destroy(bodyGo.GetComponent<Collider>());
            ApplyEmissiveMaterial(bodyGo, new Color(0.75f, 0.75f, 0.78f), 0.4f);

            var noseGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            noseGo.name = "Nose";
            noseGo.transform.SetParent(go.transform, false);
            noseGo.transform.localPosition = new Vector3(0f, 0f, visualRadius * 3.4f);
            noseGo.transform.localScale = Vector3.one * visualRadius * 1.3f;
            Destroy(noseGo.GetComponent<Collider>());
            ApplyEmissiveMaterial(noseGo, color, 3f);

            var exhaustGo = new GameObject("Exhaust");
            exhaustGo.transform.SetParent(go.transform, false);
            exhaustGo.transform.localPosition = new Vector3(0f, 0f, -visualRadius * 3.2f);
            exhaustGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var ps = exhaustGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.35f;
            main.startSpeed = 1.5f;
            main.startSize = visualRadius * 1.8f;
            main.startColor = new Color(1f, 0.6f, 0.2f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 60f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = visualRadius * 0.5f;

            ps.GetComponent<ParticleSystemRenderer>().material = CreateParticleMaterial();
        }

        // A guaranteed-solid, always-visible emissive sphere as the actual "body" of the fireball
        // (sparse/translucent particles alone read as "hollow" - alpha-blended Sprites/Default
        // doesn't accumulate brightness the way additive would, so a handful of soft, mostly-
        // transparent particles with nothing solid behind them just look empty), with dense
        // layered flame particles on top for texture/motion. Two particle layers: a bright inner
        // layer close to the surface, and a larger, more numerous outer layer for a proper "ball
        // of fire" silhouette instead of a faint wisp around a small dot.
        private static void BuildFireballVisual(GameObject go, Color color, float visualRadius)
        {
            Texture2D softTex = GetSoftParticleTexture();

            var coreGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            coreGo.name = "Core";
            coreGo.transform.SetParent(go.transform, false);
            coreGo.transform.localScale = Vector3.one * visualRadius * 1.3f;
            Destroy(coreGo.GetComponent<Collider>());
            ApplyEmissiveMaterial(coreGo, new Color(1f, 0.85f, 0.5f), 2.5f);

            var light = go.GetComponent<Light>();
            if (light == null) light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.6f, 0.2f);
            light.range = visualRadius * 6f;
            light.intensity = 3f;

            BuildFlameLayer(go, softTex, color, "InnerFlames",
                sizeMin: visualRadius * 0.6f, sizeMax: visualRadius * 1f,
                shapeRadius: visualRadius * 0.35f, rate: 70f, alpha: 0.8f, lifetime: 0.3f);

            BuildFlameLayer(go, softTex, color, "OuterFlames",
                sizeMin: visualRadius * 0.8f, sizeMax: visualRadius * 1.3f,
                shapeRadius: visualRadius * 0.6f, rate: 50f, alpha: 0.65f, lifetime: 0.45f);
        }

        private static void BuildFlameLayer(GameObject go, Texture2D softTex, Color color, string name,
            float sizeMin, float sizeMax, float shapeRadius, float rate, float alpha, float lifetime)
        {
            var flameGo = new GameObject(name);
            flameGo.transform.SetParent(go.transform, false);
            var flameParticle = flameGo.AddComponent<ParticleSystem>();
            var flameMain = flameParticle.main;
            flameMain.startLifetime = lifetime;
            flameMain.startSpeed = 0.6f;
            flameMain.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            flameMain.startColor = new Color(1f, 0.65f, 0.2f);
            flameMain.simulationSpace = ParticleSystemSimulationSpace.Local;

            var flameEmission = flameParticle.emission;
            flameEmission.rateOverTime = rate;

            var flameShape = flameParticle.shape;
            flameShape.shapeType = ParticleSystemShapeType.Sphere;
            flameShape.radius = shapeRadius;

            var flameColorOverLifetime = flameParticle.colorOverLifetime;
            flameColorOverLifetime.enabled = true;
            var flameGradient = new Gradient();
            flameGradient.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.9f, 0.6f), 0f), new GradientColorKey(new Color(1f, 0.5f, 0.1f), 0.4f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(alpha, 0.6f), new GradientAlphaKey(0f, 1f) });
            flameColorOverLifetime.color = flameGradient;

            var flameSizeOverLifetime = flameParticle.sizeOverLifetime;
            flameSizeOverLifetime.enabled = true;
            flameSizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.3f));

            var flameNoise = flameParticle.noise;
            flameNoise.enabled = true;
            flameNoise.strength = shapeRadius * 0.5f;
            flameNoise.frequency = 1.2f;
            flameNoise.scrollSpeed = 1.5f;

            flameParticle.GetComponent<ParticleSystemRenderer>().material = CreateAdditiveParticleMaterial(softTex, Color.white);
        }

        // Thin stretched streak - cheap (no attached particle system, since bullets fire in fast
        // bursts) but visually distinct from a round ball.
        private static void BuildStreakVisual(GameObject go, Color color, float visualRadius, float stretch)
        {
            var meshGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            meshGo.name = "Core";
            meshGo.transform.SetParent(go.transform, false);
            meshGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            meshGo.transform.localScale = new Vector3(visualRadius, visualRadius * stretch, visualRadius);
            Destroy(meshGo.GetComponent<Collider>());
            ApplyEmissiveMaterial(meshGo, color, 4f);
        }

        private static void ApplyEmissiveMaterial(GameObject target, Color color, float emissionStrength)
        {
            var renderer = target.GetComponent<Renderer>();
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * emissionStrength);
            }
            renderer.material = material;
        }

        private static Material CreateParticleMaterial()
        {
            return CreateAdditiveParticleMaterial(GetSoftParticleTexture(), Color.white);
        }

        private static Texture2D _softParticleTexture;

        // Radial white-to-transparent falloff, generated once and cached - a bare Shuriken quad
        // with no texture renders as a hard-edged square ("massive blocky particles"); this gives
        // every procedural particle a soft circular glow instead.
        private static Texture2D GetSoftParticleTexture()
        {
            if (_softParticleTexture != null) return _softParticleTexture;

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "SoftParticle",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            var center = new Vector2(size * 0.5f, size * 0.5f);
            float radius = size * 0.5f;
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / radius;
                    float alpha = Mathf.Clamp01(1f - dist);
                    alpha = alpha * alpha * (3f - 2f * alpha); // smoothstep - softer edge than a linear falloff
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            _softParticleTexture = texture;
            return texture;
        }

        // Sprites/Default, not URP/Particles/Unlit - the URP shader's Surface/Blend properties
        // didn't reliably take effect at runtime (still rendered as an opaque square regardless of
        // what was poked at it, exactly the same class of failure as the earlier "PNG card" bug),
        // and this project already has a proven track record with Sprites/Default rendering real
        // alpha transparency correctly (every TrailRenderer/tracer material elsewhere in this
        // codebase uses it). Not additive, just plain alpha blend - but a soft, correctly
        // transparent circle beats a technically-additive hard-edged square.
        private static Material CreateAdditiveParticleMaterial(Texture2D sprite, Color tint)
        {
            var material = new Material(Shader.Find("Sprites/Default"));
            material.SetTexture("_MainTex", sprite);
            material.SetColor("_Color", tint);
            return material;
        }

        // Quaternion.LookRotation(direction, Vector3.up) becomes unstable/degenerate once
        // direction is nearly parallel to the up hint (e.g. firing almost straight up at a
        // hovering flying enemy) - the cross-product LookRotation derives its right/up axes from
        // internally has near-zero magnitude there, so the resulting rotation can flip or spin
        // erratically frame to frame even though the projectile's actual travel direction/hit
        // detection are completely unaffected (confirmed: shots still landed, only the visual
        // orientation looked wrong specifically when aiming steeply upward). Falls back to
        // Vector3.forward as the up hint in that case, which is never parallel to a direction
        // that's nearly vertical.
        private static Quaternion SafeLookRotation(Vector3 direction)
        {
            Vector3 upHint = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
            return Quaternion.LookRotation(direction, upHint);
        }

        public void Launch(Vector3 origin, Vector3 initialDirection, Transform target, float projectileSpeed,
            float projectileDamage, bool isHoming, float lifetimeSeconds, LayerMask targetMask,
            float homingTurnDegreesPerSecond = 90f, float homingMaxRange = 10f)
        {
            transform.position = origin;
            _direction = initialDirection.sqrMagnitude > 0.0001f ? initialDirection.normalized : Vector3.forward;
            transform.rotation = SafeLookRotation(_direction);
            _target = target;
            speed = projectileSpeed;
            damage = projectileDamage;
            homing = isHoming;
            lifetime = lifetimeSeconds;
            hitMask = targetMask;
            turnDegreesPerSecond = homingTurnDegreesPerSecond;
            homingRange = homingMaxRange;
            _spawnTime = Time.time;

            var col = GetComponent<SphereCollider>();
            col.isTrigger = true;
        }

        private void Update()
        {
            if (Time.time - _spawnTime > lifetime)
            {
                Destroy(gameObject);
                return;
            }

            if (homing && _target != null)
            {
                Vector3 toTargetVector = _target.position + Vector3.up - transform.position;
                if (toTargetVector.magnitude <= homingRange)
                {
                    Vector3 toTarget = toTargetVector.normalized;
                    _direction = Vector3.RotateTowards(_direction, toTarget, turnDegreesPerSecond * Mathf.Deg2Rad * Time.deltaTime, 0f).normalized;
                    transform.rotation = SafeLookRotation(_direction);
                }
            }

            transform.position += _direction * speed * Time.deltaTime;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (((1 << other.gameObject.layer) & hitMask) == 0) return;

            var damageable = other.GetComponentInParent<IDamageable>();
            if (damageable != null && !damageable.IsDead)
            {
                float appliedDamage = damageable is Health health
                    ? health.ApplyDamageAndGetApplied(damage, transform.position, gameObject, DamageType.Ranged)
                    : ApplyUnknownDamageable(damageable);
                _onDamage?.Invoke(new ProjectileHit(other.gameObject, damageable, transform.position, appliedDamage));
                _onHit?.Invoke(other.gameObject);
            }

            SpawnImpactEffect();
            Destroy(gameObject);
        }

        // IDamageable intentionally stays a minimal backwards-compatible interface. Projectiles
        // can report exact damage for Health (the shared implementation); a third-party
        // implementation without that API still receives its normal hit and reports the request
        // as the best available value.
        private float ApplyUnknownDamageable(IDamageable damageable)
        {
            damageable.ApplyDamage(damage, transform.position, gameObject, DamageType.Ranged);
            return damage;
        }

        // Unparented (not a child of this GameObject) since this GameObject is destroyed the same
        // frame - the effect needs to keep playing on its own afterward. Self-destructs via its own
        // ParticleSystem lifetime; these imported prefabs are one-shot bursts, not loopers.
        private void SpawnImpactEffect()
        {
            if (_impactEffectPrefab == null) return;

            var effect = Instantiate(_impactEffectPrefab, transform.position, Quaternion.identity);
            effect.transform.localScale = Vector3.one * _impactEffectScale;
            ImportedVfxUtility.FixUrpMaterials(effect);

            float maxLifetime = 0.1f;
            foreach (var ps in effect.GetComponentsInChildren<ParticleSystem>())
            {
                maxLifetime = Mathf.Max(maxLifetime, ps.main.duration + ps.main.startLifetime.constantMax);
            }

            Destroy(effect, maxLifetime + 0.5f);
        }
    }
}
