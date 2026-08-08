using UnityEngine;
using UnityEngine.Rendering;

namespace Presentation
{
    /// <summary>
    /// Creates one rare, camera-relative shooting-star billboard without requiring a scene
    /// object or particle system. The single quad is pooled for the lifetime of the player.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpaceShootingStarController : MonoBehaviour
    {
        private const string MaterialResourcePath = "Presentation/M_ShootingStar";
        private const float MinimumSpawnDelay = 8f;
        private const float MaximumSpawnDelay = 18f;

        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int TintId = Shader.PropertyToID("_Tint");

        private MaterialPropertyBlock properties;

        private Mesh shootingStarMesh;
        private MeshRenderer shootingStarRenderer;
        private Camera activeCamera;
        private Vector3 skyDirection;
        private Vector3 travelTangent;
        private Color shootingStarTint;
        private float angularTravel;
        private float starDistance;
        private float starDuration;
        private float starStartTime;
        private float nextSpawnTime;
        private bool starActive;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<SpaceShootingStarController>(
                    FindObjectsInactive.Include) != null)
            {
                return;
            }

            GameObject controllerObject = new GameObject("Space Shooting Stars");
            DontDestroyOnLoad(controllerObject);
            controllerObject.AddComponent<SpaceShootingStarController>();
        }

        private void Awake()
        {
            properties = new MaterialPropertyBlock();

            Material shootingStarMaterial = Resources.Load<Material>(MaterialResourcePath);
            if (shootingStarMaterial == null)
            {
                Debug.LogWarning(
                    $"Shooting-star material was not found at Resources/{MaterialResourcePath}.",
                    this);
                enabled = false;
                return;
            }

            shootingStarMesh = BuildQuadMesh();

            MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = shootingStarMesh;

            shootingStarRenderer = gameObject.AddComponent<MeshRenderer>();
            shootingStarRenderer.sharedMaterial = shootingStarMaterial;
            shootingStarRenderer.shadowCastingMode = ShadowCastingMode.Off;
            shootingStarRenderer.receiveShadows = false;
            shootingStarRenderer.lightProbeUsage = LightProbeUsage.Off;
            shootingStarRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            shootingStarRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            shootingStarRenderer.allowOcclusionWhenDynamic = false;
            shootingStarRenderer.enabled = false;

            ScheduleNextSpawn(4f, 9f);
        }

        private void Update()
        {
            if (!starActive)
            {
                if (Time.unscaledTime >= nextSpawnTime)
                {
                    TrySpawnStar();
                }

                return;
            }

            if (activeCamera == null)
            {
                FinishStar();
                return;
            }

            float progress = Mathf.Clamp01(
                (Time.unscaledTime - starStartTime) / starDuration);
            float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
            Vector3 currentDirection = (
                skyDirection + travelTangent * Mathf.Lerp(
                    -angularTravel * 0.5f,
                    angularTravel * 0.5f,
                    easedProgress)).normalized;

            transform.position = activeCamera.transform.position + currentDirection * starDistance;
            Vector3 billboardUp = Vector3.Cross(currentDirection, travelTangent).normalized;
            if (billboardUp.sqrMagnitude < 0.5f)
            {
                billboardUp = activeCamera.transform.up;
            }

            transform.rotation = Quaternion.LookRotation(currentDirection, billboardUp);

            float fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.16f));
            float fadeOut = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01((1f - progress) / 0.28f));
            properties.SetFloat(IntensityId, fadeIn * fadeOut);
            properties.SetColor(TintId, shootingStarTint);
            shootingStarRenderer.SetPropertyBlock(properties);

            if (progress >= 1f)
            {
                FinishStar();
            }
        }

        private void TrySpawnStar()
        {
            activeCamera = Camera.main;
            if (activeCamera == null)
            {
                ScheduleNextSpawn(1f, 2f);
                return;
            }

            Vector3 localDirection = new Vector3(
                Random.Range(-0.7f, 0.7f),
                Random.Range(-0.2f, 0.65f),
                1f).normalized;
            skyDirection = activeCamera.transform.TransformDirection(localDirection).normalized;

            float travelAngle = Random.Range(-28f, 28f) * Mathf.Deg2Rad;
            Vector3 cameraPlaneTangent =
                activeCamera.transform.right * Mathf.Cos(travelAngle) +
                activeCamera.transform.up * Mathf.Sin(travelAngle);
            travelTangent = Vector3.ProjectOnPlane(cameraPlaneTangent, skyDirection).normalized;
            if (travelTangent.sqrMagnitude < 0.5f)
            {
                travelTangent = Vector3.ProjectOnPlane(
                    activeCamera.transform.right,
                    skyDirection).normalized;
            }

            starDistance = Mathf.Clamp(activeCamera.farClipPlane * 0.35f, 80f, 220f);
            angularTravel = Random.Range(0.18f, 0.30f);
            starDuration = Random.Range(0.65f, 0.95f);
            starStartTime = Time.unscaledTime;

            float angularLength = Random.Range(0.06f, 0.095f);
            float angularWidth = Random.Range(0.0015f, 0.003f);
            transform.localScale = new Vector3(
                starDistance * angularLength,
                starDistance * angularWidth,
                1f);

            shootingStarTint = Color.Lerp(
                new Color(1.7f, 1.15f, 0.75f, 1f),
                new Color(1.15f, 1.65f, 2.5f, 1f),
                Random.Range(0.55f, 1f));
            properties.Clear();
            properties.SetFloat(IntensityId, 0f);
            properties.SetColor(TintId, shootingStarTint);
            shootingStarRenderer.SetPropertyBlock(properties);
            shootingStarRenderer.enabled = true;
            starActive = true;
        }

        private void FinishStar()
        {
            starActive = false;
            activeCamera = null;
            if (shootingStarRenderer != null)
            {
                shootingStarRenderer.enabled = false;
            }

            ScheduleNextSpawn(MinimumSpawnDelay, MaximumSpawnDelay);
        }

        private void ScheduleNextSpawn(float minimumDelay, float maximumDelay)
        {
            nextSpawnTime = Time.unscaledTime + Random.Range(minimumDelay, maximumDelay);
        }

        private static Mesh BuildQuadMesh()
        {
            Mesh mesh = new Mesh
            {
                name = "Runtime Shooting Star Quad",
                hideFlags = HideFlags.DontSave
            };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            return mesh;
        }

        private void OnDestroy()
        {
            if (shootingStarMesh != null)
            {
                Destroy(shootingStarMesh);
            }
        }
    }
}
