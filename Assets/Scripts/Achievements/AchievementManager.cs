using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using CodeEscape.Audio;
using CodeEscape.Clues;
using CodeEscape.Cloud;
using CodeEscape.Game;
using CodeEscape.Programming;
using CodeEscape.SaveSystem;

namespace CodeEscape.Rewards
{
    /// <summary>
    /// Awards achievements, concept badges and level stars from the game's existing events (answers, hints,
    /// RUN, clues, cleared levels), saves them as lifetime progress (kept across new games, copied to Cloud
    /// Save), and shows a short toast when one is earned. Lives on the persistent GameSystems object.
    ///
    /// Level stars: 1 for clearing the level, +1 if no hint was used in it, +1 if every code lock in it was
    /// solved on the first CHECK.
    /// </summary>
    public class AchievementManager : MonoBehaviour
    {
        public const int MaxStarsPerLevel = 3;

        public static AchievementManager Instance { get; private set; }

        /// <summary>Raised when an achievement or badge is earned.</summary>
        public static event Action<AchievementDefinition> Unlocked;
        /// <summary>Raised when a level's best star count improves (level ID, stars).</summary>
        public static event Action<string, int> StarsImproved;

        [Header("Toast")]
        [SerializeField] TMP_FontAsset headerFont;
        [SerializeField] TMP_FontAsset bodyFont;
        [SerializeField] Color accentColor = new Color(0.98f, 0.8f, 0.2f);
        [SerializeField, Min(0.5f)] float toastSeconds = 3.2f;

        AchievementProgress progress = new AchievementProgress();
        List<AchievementDefinition> definitions;

        // The open challenge.
        int attempts;
        bool challengeIsDoor;

        // The level being played.
        bool levelHinted;
        bool levelMissedLock;

        readonly Queue<AchievementDefinition> toastQueue = new Queue<AchievementDefinition>();
        Coroutine toastRoutine;
        CanvasGroup toastGroup;
        RectTransform toastPanel;
        TMP_Text toastName;
        TMP_Text toastDescription;

        public AchievementProgress Progress => progress;
        public int UnlockedCount => progress.unlocked.Count;

        public IReadOnlyList<AchievementDefinition> Definitions
        {
            get
            {
                if (definitions == null)
                    definitions = AchievementCatalog.All(Database);
                return definitions;
            }
        }

        static QuestionDatabase Database => ProgrammingChallengeManager.Instance != null ? ProgrammingChallengeManager.Instance.Database : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            Unlocked = null;
            StarsImproved = null;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Load();
            AchievementCatalog.ClearedLevels = ClearedLevelCount;
        }

        void OnEnable()
        {
            ProgrammingChallengeManager.ChallengeOpened += HandleChallengeOpened;
            ProgrammingChallengeManager.AnswerChecked += HandleAnswerChecked;
            ProgrammingChallengeManager.HintShown += HandleHintShown;
            ProgrammingChallengeManager.CodeRun += HandleCodeRun;
            ClueManager.ClueCollected += HandleClueCollected;
            LevelProgressManager.LevelCleared += HandleLevelCleared;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        void OnDisable()
        {
            ProgrammingChallengeManager.ChallengeOpened -= HandleChallengeOpened;
            ProgrammingChallengeManager.AnswerChecked -= HandleAnswerChecked;
            ProgrammingChallengeManager.HintShown -= HandleHintShown;
            ProgrammingChallengeManager.CodeRun -= HandleCodeRun;
            ClueManager.ClueCollected -= HandleClueCollected;
            LevelProgressManager.LevelCleared -= HandleLevelCleared;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        void Start()
        {
            if (LevelManager.Instance != null)
                AchievementCatalog.LevelCount = LevelManager.Instance.LevelCount;
            definitions = null;
            // Progress made before this feature existed (cleared levels) counts straight away, without toasts.
            CheckAll(announce: false);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ---------- Queries ----------

        public bool IsUnlocked(string id) => progress.unlocked.Contains(id);

        public int StarsFor(string levelId) => progress.StarsFor(levelId);

        public int TotalStars => progress.TotalStars();

        public int ProgressOf(AchievementDefinition definition)
        {
            if (IsUnlocked(definition.Id))
                return definition.Target;
            return Mathf.Clamp(definition.Progress(progress), 0, definition.Target);
        }

        // ---------- Cloud Save ----------

        public string ExportJson() => JsonUtility.ToJson(progress);

        /// <summary>Adds achievements restored from Cloud Save (their counters and the record's ID list) to the local ones.</summary>
        public void MergeFromCloud(string json, IEnumerable<string> recordAchievements)
        {
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    progress.MergeFrom(JsonUtility.FromJson<AchievementProgress>(json));
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Achievements] Cloud achievements could not be read: {e.Message}");
                }
            }
            if (recordAchievements != null)
            {
                foreach (string id in recordAchievements)
                    if (!string.IsNullOrEmpty(id) && !progress.unlocked.Contains(id))
                        progress.unlocked.Add(id);
            }
            Save();
        }

