using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Player.UI
{
    /// <summary>
    /// Persistent (DontDestroyOnLoad) circle-wipe scene transition. Every gameplay/menu scene
    /// switch should route through <see cref="LoadScene"/> instead of calling
    /// SceneManager.LoadScene directly: it wipes to a full-screen circle covering the current
    /// scene, loads the next scene asynchronously with activation held back
    /// (allowSceneActivation = false) until it's actually ready, then wipes the circle back open
    /// on the loaded scene. This is what removes the jitter/pop of a synchronous LoadScene
    /// swapping scenes out from under a still-rendering frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneTransitionController : MonoBehaviour
    {
        private const string WipeMaterialPath = "UI/M_CircleWipe";
        private const float WipeSoftness = 0.03f;
        // Extra beyond the exact corner distance so the soft antialiased edge (WipeSoftness)
        // finishes fully off-screen instead of leaving a faint ring visible at the corners.
        private const float CoverageMargin = 0.05f;

        [SerializeField] private float closeDuration = 0.45f;
        [SerializeField] private float openDuration = 0.45f;
        [Tooltip("Keeps the covered hold for at least this long even if the next scene loads " +
                 "instantly, so the wipe never reads as a single-frame flash.")]
        [SerializeField] private float minimumHoldDuration = 0.15f;
        // White so it doesn't tint away the wipe material's own deep-space/star color (the
        // Image's vertex color multiplies into the shader's _Color) - only change this for a
        // deliberate overall tint, not to pick the wipe's actual color.
        [SerializeField] private Color wipeColor = Color.white;

        private static SceneTransitionController _instance;
        private static readonly int RadiusId = Shader.PropertyToID("_Radius");
        private static readonly int AspectId = Shader.PropertyToID("_Aspect");

        private Image _wipeImage;
        private Material _wipeMaterial;
        private bool _transitioning;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;

            var go = new GameObject(nameof(SceneTransitionController));
            Object.DontDestroyOnLoad(go);
            go.AddComponent<SceneTransitionController>();
        }

        /// Routes a scene switch through the circle wipe. Ignored if a transition is already
        /// playing, so a doubled button click can't stack two loads.
        public static void LoadScene(string sceneName)
        {
            if (_instance == null) Bootstrap();
            if (_instance._transitioning) return;
            _instance.StartCoroutine(_instance.LoadSceneRoutine(sceneName));
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            BuildWipeCanvas();
        }

        private void BuildWipeCanvas()
        {
            var canvasGo = new GameObject("WipeCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above every other Canvas (menus, HUD, tutorial UI, pause menu) so the wipe always
            // draws on top regardless of which scene is currently loaded.
            canvas.sortingOrder = 32760;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var imageGo = new GameObject("Wipe", typeof(RectTransform), typeof(Image));
            imageGo.transform.SetParent(canvasGo.transform, false);
            var rect = (RectTransform)imageGo.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _wipeImage = imageGo.GetComponent<Image>();
            _wipeImage.color = wipeColor;
            _wipeImage.raycastTarget = false;

            Material template = Resources.Load<Material>(WipeMaterialPath);
            if (template == null)
            {
                Debug.LogError($"SceneTransitionController: material \"{WipeMaterialPath}\" not " +
                    "found under Resources - scene loads will fall back to an instant swap.", this);
                _wipeImage.gameObject.SetActive(false);
                return;
            }

            _wipeMaterial = new Material(template);
            _wipeMaterial.SetFloat(Shader.PropertyToID("_Softness"), WipeSoftness);
            _wipeImage.material = _wipeMaterial;
            SetRadius(0f);

            _wipeImage.gameObject.SetActive(false);
        }

        /// Exact aspect-corrected UV distance to the farthest screen corner, plus enough margin
        /// that the soft edge (WipeSoftness) finishes fully off-screen - i.e. the smallest radius
        /// that guarantees full coverage at the current resolution/aspect, computed fresh per
        /// transition instead of a guessed constant (which under-covered wide aspect ratios).
        private static float ComputeCoverRadius(float aspect)
        {
            float halfDiagonal = Mathf.Sqrt((0.5f * aspect) * (0.5f * aspect) + 0.25f);
            return halfDiagonal + WipeSoftness + CoverageMargin;
        }

        private IEnumerator LoadSceneRoutine(string sceneName)
        {
            _transitioning = true;

            bool hasWipe = _wipeMaterial != null;
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            float coverRadius = ComputeCoverRadius(aspect);

            if (hasWipe)
            {
                _wipeImage.gameObject.SetActive(true);
                _wipeImage.raycastTarget = true;
                _wipeMaterial.SetFloat(AspectId, aspect);
                yield return AnimateRadius(0f, coverRadius, closeDuration);
            }

            AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            load.allowSceneActivation = false;

            float elapsed = 0f;
            while (load.progress < 0.9f || elapsed < minimumHoldDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            load.allowSceneActivation = true;
            while (!load.isDone) yield return null;

            // isDone can flip true a frame or two before every newly-activated object's own
            // Start() (including coroutine-based ones, e.g. opening cutscenes) has actually run -
            // a couple settle frames here means the wipe reliably opens onto a scene whose
            // objects have finished their own setup, instead of racing them.
            yield return null;
            yield return null;

            if (hasWipe)
            {
                aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
                _wipeMaterial.SetFloat(AspectId, aspect);
                yield return AnimateRadius(ComputeCoverRadius(aspect), 0f, openDuration);
                _wipeImage.raycastTarget = false;
                _wipeImage.gameObject.SetActive(false);
            }

            _transitioning = false;
        }

        // Interpolates covered *area* (radius^2) linearly rather than radius itself - screen
        // coverage scales with radius^2, so a linear radius lerp spends most of its duration
        // barely covering anything and then snaps to full coverage right at the end (closing
        // read as an abrupt pop instead of a fade). Lerping area keeps the covered/revealed
        // fraction of the screen changing at a constant rate in both directions.
        private IEnumerator AnimateRadius(float from, float to, float duration)
        {
            float safeDuration = Mathf.Max(0.0001f, duration);
            float elapsed = 0f;
            float areaFrom = from * from;
            float areaTo = to * to;
            SetRadius(from);

            while (elapsed < safeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / safeDuration);
                SetRadius(Mathf.Sqrt(Mathf.Lerp(areaFrom, areaTo, t)));
                yield return null;
            }

            SetRadius(to);
        }

        private void SetRadius(float radius)
        {
            if (_wipeMaterial != null) _wipeMaterial.SetFloat(RadiusId, radius);
        }
    }
}
