using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CodeEscape.Game;
using CodeEscape.Programming;
using CodeEscape.Rewards;

namespace CodeEscape.UI
{
    /// <summary>
    /// Displays the results gathered by ResultsManager: language, score, challenges, clues, hints,
    /// time played, completion status and rank. Its buttons forward to ResultsManager.
    /// </summary>
    public class ResultsUI : MonoBehaviour
    {
        [SerializeField] ResultsManager manager;

        [Header("Values")]
        [SerializeField] TMP_Text languageText;
        [SerializeField] TMP_Text scoreText;
        [SerializeField] TMP_Text hintsText;
        [SerializeField] TMP_Text cluesText;
        [SerializeField] TMP_Text challengesText;
        [SerializeField] TMP_Text timeText;
        [SerializeField] TMP_Text statusText;
        [SerializeField] TMP_Text rankText;
        [SerializeField, Tooltip("Achievements and stars; created under the status text when left empty.")]
        TMP_Text achievementsText;
        [SerializeField] Selectable firstSelected;

        [Header("Colours")]
        [SerializeField] Color completeColor = new Color(0.29f, 0.87f, 0.5f);
        [SerializeField] Color incompleteColor = new Color(0.98f, 0.8f, 0.08f);

        void Start()
        {
            if (manager == null)
                manager = FindAnyObjectByType<ResultsManager>();
            Show(manager != null ? manager.Results : ResultsManager.Capture());
            UIFocus.Select(firstSelected);
        }

        public void Show(ResultsData results)
        {
            languageText.text = results.language.IsValid() ? results.language.ToDisplayName() : "-";
            scoreText.text = results.score.ToString();
            hintsText.text = results.hintsUsed.ToString();
            cluesText.text = $"{results.cluesCollected} / {results.totalClues}";
            challengesText.text = $"{results.challengesCompleted} / {results.totalChallenges}";
            timeText.text = results.TimeText;
            if (statusText != null)
            {
                statusText.text = results.CompletionStatus;
                statusText.color = results.storyComplete ? completeColor : incompleteColor;
            }
            rankText.text = results.Rank;
            ShowAchievements();
        }

        // A line under the completion status: achievements earned and level stars (lifetime totals).
        void ShowAchievements()
        {
            AchievementManager achievements = AchievementManager.Instance;
            TMP_Text template = statusText != null ? statusText : rankText;
            if (achievements == null || template == null)
                return;
            if (achievementsText == null)
            {
                // One centred line just under the results box (the box's own rows are full).
                var box = (RectTransform)template.transform.parent;
                var holder = (RectTransform)box.parent;
                achievementsText = Instantiate(template, holder);
                achievementsText.name = "AchievementsText";
                var r = (RectTransform)achievementsText.transform;
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.pivot = new Vector2(0.5f, 1f);
                r.sizeDelta = new Vector2(box.rect.width, 28f);
                Vector3 bottomCentre = box.TransformPoint(new Vector3(box.rect.center.x, box.rect.yMin, 0f));
                Vector3 local = holder.InverseTransformPoint(bottomCentre);
                r.anchoredPosition = new Vector2(local.x - holder.rect.center.x, local.y - holder.rect.center.y - 8f);
                achievementsText.alignment = TextAlignmentOptions.Top;
                achievementsText.fontSize = 20f;
                achievementsText.enableAutoSizing = false;
                achievementsText.textWrappingMode = TextWrappingModes.NoWrap;
            }
            int levels = Mathf.Max(1, AchievementCatalog.LevelCount);
            achievementsText.text = $"ACHIEVEMENTS {achievements.UnlockedCount} / {achievements.Definitions.Count}     STARS {achievements.TotalStars} / {levels * AchievementManager.MaxStarsPerLevel}";
            achievementsText.color = new Color(0.98f, 0.8f, 0.2f);
        }

        /// <summary>Return to Main Menu button.</summary>
        public void OnMainMenuClicked()
        {
            if (manager != null)
                manager.ReturnToMainMenu();
            else
                SceneTransitionManager.Load(SceneNames.MainMenu);
        }

        /// <summary>Play Again button.</summary>
        public void OnPlayAgainClicked()
        {
            if (manager != null)
                manager.PlayAgain();
            else
            {
                LanguageManager.EnsurePlayableLanguage();
                SceneTransitionManager.Load(SceneNames.LevelMap);
            }
        }
    }
}