        // ---------- Events ----------

        void HandleChallengeOpened(QuestionData question)
        {
            attempts = 0;
            ProgrammingChallengeManager manager = ProgrammingChallengeManager.Instance;
            challengeIsDoor = manager != null && manager.ActiveSource != null && !manager.ActiveSource.IsMandatory;
        }

        void HandleAnswerChecked(bool correct)
        {
            ProgrammingChallengeManager manager = ProgrammingChallengeManager.Instance;
            QuestionData question = manager != null ? manager.ActiveQuestion : null;
            if (question == null)
                return;

            // An answer shown with SHOW ANSWER opens the way but earns no achievement, badge or streak.
            if (correct && manager.ActiveAnswerRevealed)
            {
                if (challengeIsDoor)
                    levelMissedLock = true;
                Save();
                return;
            }

            if (question.ChecksByRunning)
            {
                progress.predictStreak = correct ? progress.predictStreak + 1 : 0;
                progress.bestPredictStreak = Mathf.Max(progress.bestPredictStreak, progress.predictStreak);
            }

            if (!correct)
            {
                attempts++;
                if (challengeIsDoor)
                    levelMissedLock = true;
                Save();
                return;
            }

            if (challengeIsDoor)
            {
                if (!progress.solvedDoors.Contains(question.Id))
                    progress.solvedDoors.Add(question.Id);
                if (attempts == 0)
                    progress.firstTries++;
            }
            else if (attempts == 0)
            {
                progress.escapes++;
            }
            if (attempts > 0)
                progress.debugs++;

            LessonData lesson = manager.ActiveLesson;
            if (lesson != null && lesson.Language.IsPlayable())
                Unlock(AchievementCatalog.ConceptPrefix + lesson.Id, true);

            Save();
            CheckAll(announce: true);
        }

        void HandleHintShown()
        {
            levelHinted = true;
        }

        void HandleCodeRun(bool ranWithoutError)
        {
            progress.runs++;
            Save();
            CheckAll(announce: true);
        }

        void HandleClueCollected(ClueData clue)
        {
            if (clue == null || string.IsNullOrEmpty(clue.Id) || progress.clues.Contains(clue.Id))
                return;
            progress.clues.Add(clue.Id);
            Save();
            CheckAll(announce: true);
        }

        void HandleLevelCleared(ProgrammingLanguage language, LevelInfo cleared, LevelInfo next)
        {
            if (cleared == null || !language.IsPlayable())
                return;
            int stars = 1 + (levelHinted ? 0 : 1) + (levelMissedLock ? 0 : 1);
            if (!levelHinted)
                progress.hintFreeLevels++;
            if (progress.SetStars(cleared.levelId, stars))
                StarsImproved?.Invoke(cleared.levelId, stars);
            Debug.Log($"[Achievements] Level '{cleared.levelId}' cleared with {stars} star(s) (hint used: {levelHinted}, missed a lock: {levelMissedLock}).");
            ResetLevelRun();
            Save();
            CheckAll(announce: true);
        }

