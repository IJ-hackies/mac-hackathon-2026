using UnityEngine;
using UnityEngine.UI;

namespace Player.UI
{
    /// <summary>
    /// Renders a live, slowly-spinning instance of the coin model into a RenderTexture and
    /// displays it via a RawImage - the gold HUD's icon is the actual 3D coin asset, not a flat
    /// sprite. Builds its own isolated stage (model instance, light, orthographic camera, render
    /// texture) at runtime, parked far outside the playable world on a dedicated layer so nothing
    /// else in the scene ever renders or collides with it - the same self-contained runtime-build
    /// approach SceneTransitionController uses for its wipe canvas.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public sealed class Coin3DIconRenderer : MonoBehaviour
    {
        private const string IconLayerName = "UI3DIcon";
        private static readonly Vector3 StageOrigin = new Vector3(10000f, 10000f, 10000f);

        [SerializeField] private GameObject coinModelPrefab;
        [SerializeField, Min(32)] private int textureSize = 128;
        [SerializeField] private float spinSpeed = 45f;
        [SerializeField] private float cameraDistance = 3f;
        [SerializeField] private float modelScale = 0.72f;
        [SerializeField] private float orthographicSize = 0.75f;

        private Transform _coinInstance;

        private void Awake()
        {
            var rawImage = GetComponent<RawImage>();
            if (coinModelPrefab == null)
            {
                Debug.LogWarning("Coin3DIconRenderer: no coinModelPrefab assigned.", this);
                return;
            }

            int layer = LayerMask.NameToLayer(IconLayerName);
            if (layer < 0)
            {
                Debug.LogWarning(
                    $"Coin3DIconRenderer: layer \"{IconLayerName}\" not found - add it under " +
                    "Project Settings > Tags and Layers.", this);
                layer = 0;
            }

            var stageRoot = new GameObject("CoinIconStage");
            stageRoot.transform.position = StageOrigin;
            DontDestroyOnLoad(stageRoot);

            GameObject coinGo = Instantiate(coinModelPrefab, stageRoot.transform);
            coinGo.transform.localPosition = Vector3.zero;
            coinGo.transform.localRotation = Quaternion.identity;
            coinGo.transform.localScale = Vector3.one * modelScale;
            SetLayerRecursively(coinGo, layer);
            _coinInstance = coinGo.transform;

            var lightGo = new GameObject("CoinIconLight", typeof(Light));
            lightGo.transform.SetParent(stageRoot.transform, false);
            lightGo.transform.localRotation = Quaternion.Euler(35f, -30f, 0f);
            Light light = lightGo.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            light.shadows = LightShadows.None;
            light.cullingMask = 1 << layer;

            var renderTexture = new RenderTexture(textureSize, textureSize, 16, RenderTextureFormat.ARGB32)
            {
                name = "RT_CoinIcon",
                antiAliasing = 2
            };

            var cameraGo = new GameObject("CoinIconCamera", typeof(Camera));
            cameraGo.transform.SetParent(stageRoot.transform, false);
            cameraGo.transform.localPosition = new Vector3(0f, 0f, -cameraDistance);
            cameraGo.transform.localRotation = Quaternion.identity;
            Camera stageCamera = cameraGo.GetComponent<Camera>();
            stageCamera.clearFlags = CameraClearFlags.SolidColor;
            stageCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            stageCamera.cullingMask = 1 << layer;
            stageCamera.orthographic = true;
            stageCamera.orthographicSize = orthographicSize;
            stageCamera.nearClipPlane = 0.1f;
            stageCamera.farClipPlane = cameraDistance + 5f;
            stageCamera.targetTexture = renderTexture;
            stageCamera.allowHDR = false;
            stageCamera.allowMSAA = false;

            rawImage.texture = renderTexture;
        }

        private void Update()
        {
            if (_coinInstance != null)
            {
                _coinInstance.Rotate(Vector3.up, spinSpeed * Time.unscaledDeltaTime, Space.World);
            }
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
        }
    }
}
