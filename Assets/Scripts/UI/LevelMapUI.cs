using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using CodeEscape.Game;
using CodeEscape.Programming;
using CodeEscape.Rewards;

namespace CodeEscape.UI
{
    /// <summary>
    /// The level map of the selected language: location cards (picture, number, name) joined by a path,
    /// showing which levels are cleared, which one is next and which are locked. Choosing a level opens its panel with the actions it allows:
    /// Play / Continue / Restart for the next level, Replay / Skip to Next Level for a cleared one, and only
    /// an explanation for a locked one - plus Back to Map. Play / Continue under the map starts the next
    /// level directly. Keyboard: arrows (or A/D) move between levels, Enter opens one, Esc closes the panel
    /// or returns to the language select screen.
    /// </summary>
    public class LevelMapUI : MonoBehaviour
    {
        [Header("Map")]
        [SerializeField, Tooltip("One node per level, in level order.")]
        List<LevelMapNode> nodes = new List<LevelMapNode>();
        [SerializeField, Tooltip("Path pieces: piece i joins node i and node i + 1.")]
        List<Graphic> pathSegments = new List<Graphic>();
        [SerializeField] TMP_Text languageText;
        [SerializeField] TMP_Text progressText;
        [SerializeField, Tooltip("Disabled while the level panel is open, so keyboard focus stays in the panel.")]
        CanvasGroup mapGroup;
        [SerializeField] Button playButton;
        [SerializeField] TMP_Text playButtonLabel;
        [SerializeField] Button backButton;

        [Header("Message Banner")]
        [SerializeField] CanvasGroup banner;
        [SerializeField] TMP_Text bannerText;
        [SerializeField, Min(0.5f)] float bannerTime = 4.5f;

        [Header("Level Panel")]
        [SerializeField] GameObject levelPanel;
        [SerializeField] Graphic panelAccent;
        [SerializeField] TMP_Text panelNumberText;
        [SerializeField] TMP_Text panelNameText;
        [SerializeField] TMP_Text panelStatusText;
        [SerializeField] TMP_Text panelMessageText;
        [SerializeField] Image panelIcon;
        [SerializeField] Button continueButton;
        [SerializeField] Button playLevelButton;
        [SerializeField] TMP_Text playLevelLabel;
        [SerializeField] Button skipButton;
        [SerializeField] Button backToMapButton;

        [Header("Look")]
        [SerializeField] Sprite checkIcon;
        [SerializeField] Sprite lockIcon;
        [SerializeField] Sprite playIcon;
        [SerializeField] Color pythonColor = new Color(1f, 0.83f, 0.23f);
        [SerializeField] Color phpColor = new Color(0.65f, 0.55f, 0.98f);
        [SerializeField] Color sqlColor = new Color(0.95f, 0.57f, 0.07f);
        [SerializeField] Color clearedColor = new Color(0.29f, 0.87f, 0.5f);
        [SerializeField] Color lockedColor = new Color(0.29f, 0.33f, 0.39f);
        [SerializeField] Color warningColor = new Color(0.98f, 0.8f, 0.08f);

        readonly List<LevelInfo> levels = new List<LevelInfo>();
        LevelProgressManager progress;
        ProgrammingLanguage language;
        Color accent;
        int panelIndex = -1;
        int lastFocused = -1;
        bool busy;
        float bannerUntil;

        public bool IsPanelOpen => levelPanel != null && levelPanel.activeSelf;
        public int PanelLevelIndex => IsPanelOpen ? panelIndex : -1;
        /// <summary>Index of the level to play next (the last one when everything is cleared); -1 before the map is set up. Read-only, for visuals.</summary>
        public int NextLevelIndex
        {
            get
            {
                if (progress == null || levels.Count == 0)
                    return -1;
                LevelInfo next = progress.GetNextLevel(language);
                return next != null ? levels.IndexOf(next) : levels.Count - 1;
            }
        }