        void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Entering a level scene starts a new run of that level (stars are judged per run).
            if (mode == LoadSceneMode.Single && LevelManager.Instance != null && LevelManager.Instance.GetInfo(scene.name) != null)
                ResetLevelRun();
        }

        void ResetLevelRun()
        {
            levelHinted = false;
            levelMissedLock = false;
        }

        // ---------- Unlocking ----------

        void CheckAll(bool announce)
        {
            foreach (AchievementDefinition definition in Definitions)
            {
                if (definition.Category == AchievementCategory.Concept || IsUnlocked(definition.Id))
                    continue;
                if (definition.Progress(progress) >= definition.Target)
                    Unlock(definition.Id, announce);
            }
        }

        void Unlock(string id, bool announce)
        {
            if (string.IsNullOrEmpty(id) || progress.unlocked.Contains(id))
                return;
            progress.unlocked.Add(id);
            Save();

            AchievementDefinition definition = Find(id);
            Debug.Log($"[Achievements] Unlocked '{id}'");
            if (definition != null)
            {
                Unlocked?.Invoke(definition);
                if (announce)
                    ShowToast(definition);
            }
            if (CloudProgressService.Instance != null)
                CloudProgressService.Instance.RequestSave(false);
        }

        AchievementDefinition Find(string id)
        {
            foreach (AchievementDefinition definition in Definitions)
                if (definition.Id == id)
                    return definition;
            return null;
        }

        static int ClearedLevelCount()
        {
            LevelProgressManager levels = LevelProgressManager.Instance;
            return levels != null ? levels.CompletedCount(ProgrammingLanguage.Python) : 0;
        }

        void Load()
        {
            string json = SaveManager.LoadAchievementsJson();
            progress = new AchievementProgress();
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    progress = JsonUtility.FromJson<AchievementProgress>(json) ?? new AchievementProgress();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Achievements] Saved achievements could not be read and were reset: {e.Message}");
                    progress = new AchievementProgress();
                }
            }
            progress.Sanitize();
        }

        void Save()
        {
            SaveManager.SaveAchievementsJson(JsonUtility.ToJson(progress));
        }

        // ---------- Toast ----------

        void ShowToast(AchievementDefinition definition)
        {
            toastQueue.Enqueue(definition);
            if (toastRoutine == null && isActiveAndEnabled)
                toastRoutine = StartCoroutine(RunToasts());
        }

        IEnumerator RunToasts()
        {
            EnsureToast();
            while (toastQueue.Count > 0)
            {
                AchievementDefinition d = toastQueue.Dequeue();
                toastName.text = $"<noparse>{d.Name}</noparse>";
                toastDescription.text = $"<noparse>{d.Description}</noparse>";
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlaySfx(SfxType.Clue);

                for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
                {
                    SetToast(t / 0.25f);
                    yield return null;
                }
                SetToast(1f);
                for (float t = 0f; t < toastSeconds; t += Time.unscaledDeltaTime)
                    yield return null;
                for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
                {
                    SetToast(1f - t / 0.25f);
                    yield return null;
                }
                SetToast(0f);
            }
            toastRoutine = null;
        }

        void SetToast(float amount)
        {
            float eased = 1f - (1f - amount) * (1f - amount);
            toastGroup.alpha = eased;
            // Slides in from the right edge, below the top corner (clear of the centred CAUGHT banner).
            toastPanel.anchoredPosition = new Vector2(Mathf.Lerp(700f, -28f, eased), -96f);
        }

        // Built in code so it needs no scene setup: a banner sliding in at the top of the screen, above every other UI.
        void EnsureToast()
        {
            if (toastGroup != null)
                return;

            var canvasObject = new GameObject("AchievementToast", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 95;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            toastGroup = canvasObject.GetComponent<CanvasGroup>();
            toastGroup.blocksRaycasts = false;
            toastGroup.interactable = false;
            toastGroup.alpha = 0f;

            toastPanel = NewRect("Panel", canvasObject.transform);
            toastPanel.anchorMin = toastPanel.anchorMax = new Vector2(1f, 1f);
            toastPanel.pivot = new Vector2(1f, 1f);
            toastPanel.sizeDelta = new Vector2(660f, 104f);
            AddImage(toastPanel.gameObject, new Color(0.035f, 0.06f, 0.1f, 0.96f));

            RectTransform border = NewRect("Border", toastPanel);
            Stretch(border, -2f);
            var borderImage = AddImage(border.gameObject, accentColor);
            borderImage.transform.SetAsFirstSibling();
            var outline = border.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.35f);
            outline.effectDistance = new Vector2(4f, 4f);
            RectTransform inner = NewRect("Inner", toastPanel);
            Stretch(inner, 2f);
            AddImage(inner.gameObject, new Color(0.035f, 0.06f, 0.1f, 1f));

            // Diamond badge.
            RectTransform badge = NewRect("Badge", toastPanel);
            badge.anchorMin = badge.anchorMax = new Vector2(0f, 0.5f);
            badge.anchoredPosition = new Vector2(58f, 0f);
            badge.sizeDelta = new Vector2(46f, 46f);
            badge.localRotation = Quaternion.Euler(0f, 0f, 45f);
            AddImage(badge.gameObject, accentColor);
            RectTransform badgeInner = NewRect("BadgeInner", badge);
            badgeInner.sizeDelta = new Vector2(22f, 22f);
            AddImage(badgeInner.gameObject, new Color(0.035f, 0.06f, 0.1f, 1f));

            TMP_Text header = AddText("Header", toastPanel, headerFont, 17f, accentColor, new Vector2(112f, -14f), new Vector2(520f, 24f));
            header.text = "ACHIEVEMENT UNLOCKED";
            header.characterSpacing = 4f;
            toastName = AddText("Name", toastPanel, headerFont, 27f, new Color(0.95f, 0.97f, 1f), new Vector2(112f, -38f), new Vector2(530f, 34f));
            toastDescription = AddText("Description", toastPanel, bodyFont, 19f, new Color(0.68f, 0.74f, 0.82f), new Vector2(112f, -72f), new Vector2(530f, 26f));
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        static Image AddImage(GameObject go, Color color)
        {
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static TMP_Text AddText(string name, Transform parent, TMP_FontAsset font, float size, Color color, Vector2 topLeft, Vector2 dimensions)
        {
            RectTransform rect = NewRect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = topLeft;
            rect.sizeDelta = dimensions;
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null)
                text.font = font;
            text.fontSize = size;
            text.color = color;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Truncate;
            text.alignment = TextAlignmentOptions.TopLeft;
            return text;
        }
    }
}
