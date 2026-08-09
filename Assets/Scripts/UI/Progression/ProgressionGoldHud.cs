using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Player.UI.Progression
{
    [DisallowMultipleComponent]
    public sealed class ProgressionGoldHud : MonoBehaviour
    {
        [SerializeField] private ProgressionDataAdapter progression;
        [SerializeField] private Text valueText;
        [SerializeField] private string prefix = "G ";

        [Header("Collect Feedback")]
        [Tooltip("Whole HUD box - punch-scaled on collect. Falls back to this object's own " +
                 "transform if unset.")]
        [SerializeField] private RectTransform panelRect;
        [SerializeField] private float popDuration = 0.28f;
        [SerializeField] private float popScale = 0.22f;
        [Tooltip("Small, subtle burst of yellow squares from the HUD box - self-built (no " +
                 "external VFX asset), matches the confirm-dialog/tutorial UI's own runtime-built " +
                 "UI convention.")]
        [SerializeField] private int confettiCount = 7;
        [SerializeField] private float confettiSpeed = 90f;
        [SerializeField] private float confettiDuration = 0.55f;
        [SerializeField] private Color confettiColor = new Color(1f, 0.85f, 0.25f, 1f);

        /// Static single instance - CoinPickup (spawned per-enemy-kill, with no direct scene
        /// reference to hand it) pings this to trigger the pop/confetti on arrival.
        public static ProgressionGoldHud Instance { get; private set; }

        public void Configure(Text target) => valueText = target;
        public void Bind(MonoBehaviour source)
        {
            if (progression == null) progression = GetComponent<ProgressionDataAdapter>();
            if (progression != null) progression.Bind(source);
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            if (progression == null) progression = GetComponent<ProgressionDataAdapter>();
            if (progression != null) progression.Refreshed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (progression != null) progression.Refreshed -= Refresh;
        }

        public void Refresh()
        {
            if (valueText != null) valueText.text = prefix + (progression != null ? progression.Gold : 0);
        }

        /// Called once per coin as it arrives at the player (see CoinPickup) - a quick scale
        /// punch on the whole HUD box plus a small confetti burst, purely cosmetic feedback for
        /// gold that was already added to the run total the instant the enemy died.
        public void PlayCollectPop()
        {
            RectTransform target = panelRect != null ? panelRect : (RectTransform)transform;
            StopCoroutine(nameof(PopRoutine));
            StartCoroutine(PopRoutine(target));
            SpawnConfetti(target);
        }

        private IEnumerator PopRoutine(RectTransform target)
        {
            float elapsed = 0f;
            while (elapsed < popDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / popDuration);
                float scale = 1f + Mathf.Sin(t * Mathf.PI) * popScale;
                target.localScale = Vector3.one * scale;
                yield return null;
            }

            target.localScale = Vector3.one;
        }

        private void SpawnConfetti(RectTransform origin)
        {
            for (int i = 0; i < confettiCount; i++)
            {
                var pieceGo = new GameObject("ConfettiPiece", typeof(RectTransform), typeof(Image));
                pieceGo.transform.SetParent(origin, false);
                var rect = (RectTransform)pieceGo.transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = Vector2.one * Random.Range(5f, 9f);
                rect.anchoredPosition = Vector2.zero;
                rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

                var image = pieceGo.GetComponent<Image>();
                image.color = confettiColor;
                image.raycastTarget = false;

                float angle = Random.Range(0f, Mathf.PI * 2f);
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.6f + 0.4f);
                StartCoroutine(AnimateConfetti(rect, image, direction * Random.Range(0.6f, 1f)));
            }
        }

        private IEnumerator AnimateConfetti(RectTransform rect, Image image, Vector2 direction)
        {
            float elapsed = 0f;
            Vector2 start = rect.anchoredPosition;
            Color startColor = image.color;
            float spin = Random.Range(-260f, 260f);

            while (elapsed < confettiDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / confettiDuration);
                Vector2 offset = direction * confettiSpeed * t;
                offset.y -= 60f * t * t; // gentle fall-off, matches a light gravity arc
                rect.anchoredPosition = start + offset;
                rect.Rotate(Vector3.forward, spin * Time.unscaledDeltaTime);
                image.color = new Color(startColor.r, startColor.g, startColor.b, startColor.a * (1f - t));
                yield return null;
            }

            if (rect != null) Destroy(rect.gameObject);
        }
    }
}
