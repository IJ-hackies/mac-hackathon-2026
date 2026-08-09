using System.Collections;
using Gameplay.Waves;
using Player.UI;
using Services.Leaderboards;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Player.UI.Score
{
    /// <summary>Owns the run-end leaderboard submission flow: records the local best, prompts for
    /// a username on the first-ever completed run (skippable, re-prompted next game-over if
    /// skipped), then submits to both cloud leaderboards. Self-builds its own prompt UI the same
    /// way SettingsMenuController builds its confirm dialog. Add anywhere on the PlayerRig
    /// prefab; everything else is resolved automatically.
    ///
    /// Gates on UsernamePolicy.HasChosenName (a local flag), not
    /// AuthenticationService.PlayerName - Unity Authentication assigns anonymous sessions an
    /// auto-generated default display name, so checking that field for "empty" never actually
    /// gated anything and every run silently submitted under a name like "TestUser#12345".</summary>
    [DisallowMultipleComponent]
    public sealed class RunScoreCoordinator : MonoBehaviour
    {
        [SerializeField] private WaveDirector waveDirector;
        [SerializeField] private Canvas hudCanvas;

        private GameObject _promptRoot;
        private GameObject _saveCta;
        private InputField _nameInput;
        private Text _errorText;
        private WaveRunResult _pendingResult;

        private void Awake()
        {
            if (waveDirector == null) waveDirector = Object.FindFirstObjectByType<WaveDirector>();
            if (hudCanvas == null) hudCanvas = GetComponentInChildren<Canvas>(true);
        }

        private void OnEnable()
        {
            if (waveDirector != null) waveDirector.RunEnded += HandleRunEnded;
        }

        private void OnDisable()
        {
            if (waveDirector != null) waveDirector.RunEnded -= HandleRunEnded;
        }

        private void HandleRunEnded(WaveRunResult result)
        {
            _pendingResult = result;
            int score = ScoreRules.ComputeScore(result);
            LocalScoreRecord.TryRecordRun(score, result.WaveReached);

            if (!UsernamePolicy.HasChosenName)
            {
                // See the final score/summary first; only bring up the submission prompt once
                // they choose to via this CTA, rather than instantly covering the summary.
                ShowSaveCta();
                return;
            }

            StartCoroutine(SubmitRoutine(result.WaveReached, score));
        }

        // Small standalone call-to-action near the bottom of the run summary screen. Self-built
        // rather than added to GameOverMissionSummaryView's own button row - that class is
        // sealed and already wired with fixed serialized fields in the scene/prefab.
        private void ShowSaveCta()
        {
            EnsureSaveCta();
            _saveCta.transform.SetAsLastSibling();
            _saveCta.SetActive(true);
        }

        private void EnsureSaveCta()
        {
            if (_saveCta != null) return;

            Transform parent = hudCanvas != null ? hudCanvas.transform : transform;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _saveCta = new GameObject("SaveScoreCta", typeof(RectTransform), typeof(Image), typeof(Button));
            _saveCta.transform.SetParent(parent, false);
            var rect = (RectTransform)_saveCta.transform;
            // Anchored to screen center (matching GameOverMissionSummaryView's own 680x600
            // centered panel) and positioned inside that panel's bounds, just below its
            // Restart/Main Menu row, rather than floating as a separate element beneath it.
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(480f, 46f);
            rect.anchoredPosition = new Vector2(0f, -262f);

            var image = _saveCta.GetComponent<Image>();
            image.sprite = RuntimeUiSprites.RoundedRect;
            image.type = Image.Type.Sliced;
            image.color = new Color(0.25f, 0.55f, 0.3f, 0.95f);

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(_saveCta.transform, false);
            var text = textGo.GetComponent<Text>();
            text.font = font;
            text.fontSize = 16;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.text = "SAVE SCORE TO LEADERBOARD";
            var textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 4f);
            textRect.offsetMax = new Vector2(-8f, -8f);

            var button = _saveCta.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(HandleSaveCtaClicked);
            UiSfxWirer.WireAll(_saveCta);

            _saveCta.SetActive(false);
        }

        private void HandleSaveCtaClicked()
        {
            if (_saveCta != null) _saveCta.SetActive(false);
            ShowNamePrompt();
        }

        private IEnumerator SubmitRoutine(int waveReached, int score)
        {
            var task = LeaderboardsClient.SubmitRunAsync(waveReached, score);
            while (!task.IsCompleted) yield return null;
        }

        private void ShowNamePrompt()
        {
            EnsurePromptPanel();
            _promptRoot.transform.SetAsLastSibling();
            _promptRoot.SetActive(true);
            SetError(null);
            if (EventSystem.current != null && _nameInput != null)
            {
                EventSystem.current.SetSelectedGameObject(_nameInput.gameObject);
            }
        }

        // Self-built the same way SettingsMenuController.EnsureConfirmDialog is - dim backdrop +
        // centered panel, LegacyRuntime font, built lazily since it is only ever needed once
        // (first completed run without a locally-stored username).
        private void EnsurePromptPanel()
        {
            if (_promptRoot != null) return;

            Transform parent = hudCanvas != null ? hudCanvas.transform : transform;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _promptRoot = new GameObject("LeaderboardNamePrompt", typeof(RectTransform));
            _promptRoot.transform.SetParent(parent, false);
            var rootRect = (RectTransform)_promptRoot.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            // Own Canvas with overrideSorting forced high - GameOverMissionSummaryView may live
            // on a different Canvas than whatever GetComponentInChildren<Canvas> happens to find
            // first, which previously let this prompt render underneath the run summary and
            // bleed together. A dedicated top-sorted Canvas makes this independent of that
            // hierarchy entirely.
            var promptCanvas = _promptRoot.AddComponent<Canvas>();
            promptCanvas.overrideSorting = true;
            promptCanvas.sortingOrder = 1000;
            _promptRoot.AddComponent<GraphicRaycaster>();

            var backdropGo = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            backdropGo.transform.SetParent(_promptRoot.transform, false);
            var backdropRect = (RectTransform)backdropGo.transform;
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;
            backdropGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.9f);

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(_promptRoot.transform, false);
            var panelRect = (RectTransform)panelGo.transform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(620f, 440f);
            panelRect.anchoredPosition = Vector2.zero;
            // Flat rounded-rect, not the CartoonSciFi_Popup sprite - that sprite's own art
            // doesn't behave as a plain rectangle at this size/aspect (its actual readable
            // interior is much smaller than its RectTransform bounds), which is what was causing
            // the field/buttons to visually overlap despite their anchored positions being
            // correctly spaced apart. This is the same flat treatment already proven working on
            // every other confirm-style dialog in this project (SettingsMenuController's own).
            var panelImage = panelGo.GetComponent<Image>();
            panelImage.sprite = RuntimeUiSprites.RoundedRect;
            panelImage.type = Image.Type.Sliced;
            panelImage.color = new Color(0.05f, 0.07f, 0.1f, 0.98f);

            var panelBorderGo = new GameObject("Border", typeof(RectTransform), typeof(Image));
            panelBorderGo.transform.SetParent(_promptRoot.transform, false);
            panelBorderGo.transform.SetSiblingIndex(panelGo.transform.GetSiblingIndex());
            var panelBorderRect = (RectTransform)panelBorderGo.transform;
            panelBorderRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelBorderRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelBorderRect.sizeDelta = new Vector2(626f, 446f);
            panelBorderRect.anchoredPosition = Vector2.zero;
            var panelBorderImage = panelBorderGo.GetComponent<Image>();
            panelBorderImage.sprite = RuntimeUiSprites.RoundedRect;
            panelBorderImage.type = Image.Type.Sliced;
            panelBorderImage.color = new Color(0.35f, 0.75f, 0.95f, 0.6f);

            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(panelGo.transform, false);
            var titleText = titleGo.GetComponent<Text>();
            titleText.font = font;
            titleText.fontSize = 20;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.UpperCenter;
            titleText.color = Color.white;
            titleText.text = "SAVE YOUR SCORE TO THE LEADERBOARD?";
            var titleRect = (RectTransform)titleGo.transform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -24f);
            titleRect.sizeDelta = new Vector2(-40f, 56f);

            var fieldGo = new GameObject("NameField", typeof(RectTransform), typeof(Image), typeof(InputField));
            fieldGo.transform.SetParent(panelGo.transform, false);
            var fieldRect = (RectTransform)fieldGo.transform;
            fieldRect.anchorMin = new Vector2(0.5f, 0.5f);
            fieldRect.anchorMax = new Vector2(0.5f, 0.5f);
            fieldRect.sizeDelta = new Vector2(380f, 48f);
            fieldRect.anchoredPosition = new Vector2(0f, 90f);
            var fieldImage = fieldGo.GetComponent<Image>();
            fieldImage.color = new Color(0.12f, 0.14f, 0.18f, 1f);

            var fieldTextGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            fieldTextGo.transform.SetParent(fieldGo.transform, false);
            var fieldText = fieldTextGo.GetComponent<Text>();
            fieldText.font = font;
            fieldText.fontSize = 18;
            fieldText.color = Color.white;
            fieldText.supportRichText = false;
            var fieldTextRect = (RectTransform)fieldTextGo.transform;
            fieldTextRect.anchorMin = Vector2.zero;
            fieldTextRect.anchorMax = Vector2.one;
            fieldTextRect.offsetMin = new Vector2(10f, 6f);
            fieldTextRect.offsetMax = new Vector2(-10f, -6f);

            var placeholderGo = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
            placeholderGo.transform.SetParent(fieldGo.transform, false);
            var placeholderText = placeholderGo.GetComponent<Text>();
            placeholderText.font = font;
            placeholderText.fontSize = 18;
            placeholderText.fontStyle = FontStyle.Italic;
            placeholderText.color = new Color(1f, 1f, 1f, 0.4f);
            placeholderText.text = "Enter a username (max 10 chars)...";
            var placeholderRect = (RectTransform)placeholderGo.transform;
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = new Vector2(10f, 6f);
            placeholderRect.offsetMax = new Vector2(-10f, -6f);

            _nameInput = fieldGo.GetComponent<InputField>();
            _nameInput.textComponent = fieldText;
            _nameInput.placeholder = placeholderText;
            _nameInput.characterLimit = UsernamePolicy.MaxLength;

            var errorGo = new GameObject("Error", typeof(RectTransform), typeof(Text));
            errorGo.transform.SetParent(panelGo.transform, false);
            _errorText = errorGo.GetComponent<Text>();
            _errorText.font = font;
            _errorText.fontSize = 15;
            _errorText.alignment = TextAnchor.MiddleCenter;
            _errorText.color = new Color(0.95f, 0.4f, 0.4f, 1f);
            var errorRect = (RectTransform)errorGo.transform;
            errorRect.anchorMin = new Vector2(0.5f, 0.5f);
            errorRect.anchorMax = new Vector2(0.5f, 0.5f);
            errorRect.sizeDelta = new Vector2(480f, 24f);
            errorRect.anchoredPosition = new Vector2(0f, 30f);

            Button submitButton = CreatePromptButton(panelGo.transform, font, "SubmitButton",
                "SAVE & SUBMIT", new Vector2(-140f, -110f), new Color(0.25f, 0.55f, 0.3f, 0.95f));
            submitButton.onClick.AddListener(HandleSubmitClicked);

            Button skipButton = CreatePromptButton(panelGo.transform, font, "SkipButton",
                "SKIP", new Vector2(140f, -110f), new Color(0.25f, 0.3f, 0.35f, 0.95f));
            skipButton.onClick.AddListener(HandleSkipClicked);

            _promptRoot.SetActive(false);
        }

        private Button CreatePromptButton(Transform parent, Font font, string name, string label,
            Vector2 anchoredPosition, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(220f, 56f);
            rect.anchoredPosition = anchoredPosition;
            // Flat rounded-rect, not the button-kit sprite - see the Panel comment above for why.
            var image = go.GetComponent<Image>();
            image.sprite = RuntimeUiSprites.RoundedRect;
            image.type = Image.Type.Sliced;
            image.color = color;

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.GetComponent<Text>();
            text.font = font;
            text.fontSize = 18;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = label;
            var textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        private void HandleSubmitClicked()
        {
            string raw = _nameInput != null ? _nameInput.text : null;
            if (!UsernamePolicy.TryValidate(raw, out string sanitized, out string error))
            {
                SetError(error);
                return;
            }

            _promptRoot.SetActive(false);
            StartCoroutine(SetNameThenSubmitRoutine(sanitized));
        }

        private IEnumerator SetNameThenSubmitRoutine(string sanitizedName)
        {
            var nameTask = CloudIdentity.TrySetPlayerNameAsync(sanitizedName);
            while (!nameTask.IsCompleted) yield return null;

            UsernamePolicy.MarkNameChosen();
            yield return SubmitRoutine(_pendingResult.WaveReached, ScoreRules.ComputeScore(_pendingResult));
        }

        // Skip only dismisses this run's submission - the prompt re-appears on the next
        // game-over since no username has been chosen locally yet, matching the earlier design
        // decision to gate registration on a completed run rather than first launch.
        private void HandleSkipClicked()
        {
            if (_promptRoot != null) _promptRoot.SetActive(false);
        }

        private void SetError(string message)
        {
            if (_errorText == null) return;
            _errorText.text = message ?? string.Empty;
        }
    }
}
