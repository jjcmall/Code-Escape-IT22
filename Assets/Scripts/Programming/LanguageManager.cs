using System;
using UnityEngine;
using CodeEscape.SaveSystem;

namespace CodeEscape.Programming
{
    /// <summary>
    /// Holds the programming language chosen for this playthrough and makes it available globally.
    /// Lives on the persistent GameSystems object (created by GameBootstrapper).
    /// </summary>
    public class LanguageManager : MonoBehaviour
    {
        public static LanguageManager Instance { get; private set; }

        /// <summary>Raised whenever the player picks a language.</summary>
        public static event Action<ProgrammingLanguage> LanguageChanged;

        [SerializeField, Tooltip("Shown for debugging. Loaded from the save data on start-up.")]
        ProgrammingLanguage selectedLanguage = ProgrammingLanguage.None;

        public ProgrammingLanguage SelectedLanguage => selectedLanguage;
        public bool HasSelectedLanguage => selectedLanguage.IsValid();

        /// <summary>The selected language, falling back to the saved value if the manager does not exist.</summary>
        public static ProgrammingLanguage Current => Instance != null ? Instance.selectedLanguage : SaveManager.LoadLanguage();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // Domain reload is disabled in this project, so statics must be cleared manually.
            Instance = null;
            LanguageChanged = null;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // A PHP or SQL playthrough saved by an older version isn't playable any more: treat it as none,
            // so Continue doesn't resume it and starting a game picks Python.
            ProgrammingLanguage saved = SaveManager.LoadLanguage();
            selectedLanguage = saved.IsPlayable() ? saved : ProgrammingLanguage.None;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Applies the language restored from the cloud save. Unlike <see cref="SelectLanguage"/> it raises no
        /// <see cref="LanguageChanged"/>, which would start a new playthrough and reset progress.
        /// </summary>
        public void RestoreLanguage(ProgrammingLanguage language)
        {
            selectedLanguage = language.IsPlayable() ? language : ProgrammingLanguage.None;
            if (selectedLanguage.IsValid())
                SaveManager.SaveLanguage(selectedLanguage);
        }

        /// <summary>
        /// Makes sure the playable language (Python) is selected. Selecting it when another (or no) language
        /// is stored starts a new playthrough, exactly as choosing a language did on the old language screen.
        /// </summary>
        public static void EnsurePlayableLanguage()
        {
            if (Instance == null)
            {
                Debug.LogError("[LanguageManager] LanguageManager is missing, so Python cannot be selected.");
                return;
            }
            if (!Instance.selectedLanguage.IsPlayable())
                Instance.SelectLanguage(ProgrammingLanguage.Python);
        }

        public void SelectLanguage(ProgrammingLanguage language)
        {
            if (!language.IsPlayable())
            {
                Debug.LogWarning($"[LanguageManager] Ignored '{language}': only Python is playable.");
                return;
            }

            selectedLanguage = language;
            SaveManager.SaveLanguage(language);
            Debug.Log($"[LanguageManager] Selected language: {language.ToDisplayName()}");
            LanguageChanged?.Invoke(language);
        }
    }
}
