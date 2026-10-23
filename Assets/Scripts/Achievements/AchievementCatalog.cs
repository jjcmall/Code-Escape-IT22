using System;
using System.Collections.Generic;
using UnityEngine;
using CodeEscape.Programming;

namespace CodeEscape.Rewards
{
    /// <summary>Lifetime counters behind the achievements, badges and level stars. Stored as JSON (SaveManager).</summary>
    [Serializable]
    public class AchievementProgress
    {
        public int version = 1;
        public List<string> unlocked = new List<string>();
        /// <summary>IDs of questions solved at code locks (doors), each counted once.</summary>
        public List<string> solvedDoors = new List<string>();
        public List<string> clues = new List<string>();
        public int runs;
        public int firstTries;
        public int debugs;
        public int escapes;
        public int predictStreak;
        public int bestPredictStreak;
        public int hintFreeLevels;
        public List<LevelStars> stars = new List<LevelStars>();

        public void Sanitize()
        {
            unlocked ??= new List<string>();
            solvedDoors ??= new List<string>();
            clues ??= new List<string>();
            stars ??= new List<LevelStars>();
        }

        public int StarsFor(string levelId)
        {
            foreach (LevelStars s in stars)
                if (s != null && s.levelId == levelId)
                    return s.stars;
            return 0;
        }

        /// <summary>Keeps the best result per level. Returns true when it improved.</summary>
        public bool SetStars(string levelId, int value)
        {
            foreach (LevelStars s in stars)
            {
                if (s != null && s.levelId == levelId)
                {
                    if (value <= s.stars)
                        return false;
                    s.stars = value;
                    return true;
                }
            }
            stars.Add(new LevelStars { levelId = levelId, stars = value });
            return true;
        }

        public int TotalStars()
        {
            int total = 0;
            foreach (LevelStars s in stars)
                if (s != null)
                    total += s.stars;
            return total;
        }

        /// <summary>Combines another device's progress with this one (union of lists, the higher of each count).</summary>
        public void MergeFrom(AchievementProgress other)
        {
            if (other == null)
                return;
            other.Sanitize();
            Union(unlocked, other.unlocked);
            Union(solvedDoors, other.solvedDoors);
            Union(clues, other.clues);
            runs = Mathf.Max(runs, other.runs);
            firstTries = Mathf.Max(firstTries, other.firstTries);
            debugs = Mathf.Max(debugs, other.debugs);
            escapes = Mathf.Max(escapes, other.escapes);
            bestPredictStreak = Mathf.Max(bestPredictStreak, other.bestPredictStreak);
            hintFreeLevels = Mathf.Max(hintFreeLevels, other.hintFreeLevels);
            foreach (LevelStars s in other.stars)
                if (s != null && !string.IsNullOrEmpty(s.levelId))
                    SetStars(s.levelId, s.stars);
        }

        static void Union(List<string> into, List<string> from)
        {
            foreach (string id in from)
                if (!string.IsNullOrEmpty(id) && !into.Contains(id))
                    into.Add(id);
        }
    }

    [Serializable]
    public class LevelStars
    {
        public string levelId;
        public int stars;
    }

    public enum AchievementCategory
    {
        Progress,
        Learning,
        Code,
        Story,
        Concept
    }

    /// <summary>One achievement: what it is called, how to earn it, and how far the player has got.</summary>
    public class AchievementDefinition
    {
        public string Id;
        public string Name;
        public string Description;
        public AchievementCategory Category;
        /// <summary>The count needed (1 for a one-off achievement).</summary>
        public int Target = 1;
        /// <summary>Current count towards <see cref="Target"/>.</summary>
        public Func<AchievementProgress, int> Progress = p => 0;
    }

    /// <summary>
    /// Every achievement in the game. They reward learning and progress (first tries, fixing code after
    /// feedback, playing without hints, testing code, mastering concepts), never speed or failing.
    /// </summary>
    public static class AchievementCatalog
    {
        public const string FirstChallenge = "first_challenge";
        public const string FiveChallenges = "five_challenges";
        public const string FirstLevel = "first_level";
        public const string Halfway = "halfway";
        public const string CaseClosed = "case_closed";
        public const string FirstTry = "first_try";
        public const string SharpMind = "sharp_mind";
        public const string Debugger = "debugger";
        public const string HintFree = "hint_free";
        public const string PerfectLevel = "perfect_level";
        public const string TestPilot = "test_pilot";
        public const string PredictionPro = "prediction_pro";
        public const string EscapeArtist = "escape_artist";
        public const string ClueCollector = "clue_collector";
        public const string ConceptPrefix = "concept_";

        /// <summary>Level counts come from the level list (set by the manager); 6 is the default.</summary>
        public static int LevelCount = 6;
        /// <summary>Levels cleared in the playable language (set by the manager).</summary>
        public static Func<int> ClearedLevels = () => 0;

