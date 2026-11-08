using System;
using System.Collections.Generic;

namespace CodeEscape.SaveSystem
{
    /// <summary>Everything needed to continue a playthrough. Stored as JSON by SaveManager.</summary>
    [Serializable]
    public class ProgressData
    {
        /// <summary>First version that records <see cref="activeLevel"/> (saves from before the level map are version 1).</summary>
        public const int LevelMapVersion = 2;

        public int version = LevelMapVersion;
        public int language;
        public int storyStage;
        public string currentLevel;
        public string currentObjective;
        /// <summary>Level ID (not scene name) of the level started from the level map; empty when none is being played.</summary>
        public string activeLevel = string.Empty;
        public List<string> completedEvents = new List<string>();
        public List<string> unlockedLevels = new List<string>();
        public List<string> completedLevels = new List<string>();
        public List<string> completedChallenges = new List<string>();
        public List<string> completedQuestions = new List<string>();
        public List<string> hintedQuestions = new List<string>();
        /// <summary>Obstacles opened with SHOW ANSWER: solved, but they earned no points.</summary>
        public List<string> unscoredChallenges = new List<string>();
        public List<string> collectedClues = new List<string>();
        public int score;
        public int hintsUsed;
        public float timePlayed;

        const int MaxListEntries = 1000;
        const int MaxTextLength = 200;

        /// <summary>
        /// Repairs a playthrough read from a save or from the cloud before the game uses it: missing lists become empty,
        /// empty, duplicate and over-long IDs are dropped, lists are capped, numbers can't be negative or invalid, an
        /// unknown language becomes "none", an unknown level scene becomes <paramref name="fallbackScene"/> and an
        /// unknown active level is cleared. A null check is skipped. Returns true when anything had to be repaired.
        /// </summary>
        public bool Sanitize(Predicate<string> isKnownScene, Predicate<string> isKnownLevelId, string fallbackScene)
        {
            bool repaired = false;
            completedEvents = CleanList(completedEvents, ref repaired);
            unlockedLevels = CleanList(unlockedLevels, ref repaired);
            completedLevels = CleanList(completedLevels, ref repaired);
            completedChallenges = CleanList(completedChallenges, ref repaired);
            completedQuestions = CleanList(completedQuestions, ref repaired);
            hintedQuestions = CleanList(hintedQuestions, ref repaired);
            unscoredChallenges = CleanList(unscoredChallenges, ref repaired);
            collectedClues = CleanList(collectedClues, ref repaired);

            if (score < 0 || hintsUsed < 0 || storyStage < 0)
            {
                score = Math.Max(0, score);
                hintsUsed = Math.Max(0, hintsUsed);
                storyStage = Math.Max(0, storyStage);
                repaired = true;
            }
            if (float.IsNaN(timePlayed) || float.IsInfinity(timePlayed) || timePlayed < 0f)
            {
                timePlayed = 0f;
                repaired = true;
            }
            if (!Enum.IsDefined(typeof(Programming.ProgrammingLanguage), language))
            {
                language = (int)Programming.ProgrammingLanguage.None;
                repaired = true;
            }
            if (currentObjective != null && currentObjective.Length > MaxTextLength)
            {
                currentObjective = currentObjective.Substring(0, MaxTextLength);
                repaired = true;
            }
            if (isKnownScene != null && !string.IsNullOrEmpty(currentLevel) && !isKnownScene(currentLevel))
            {
                currentLevel = fallbackScene;
                repaired = true;
            }
            activeLevel ??= string.Empty;
            if (isKnownLevelId != null && activeLevel.Length > 0 && !isKnownLevelId(activeLevel))
            {
                activeLevel = string.Empty;
                repaired = true;
            }
            return repaired;
        }

        static List<string> CleanList(List<string> list, ref bool repaired)
        {
            if (list == null)
            {
                repaired = true;
                return new List<string>();
            }
            var seen = new HashSet<string>();
            int removed = list.RemoveAll(id => string.IsNullOrEmpty(id) || id.Length > MaxTextLength || !seen.Add(id));
            if (list.Count > MaxListEntries)
            {
                list.RemoveRange(MaxListEntries, list.Count - MaxListEntries);
                removed++;
            }
            if (removed > 0)
                repaired = true;
            return list;
        }
    }
}