        void Start()
        {
            progress = LevelProgressManager.Instance;
            // The game is Python only: opened without it selected, select it (a new Python playthrough).
            if (!LanguageManager.Current.IsPlayable())
                LanguageManager.EnsurePlayableLanguage();
            language = LanguageManager.Current;
            if (progress == null || LevelManager.Instance == null || !language.IsPlayable())
            {
                Debug.LogWarning("[LevelMap] GameSystems or the language is missing - returning to the main menu.");
                busy = SceneTransitionManager.Load(SceneNames.MainMenu);
                return;
            }

            levels.AddRange(LevelManager.Instance.Levels);
            if (levels.Count != nodes.Count)
                Debug.LogWarning($"[LevelMap] {levels.Count} levels but {nodes.Count} map nodes - extra levels are not shown.");
            accent = AccentFor(language);

            for (int i = 0; i < nodes.Count; i++)
            {
                int index = i;
                nodes[i].Button.onClick.AddListener(() => OpenLevel(index));
                nodes[i].gameObject.SetActive(i < levels.Count);
            }
            SetActive(levelPanel, false);
            SetBanner(0f);

            Refresh();
            ShowClearedNotice();
            LevelInfo next = progress.GetNextLevel(language);
            FocusNode(next != null ? levels.IndexOf(next) : Mathf.Min(levels.Count, nodes.Count) - 1);
        }