        static List<AchievementDefinition> fixedList;

        public static IReadOnlyList<AchievementDefinition> Fixed
        {
            get
            {
                if (fixedList == null)
                    fixedList = BuildFixed();
                return fixedList;
            }
        }

        static List<AchievementDefinition> BuildFixed()
        {
            return new List<AchievementDefinition>
            {
                Make(FirstChallenge, "First Code Cracked", "Solve a code lock.", AchievementCategory.Progress, 1, p => p.solvedDoors.Count),
                Make(FiveChallenges, "Lock Breaker", "Solve 5 different code locks.", AchievementCategory.Progress, 5, p => p.solvedDoors.Count),
                Make(FirstLevel, "Level Cleared", "Clear your first level.", AchievementCategory.Progress, 1, p => ClearedLevels()),
                Make(Halfway, "Halfway Through the Case", $"Clear {Mathf.Max(1, LevelCount / 2)} levels.", AchievementCategory.Progress, Mathf.Max(1, LevelCount / 2), p => ClearedLevels()),
                Make(CaseClosed, "Case Closed", "Bring Sam home.", AchievementCategory.Progress, Mathf.Max(1, LevelCount), p => ClearedLevels()),
                Make(FirstTry, "First Try", "Solve a lock on your first CHECK.", AchievementCategory.Learning, 1, p => p.firstTries),
                Make(SharpMind, "Sharp Mind", "Solve 10 code locks on the first try.", AchievementCategory.Learning, 10, p => p.firstTries),
                Make(Debugger, "Debugger", "Fix an answer after feedback.", AchievementCategory.Learning, 1, p => p.debugs),
                Make(HintFree, "Hint-Free Hero", "Clear a level with no hints.", AchievementCategory.Learning, 1, p => p.hintFreeLevels),
                Make(PerfectLevel, "Perfect Run", "Earn 3 stars on a level.", AchievementCategory.Learning, 1, p => MaxStars(p) >= 3 ? 1 : 0),
                Make(TestPilot, "Test Pilot", "Press RUN 10 times to test code.", AchievementCategory.Code, 10, p => p.runs),
                Make(PredictionPro, "Prediction Pro", "Predict 5 outputs in a row.", AchievementCategory.Code, 5, p => Mathf.Max(p.bestPredictStreak, p.predictStreak)),
                Make(EscapeArtist, "Escape Artist", "Escape a hazard in one answer.", AchievementCategory.Story, 1, p => p.escapes),
                Make(ClueCollector, "Clue Collector", "Collect 5 clues.", AchievementCategory.Story, 5, p => p.clues.Count),
            };
        }

        static int MaxStars(AchievementProgress p)
        {
            int best = 0;
            foreach (LevelStars s in p.stars)
                if (s != null)
                    best = Mathf.Max(best, s.stars);
            return best;
        }

        static AchievementDefinition Make(string id, string name, string description, AchievementCategory category, int target, Func<AchievementProgress, int> progress)
        {
            return new AchievementDefinition { Id = id, Name = name, Description = description, Category = category, Target = target, Progress = progress };
        }

        /// <summary>A concept badge for a lesson: earned by answering one of its challenges correctly.</summary>
        public static AchievementDefinition ForLesson(LessonData lesson)
        {
            string id = ConceptPrefix + lesson.Id;
            return new AchievementDefinition
            {
                Id = id,
                Name = $"{lesson.ConceptTitle} Badge",
                Description = $"Answer a {lesson.ConceptTitle} challenge correctly.",
                Category = AchievementCategory.Concept,
                Target = 1,
                Progress = p => p.unlocked.Contains(id) ? 1 : 0
            };
        }

        /// <summary>Every achievement: the fixed ones, then a concept badge per playable lesson.</summary>
        public static List<AchievementDefinition> All(QuestionDatabase database)
        {
            fixedList = BuildFixed();
            var all = new List<AchievementDefinition>(fixedList);
            if (database != null)
            {
                foreach (LessonData lesson in database.Lessons)
                    if (lesson != null && lesson.Language.IsPlayable())
                        all.Add(ForLesson(lesson));
            }
            return all;
        }

        /// <summary>Player-facing name of an achievement ID (also for IDs saved by older versions).</summary>
        public static string NameOf(string id, QuestionDatabase database = null)
        {
            foreach (AchievementDefinition d in Fixed)
                if (d.Id == id)
                    return d.Name;
            if (id != null && id.StartsWith(ConceptPrefix) && database != null)
            {
                foreach (LessonData lesson in database.Lessons)
                    if (lesson != null && ConceptPrefix + lesson.Id == id)
                        return $"{lesson.ConceptTitle} Badge";
            }
            return id;
        }
    }
}
