using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using CodeEscape.Game;
using CodeEscape.Programming;

namespace CodeEscape.UI
{
    /// <summary>
    /// The old language select screen. The game is now Python only, so nothing opens this scene any more;
    /// if it is opened anyway it selects Python and goes straight on to the level map. PHP and SQL are
    /// planned for future versions (their buttons only lead to Python).
    /// </summary>
    public class LanguageSelectUI : MonoBehaviour
    {
        [SerializeField, Tooltip("Shows the previous choice, then the confirmation.")]
        TMP_Text selectionStatus;
        [SerializeField, Tooltip("Selected on start for keyboard navigation.")]
        Selectable firstSelected;

        bool choiceLocked;

        void Start()
        {
            UIFocus.Select(firstSelected);
            Choose(ProgrammingLanguage.Python);
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                BackToMainMenu();
        }

        public void SelectPython()
        {
            Choose(ProgrammingLanguage.Python);
        }

        public void SelectPHP()
        {
            Choose(ProgrammingLanguage.Python);
        }

        public void SelectSQL()
        {
            Choose(ProgrammingLanguage.Python);
        }

        public void BackToMainMenu()
        {
            if (choiceLocked)
                return;
            SceneTransitionManager.Load(SceneNames.MainMenu);
        }

        void Choose(ProgrammingLanguage language)
        {
            if (choiceLocked)
                return;
            if (LanguageManager.Instance == null)
            {
                Debug.LogError("[LanguageSelect] LanguageManager is missing, so the choice cannot be stored.");
                return;
            }

            // Picking the same language again keeps the playthrough, so a level in progress can be continued.
            if (LanguageManager.Current != language)
                LanguageManager.Instance.SelectLanguage(language);
            SetStatus($"{language.ToDisplayName()} selected!");
            choiceLocked = SceneTransitionManager.Load(SceneNames.LevelMap);
        }

        void SetStatus(string message)
        {
            if (selectionStatus != null)
                selectionStatus.text = message;
        }
    }
}