        void Update()
        {
            if (banner != null && banner.alpha > 0f && Time.unscaledTime > bannerUntil)
                SetBanner(Mathf.MoveTowards(banner.alpha, 0f, Time.unscaledDeltaTime * 2f));

            Keyboard keyboard = Keyboard.current;
            if (busy || keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
                return;
            if (IsPanelOpen)
                CloseLevelPanel();
            else
                OnBackClicked();
        }

        // ---------- Map ----------

        /// <summary>Redraws every node, the path, the progress text and the Play / Continue button.</summary>
        public void Refresh()
        {
            LevelInfo next = progress.GetNextLevel(language);
            int count = Mathf.Min(levels.Count, nodes.Count);
            for (int i = 0; i < count; i++)
            {
                LevelInfo level = levels[i];
                LevelMapState state = progress.GetState(language, level.levelId);
                nodes[i].Show(i + 1, level.displayName, level.mapTagline, level.mapPicture, state, level == next,
                    StatusText(level, state), accent, clearedColor, lockedColor, checkIcon);
                nodes[i].SetStars(StarsFor(level), state == LevelMapState.Completed);
            }
            for (int i = 0; i < pathSegments.Count; i++)
            {
                if (pathSegments[i] == null)
                    continue;
                bool open = i < count && progress.IsCompleted(language, levels[i].levelId);
                Color c = open ? clearedColor : lockedColor;
                c.a = open ? 0.9f : 0.45f;
                pathSegments[i].color = c;
            }

            if (languageText != null)
            {
                languageText.text = $"{language.ToDisplayName().ToUpperInvariant()} TRACK";
                languageText.color = accent;
            }
            if (progressText != null)
                progressText.text = $"{progress.CompletedCount(language)} / {levels.Count} LEVELS CLEARED";

            if (playButton != null)
            {
                playButton.interactable = next != null;
                if (playButtonLabel != null)
                {
                    playButtonLabel.text = next == null
                        ? "ALL LEVELS CLEARED"
                        : $"{(progress.CanResume(language, next.levelId) || progress.CompletedCount(language) > 0 ? "CONTINUE" : "PLAY")}  -  LEVEL {levels.IndexOf(next) + 1}";
                }
            }
            SetupNavigation(next);
        }

        string StatusText(LevelInfo level, LevelMapState state)
        {
            switch (state)
            {
                case LevelMapState.Completed: return "CLEARED";
                case LevelMapState.Locked: return "LOCKED";
                default: return progress.CanResume(language, level.levelId) ? "IN PROGRESS" : "NEXT UP";
            }
        }

        // Arrow keys follow the cards on screen (the route winds over two rows), not just the level order.
        void SetupNavigation(LevelInfo next)
        {
            int count = Mathf.Min(levels.Count, nodes.Count);
            Selectable below = playButton != null && playButton.interactable ? playButton : (Selectable)backButton;
            for (int i = 0; i < count; i++)
            {
                Vector2 from = nodes[i].transform.position;
                Navigation nav = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = NearestNode(from, Vector2.left, count, i),
                    selectOnRight = NearestNode(from, Vector2.right, count, i),
                    selectOnUp = NearestNode(from, Vector2.up, count, i),
                    selectOnDown = NearestNode(from, Vector2.down, count, i) ?? below
                };
                nodes[i].Button.navigation = nav;
            }
            Selectable nextNode = count > 0 ? nodes[next != null ? levels.IndexOf(next) : count - 1].Button : null;
            if (playButton != null)
                playButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = backButton, selectOnUp = nextNode };
            if (backButton != null)
            {
                backButton.navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnRight = playButton != null && playButton.interactable ? playButton : null,
                    selectOnUp = NearestNode(backButton.transform.position, Vector2.up, count, -1) ?? (count > 0 ? nodes[0].Button : null)
                };
            }
        }

        /// <summary>The closest card roughly in a direction from a screen position (null if there is none).</summary>
        Button NearestNode(Vector2 from, Vector2 direction, int count, int exclude)
        {
            Button best = null;
            float bestScore = float.MaxValue;
            for (int j = 0; j < count; j++)
            {
                if (j == exclude || !nodes[j].gameObject.activeSelf)
                    continue;
                Vector2 offset = (Vector2)nodes[j].transform.position - from;
                float along = Vector2.Dot(offset, direction);
                float across = Mathf.Abs(Vector2.Dot(offset, new Vector2(-direction.y, direction.x)));
                if (along <= 1f || across > along * 1.5f)
                    continue;
                float score = along + across * 2f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = nodes[j].Button;
                }
            }
            return best;
        }

        void ShowClearedNotice()
        {
            LevelProgressManager.ClearedNotice notice = progress.ConsumeNotice(language);
            if (notice == null)
                return;
            int cleared = progress.IndexOf(notice.levelId);
            int unlocked = progress.IndexOf(notice.unlockedLevelId);
            if (cleared >= 0 && cleared < nodes.Count)
                nodes[cleared].Flash(clearedColor);
            if (unlocked >= 0 && unlocked < nodes.Count)
                nodes[unlocked].Flash(accent);

            string message = $"LEVEL {cleared + 1} CLEARED!";
            LevelInfo next = progress.GetNextLevel(language);
            if (notice.firstClear && unlocked >= 0)
                message += $"  LEVEL {unlocked + 1} UNLOCKED: {levels[unlocked].displayName}";
            else if (next != null)
                message += $"  NEXT UP: LEVEL {levels.IndexOf(next) + 1} - {next.displayName}";
            else
                message += "  EVERY LEVEL IS CLEARED - GREAT DETECTIVE WORK!";
            ShowBanner(message, clearedColor);
        }

        // ---------- Level panel ----------

        /// <summary>Opens the panel of a level (a node was clicked or submitted).</summary>
        public void OpenLevel(int index)
        {
            if (busy || index < 0 || index >= levels.Count || levelPanel == null)
                return;

            LevelInfo level = levels[index];
            LevelMapState state = progress.GetState(language, level.levelId);
            bool canResume = state != LevelMapState.Locked && progress.CanResume(language, level.levelId);
            LevelInfo skipTarget = progress.GetSkipTarget(language, level.levelId);
            Color color = state == LevelMapState.Completed ? clearedColor : state == LevelMapState.Locked ? lockedColor : accent;

            panelIndex = index;
            lastFocused = index;
            panelNumberText.text = $"LEVEL {index + 1}  /  {language.ToDisplayName().ToUpperInvariant()}";
            panelNumberText.color = state == LevelMapState.Locked ? new Color(0.6f, 0.64f, 0.7f) : color;
            panelNameText.text = level.displayName;
            panelStatusText.text = StatusText(level, state);
            panelStatusText.color = state == LevelMapState.Locked ? new Color(0.6f, 0.64f, 0.7f) : color;
            if (panelAccent != null)
                panelAccent.color = color;
            if (panelIcon != null)
            {
                panelIcon.sprite = state == LevelMapState.Completed ? checkIcon : state == LevelMapState.Locked ? lockIcon : playIcon;
                panelIcon.color = state == LevelMapState.Locked ? new Color(0.6f, 0.64f, 0.7f) : color;
            }
            panelMessageText.text = PanelMessage(index, level, state, canResume, skipTarget);

            SetActive(continueButton, canResume);
            SetActive(playLevelButton, state != LevelMapState.Locked);
            if (playLevelLabel != null)
                playLevelLabel.text = state == LevelMapState.Completed ? "REPLAY LEVEL" : canResume ? "RESTART LEVEL" : "PLAY LEVEL";
            // Skip is only ever offered for a cleared level.
            SetActive(skipButton, state == LevelMapState.Completed && skipTarget != null);
            SetActive(backToMapButton, true);

            SetActive(levelPanel, true);
            if (mapGroup != null)
                mapGroup.interactable = false;
            Selectable first = canResume ? continueButton : state != LevelMapState.Locked ? playLevelButton : backToMapButton;
            UIFocus.Select(first);
        }

        string PanelMessage(int index, LevelInfo level, LevelMapState state, bool canResume, LevelInfo skipTarget)
        {
            string mission = string.IsNullOrWhiteSpace(level.mission) ? string.Empty : $"<color=#FACC15><b>MISSION:</b></color> {level.mission}\n";
            switch (state)
            {
                case LevelMapState.Locked:
                    return $"This level is locked.\nClear <b>Level {index} - {levels[index - 1].displayName}</b> first to unlock it.";
                case LevelMapState.Completed:
                    int stars = StarsFor(level);
                    string best = $"Best result: <color=#FACC15><b>{stars} of {AchievementManager.MaxStarsPerLevel} stars</b></color>" +
                                  (stars < AchievementManager.MaxStarsPerLevel ? ".\nEarn the rest with no hints and every lock solved on the first CHECK.\n" : ". Perfect!\n");
                    string replay = "Replay it to practise again";
                    return best + (skipTarget != null
                        ? $"{replay}, or skip ahead to <b>Level {levels.IndexOf(skipTarget) + 1} - {skipTarget.displayName}</b>."
                        : $"{replay}.");
                default:
                    return canResume
                        ? mission + "Your investigation here is saved. Continue where you left off, or restart the level."
                        : mission + $"{level.mapDescription}";
            }
        }

        static int StarsFor(LevelInfo level)
        {
            return AchievementManager.Instance != null ? AchievementManager.Instance.StarsFor(level.levelId) : 0;
        }

        /// <summary>Back to Map button (and Esc): closes the level panel.</summary>
        public void CloseLevelPanel()
        {
            if (!IsPanelOpen || busy)
                return;
            SetActive(levelPanel, false);
            if (mapGroup != null)
                mapGroup.interactable = true;
            FocusNode(lastFocused);
        }

        /// <summary>Continue button: resumes the saved progress inside the level.</summary>
        public void OnContinueClicked()
        {
            if (panelIndex >= 0)
                Begin(levels[panelIndex], true);
        }

        /// <summary>Play Level / Replay Level / Restart Level button: starts the level from its beginning.</summary>
        public void OnPlayLevelClicked()
        {
            if (panelIndex >= 0)
                Begin(levels[panelIndex], false);
        }

        /// <summary>Skip to Next Level button (cleared levels only).</summary>
        public void OnSkipClicked()
        {
            if (busy || panelIndex < 0)
                return;
            if (progress.SkipFrom(levels[panelIndex].levelId, out string reason))
                LockWhileLoading();
            else
                ShowBanner(reason, warningColor);
        }

        /// <summary>Play / Continue button under the map: starts (or continues) the next level.</summary>
        public void OnPlayClicked()
        {
            LevelInfo next = progress != null ? progress.GetNextLevel(language) : null;
            if (next != null)
                Begin(next, progress.CanResume(language, next.levelId));
        }

        /// <summary>Back button: returns to the main menu (there is no language screen; the game is Python only).</summary>
        public void OnBackClicked()
        {
            if (busy)
                return;
            busy = SceneTransitionManager.Load(SceneNames.MainMenu);
        }

        void Begin(LevelInfo level, bool resume)
        {
            if (busy)
                return;
            if (progress.StartLevel(level.levelId, resume, out string reason))
                LockWhileLoading();
            else
                ShowBanner(reason, warningColor);
        }

        void LockWhileLoading()
        {
            busy = true;
            if (mapGroup != null)
                mapGroup.interactable = false;
            foreach (Button b in new[] { continueButton, playLevelButton, skipButton, backToMapButton })
            {
                if (b != null)
                    b.interactable = false;
            }
        }

        // ---------- Helpers ----------

        void FocusNode(int index)
        {
            if (index >= 0 && index < nodes.Count && nodes[index].gameObject.activeSelf)
                UIFocus.Select(nodes[index].Button);
        }

        void ShowBanner(string message, Color color)
        {
            if (bannerText == null || string.IsNullOrEmpty(message))
                return;
            bannerText.text = message;
            bannerText.color = color;
            bannerUntil = Time.unscaledTime + bannerTime;
            SetBanner(1f);
        }

        void SetBanner(float alpha)
        {
            if (banner != null)
                banner.alpha = alpha;
        }

        Color AccentFor(ProgrammingLanguage lang)
        {
            switch (lang)
            {
                case ProgrammingLanguage.PHP: return phpColor;
                case ProgrammingLanguage.SQL: return sqlColor;
                default: return pythonColor;
            }
        }

        static void SetActive(Component target, bool active)
        {
            if (target != null && target.gameObject.activeSelf != active)
                target.gameObject.SetActive(active);
        }

        static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
                target.SetActive(active);
        }
    }
}
