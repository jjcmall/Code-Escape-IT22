using System;
using System.Collections.Generic;
using UnityEngine;
using CodeEscape.Game;
using CodeEscape.Programming;
using CodeEscape.Rewards;
using CodeEscape.SaveSystem;

namespace CodeEscape.Cloud
{
    /// <summary>
    /// Turns the current game state into the progression record and the restore snapshot that are stored in
    /// Cloud Save: cleared and highest unlocked level, the score total and the achievements. It holds the rules for
    /// what the record says, and nothing else: no network, no save timing and no UI. CloudProgressService decides
    /// when to build and send it.
    /// </summary>
    public static class ProgressRecordBuilder
    {
        static readonly ProgrammingLanguage[] Languages = { ProgrammingLanguage.Python, ProgrammingLanguage.PHP, ProgrammingLanguage.SQL };

        /// <summary>
        /// Reads the current game state (flushed to the local save first) and merges it into the last known record.
        /// newestAchievement is the display name of the achievement unlocked by this build, or empty.
        /// </summary>
        public static void Build(PlayerProgressData lastKnown, string playerId,
                                 out PlayerProgressData record, out ProgressSnapshot snapshot, out string newestAchievement)
        {
            ProgressSaveService saveService = ProgressSaveService.Instance;
            if (saveService != null)
                saveService.SaveNow();

            record = lastKnown != null ? lastKnown.Clone() : new PlayerProgressData();
            record.Sanitize();
            record.version = PlayerProgressData.CurrentVersion;
            record.player_id = playerId;

            ProgrammingLanguage current = LanguageManager.Current;
            ProgressData progress = SaveManager.LoadProgress();
            snapshot = new ProgressSnapshot
            {
                language = (int)current,
                progress_json = progress != null ? JsonUtility.ToJson(progress) : string.Empty
            };

            LevelManager levels = LevelManager.Instance;
            LevelProgressManager levelProgress = LevelProgressManager.Instance;
            // Cleared levels mirror the current save (a progress reset clears them); score and achievements are lifetime totals.
            var completed = new HashSet<string>();
            int maxClearedInOneLanguage = 0;
            foreach (ProgrammingLanguage language in Languages)
            {
                LevelProgressData data = SaveManager.LoadLevelProgress(language);
                if (data == null)
                    continue;
                data.Sanitize(id => levels == null || levels.GetInfoById(id) != null);
                snapshot.level_progress.Add(new LanguageLevelProgress { language = (int)language, json = JsonUtility.ToJson(data) });
                foreach (string id in data.completedLevels)
                    completed.Add(id);
                maxClearedInOneLanguage = Mathf.Max(maxClearedInOneLanguage, data.completedLevels.Count);
            }
            record.completed_levels = OrderedLevelIds(completed, levels);

            // Current level: the level being played, else the last one the record named.
            string currentLevel = progress != null ? progress.activeLevel : string.Empty;
            if (string.IsNullOrEmpty(currentLevel) && progress != null && levels != null)
            {
                LevelInfo info = levels.GetInfo(progress.currentLevel);
                currentLevel = info != null ? info.levelId : string.Empty;
            }
            if (!string.IsNullOrEmpty(currentLevel))
                record.current_level = currentLevel;
            else if (string.IsNullOrEmpty(record.current_level) && levels != null && levels.Levels.Count > 0)
                record.current_level = levels.Levels[0].levelId;

            // Highest unlocked level in any language.
            if (levels != null && levelProgress != null)
            {
                int best = -1;
                for (int i = 0; i < levels.Levels.Count; i++)
                {
                    foreach (ProgrammingLanguage language in Languages)
                    {
                        if (levelProgress.IsUnlocked(language, levels.Levels[i].levelId))
                            best = Mathf.Max(best, i);
                    }
                }
                if (best >= 0)
                    record.highest_unlocked_level = levels.Levels[best].levelId;
            }

            // Score: every solved challenge and every hinted question is counted once, however often events fire.
            ProgrammingChallengeManager challenges = ProgrammingChallengeManager.Instance;
            if (challenges != null && current.IsValid())
            {
                var scored = new HashSet<string>(record.scored_challenges);
                // Doors opened with SHOW ANSWER earned no points, so they don't count here either.
                foreach (string obstacleId in challenges.ScoredObstacleIds)
                    scored.Add($"{current.ToId()}:{obstacleId}");
                var hinted = new HashSet<string>(record.penalised_hints);
                foreach (string questionId in challenges.HintedQuestionIds)
                    hinted.Add($"{current.ToId()}:{questionId}");
                record.scored_challenges = SortedList(scored);
                record.penalised_hints = SortedList(hinted);
            }
            record.total_score = Mathf.Max(0, record.scored_challenges.Count * ProgrammingChallengeManager.PointsPerCorrectAnswer
                                              - record.penalised_hints.Count * ProgrammingChallengeManager.HintPenalty);

            newestAchievement = UnlockAchievements(record, maxClearedInOneLanguage, levels != null ? levels.LevelCount : 0);
            if (AchievementManager.Instance != null)
                snapshot.achievements_json = AchievementManager.Instance.ExportJson();
        }

        static List<string> SortedList(HashSet<string> set)
        {
            var list = new List<string>(set);
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        static List<string> OrderedLevelIds(HashSet<string> ids, LevelManager levels)
        {
            var result = new List<string>();
            if (levels != null)
            {
                foreach (LevelInfo level in levels.Levels)
                {
                    if (ids.Remove(level.levelId))
                        result.Add(level.levelId);
                }
            }
            var rest = SortedList(ids); // IDs no longer in the level list are kept, not silently dropped.
            result.AddRange(rest);
            return result;
        }

        // Adds the achievements the record has earned and returns the name of the last one newly added (or empty).
        static string UnlockAchievements(PlayerProgressData record, int maxClearedInOneLanguage, int levelCount)
        {
            string newest = string.Empty;
            void Unlock(string id, bool earned)
            {
                if (earned && !record.achievements.Contains(id))
                {
                    record.achievements.Add(id);
                    newest = Achievements.NameOf(id);
                }
            }
            // Everything the achievement system has awarded (learning, code, story and concept badges).
            AchievementManager achievements = AchievementManager.Instance;
            if (achievements != null)
            {
                foreach (string id in achievements.Progress.unlocked)
                    Unlock(id, true);
            }
            Unlock(Achievements.FirstChallenge, record.scored_challenges.Count >= 1);
            Unlock(Achievements.FirstLevel, record.completed_levels.Count >= 1);
            Unlock(Achievements.FiveChallenges, record.scored_challenges.Count >= 5);
            Unlock(Achievements.Halfway, levelCount > 0 && maxClearedInOneLanguage * 2 >= levelCount);
            Unlock(Achievements.CaseClosed, levelCount > 0 && maxClearedInOneLanguage >= levelCount);
            return newest;
        }
    }
}
