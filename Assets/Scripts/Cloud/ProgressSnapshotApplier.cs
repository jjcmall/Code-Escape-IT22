using UnityEngine;
using CodeEscape.Game;
using CodeEscape.Programming;
using CodeEscape.Rewards;
using CodeEscape.SaveSystem;

namespace CodeEscape.Cloud
{
    /// <summary>
    /// Restores a progression snapshot loaded from Cloud Save into the game, through the existing save system and
    /// managers (language, level progress, playthrough state, achievements). The mirror image of
    /// ProgressRecordBuilder. No network and no UI; CloudProgressService decides when to apply.
    /// </summary>
    public static class ProgressSnapshotApplier
    {
        /// <summary>Replaces the local game state with the snapshot. record supplies the achievements already confirmed.</summary>
        public static void Apply(ProgressSnapshot snapshot, PlayerProgressData record)
        {
            ProgressSaveService saveService = ProgressSaveService.Instance;
            if (saveService != null)
                saveService.ResetProgress();

            foreach (LanguageLevelProgress entry in snapshot.level_progress)
            {
                var language = (ProgrammingLanguage)entry.language;
                if (!language.IsValid() || string.IsNullOrEmpty(entry.json))
                    continue;
                LevelProgressData data = JsonUtility.FromJson<LevelProgressData>(entry.json);
                if (data != null)
                    SaveManager.SaveLevelProgress(language, data);
            }

            var selected = (ProgrammingLanguage)snapshot.language;
            if (LanguageManager.Instance != null)
                LanguageManager.Instance.RestoreLanguage(selected);
            else if (selected.IsValid())
                SaveManager.SaveLanguage(selected);

            if (saveService != null && !string.IsNullOrEmpty(snapshot.progress_json))
            {
                ProgressData progress = JsonUtility.FromJson<ProgressData>(snapshot.progress_json);
                if (progress != null)
                    saveService.Restore(progress);
            }

            if (LevelProgressManager.Instance != null)
                LevelProgressManager.Instance.ReloadFromSave();

            // Achievements are lifetime progress: merge, never replace.
            if (AchievementManager.Instance != null)
                AchievementManager.Instance.MergeFromCloud(snapshot.achievements_json, record != null ? record.achievements : null);
        }
    }
}
