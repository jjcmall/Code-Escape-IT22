using TMPro;
using CodeEscape.Execution;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using CodeEscape.Programming;

namespace CodeEscape.UI
{
    /// <summary>
    /// Window for programming challenges. When the question has a lesson, a learning card (concept,
    /// definition, how it works, an example and what it does) is shown first; Continue opens the question,
    /// and Review Concept brings the card back at any time without resetting the challenge. Then: question,
    /// code, choices or typed answer, hint and result. It only displays things - every button forwards to
    /// ProgrammingChallengeManager, which owns the logic. When a hazard has caught Alex it shows the CAUGHT
    /// banner and hides Close (Esc does nothing): only a correct answer ends it.
    /// </summary>
    public class ChallengeUI : MonoBehaviour
    {
        const string CodeFormat = "<mspace=0.66em><noparse>{0}</noparse></mspace>";
        static readonly string[] Letters = { "A", "B", "C", "D" };

        [SerializeField] ProgrammingChallengeManager manager;
        [SerializeField, Tooltip("Shown while a challenge is open. Keep this script's own GameObject active.")]
        GameObject overlay;

        [Header("Header")]
        [SerializeField] TMP_Text tagsText;
        [SerializeField] TMP_Text questionTypeText;
        [SerializeField] TMP_Text scoreText;

        [Header("Question")]
        [SerializeField] TMP_Text questionText;
        [SerializeField] GameObject codePanel;
        [SerializeField] TMP_Text codeText;

        [Header("Answer")]
        [SerializeField] GameObject choicesRoot;
        [SerializeField] Button[] choiceButtons = new Button[QuestionData.MaxChoices];
        [SerializeField] TMP_Text[] choiceLabels = new TMP_Text[QuestionData.MaxChoices];
        [SerializeField] GameObject typedAnswerRoot;
        [SerializeField] TMP_InputField answerInput;
        [SerializeField, Tooltip("The line above the input box (e.g. 'Type your answer').")]
        TMP_Text answerPrompt;
        [SerializeField, Tooltip("Run Code questions: runs the typed code and shows what it prints (does not count as an answer).")]
        Button runButton;
        [SerializeField, Tooltip("Run Code questions: the console that shows the printed output or an error.")]
        GameObject outputPanel;
        [SerializeField] TMP_Text outputText;

        [Header("Result")]
        [SerializeField] GameObject resultRoot;
        [SerializeField] TMP_Text resultTitleText;
        [SerializeField] TMP_Text explanationText;
        [SerializeField] TMP_Text clueRewardText;

        [Header("Hint")]
        [SerializeField] GameObject hintPanel;
        [SerializeField] TMP_Text hintText;

        [Header("Buttons")]
        [SerializeField] Button hintButton;
        [SerializeField] TMP_Text hintButtonLabel;
        [SerializeField] Button submitButton;
        [SerializeField] Button retryButton;
        [SerializeField] Button continueButton;
        [SerializeField] TMP_Text continueLabel;
        [SerializeField, Tooltip("Hidden while Alex is caught: the challenge must be solved.")]
        Button closeButton;
        [SerializeField, Tooltip("On the question screen: shows the learning card again without resetting the challenge.")]
        Button reviewButton;

        [Header("Learning card (shown before the question)")]
        [SerializeField, Tooltip("Covers the question while the lesson is on screen.")]
        GameObject lessonPanel;
        [SerializeField] TMP_Text lessonBadgeText;
        [SerializeField] TMP_Text lessonSymbolText;
        [SerializeField] TMP_Text lessonTitleText;
        [SerializeField] TMP_Text lessonDefinitionText;
        [SerializeField] TMP_Text lessonHowText;
        [SerializeField, Tooltip("Example heading, code panel and explanation; hidden when the lesson has no example.")]
        GameObject lessonExampleRoot;
        [SerializeField] TMP_Text lessonCodeText;
        [SerializeField] TMP_Text lessonExampleText;
        [SerializeField] GameObject lessonTipRoot;
        [SerializeField] TMP_Text lessonTipText;
        [SerializeField] Button lessonContinueButton;
        [SerializeField] TMP_Text lessonContinueLabel;

        [Header("Caught by a hazard")]
        [SerializeField, Tooltip("\"CAUGHT! SOLVE THE CODE TO ESCAPE!\" - shown only for hazard challenges.")]
        GameObject caughtBanner;
        [SerializeField] TMP_Text caughtText;
        [SerializeField, Tooltip("Window border and glow, tinted red while Alex is caught.")]
        Graphic windowBorder;
        [SerializeField] Graphic windowGlow;
        [SerializeField] Color caughtColor = new Color(0.97f, 0.27f, 0.27f);

        [Header("Colours")]
        [SerializeField] Color choiceColor = new Color(0.12f, 0.23f, 0.37f);
        [SerializeField] Color selectedChoiceColor = new Color(0.15f, 0.39f, 0.92f);
        [SerializeField] Color correctColor = new Color(0.29f, 0.87f, 0.5f);
        [SerializeField] Color incorrectColor = new Color(0.97f, 0.44f, 0.44f);

        const string CaughtMessage = "CAUGHT!  SOLVE THE CODE TO ESCAPE!";
        const string ReviewReminder = "\n<size=85%><color=#8B98A5>Need a reminder? Press REVIEW CONCEPT to read the lesson again.</color></size>";

        QuestionData question;
        LessonData lesson;
        int selectedChoice = -1;
        bool hintShown;
        bool caught;
        bool questionShown;
        string continueDefault;
        string submitDefault;
        string promptDefault;
        string placeholderDefault;
        Vector2 inputDefaultPosition;
        Vector2 inputDefaultSize;
        Color borderDefault;
        Color glowDefault;

        public bool IsOpen => overlay != null && overlay.activeSelf;
        public int SelectedChoice => selectedChoice;
        public bool IsCaught => IsOpen && caught;
        /// <summary>True while the learning card is on screen (before the question, or while reviewing).</summary>
        public bool IsLessonOpen => IsOpen && lessonPanel != null && lessonPanel.activeSelf;
        public LessonData Lesson => lesson;

        ProgrammingChallengeManager Manager => manager != null ? manager : ProgrammingChallengeManager.Instance;
        bool IsRunCode => question != null && question.AnswerFormat == AnswerFormat.RunCode;
        bool IsTyped => question != null && (question.AnswerFormat == AnswerFormat.TypedAnswer || question.AnswerFormat == AnswerFormat.RunCode);

        void Awake()
        {
            if (overlay != null)
                overlay.SetActive(false);
            if (continueLabel != null)
                continueDefault = continueLabel.text;
            if (submitButton != null && submitButton.GetComponentInChildren<TMP_Text>(true) != null)
                submitDefault = submitButton.GetComponentInChildren<TMP_Text>(true).text;
            if (answerPrompt != null)
                promptDefault = answerPrompt.text;
            if (answerInput != null)
            {
                var inputRect = (RectTransform)answerInput.transform;
                inputDefaultPosition = inputRect.anchoredPosition;
                inputDefaultSize = inputRect.sizeDelta;
                if (answerInput.placeholder is TMP_Text placeholder)
                    placeholderDefault = placeholder.text;
            }
            if (windowBorder != null)
                borderDefault = windowBorder.color;
            if (windowGlow != null)
                glowDefault = windowGlow.color;
        }

        void Update()
        {
            // Esc closes an obstacle's challenge, but never frees Alex from a hazard.
            Keyboard keyboard = Keyboard.current;
            if (IsOpen && !caught && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                OnCloseClicked();
        }

        // ---------- Called by ProgrammingChallengeManager ----------

        public void Show(QuestionData data, string obstacleName, bool hintAlreadyUnlocked, bool caughtByHazard = false, LessonData lessonData = null)
        {
            question = data;
            lesson = lessonData;
            hintShown = false;
            questionShown = false;
            caught = caughtByHazard;
            if (closeButton != null)
                closeButton.gameObject.SetActive(!caught);
            if (caughtBanner != null)
                caughtBanner.SetActive(caught);
            if (caughtText != null)
                caughtText.text = CaughtMessage;
            if (continueLabel != null)
                continueLabel.text = caught ? "ESCAPE" : continueDefault;
            if (windowBorder != null)
                windowBorder.color = caught ? new Color(caughtColor.r, caughtColor.g, caughtColor.b, borderDefault.a) : borderDefault;
            if (windowGlow != null)
                windowGlow.color = caught ? new Color(caughtColor.r, caughtColor.g, caughtColor.b, glowDefault.a) : glowDefault;

            tagsText.text = $"{obstacleName}  •  {data.Language.ToDisplayName()}  •  {data.Topic}  •  {data.Difficulty}".ToUpperInvariant();
            questionTypeText.text = data.QuestionType.ToDisplayName().ToUpperInvariant();
            questionText.text = data.QuestionText;

            bool hasCode = !string.IsNullOrWhiteSpace(data.Code);
            codePanel.SetActive(hasCode);
            codeText.text = hasCode ? (IsRunCode ? BuildRunCodeView(string.Empty) : string.Format(CodeFormat, data.Code)) : string.Empty;
            SetActive(outputPanel, false);

            for (int i = 0; i < choiceButtons.Length; i++)
            {
                bool used = data.AnswerFormat == AnswerFormat.Choices && i < data.ChoiceCount;
                choiceButtons[i].gameObject.SetActive(used);
                if (used)
                    choiceLabels[i].text = FormatChoice(i, data.GetChoice(i), data.MonospaceChoices);
            }
            answerInput.SetTextWithoutNotify(string.Empty);

            hintPanel.SetActive(false);
            hintButtonLabel.text = $"Hint (-{ProgrammingChallengeManager.HintPenalty})";
            if (hintAlreadyUnlocked)
                ShowHint(data.Hint);

            overlay.SetActive(true);
            // Learn first: the lesson's card comes before the question it teaches.
            if (lesson != null && lessonPanel != null)
                ShowLesson(false);
            else
                BeginQuestion();
        }

        /// <summary>Shows the answer controls (first attempt and every retry).</summary>
        public void ShowAnswerInput()
        {
            selectedChoice = -1;
            RefreshChoiceColours();

            choicesRoot.SetActive(!IsTyped);
            typedAnswerRoot.SetActive(IsTyped);
            SetActive(runButton, IsRunCode);
            ApplyAnswerLayout();
            if (!IsRunCode)
                SetActive(outputPanel, false);
            resultRoot.SetActive(false);

            submitButton.gameObject.SetActive(true);
            retryButton.gameObject.SetActive(false);
            continueButton.gameObject.SetActive(false);
            hintButton.gameObject.SetActive(question.HasHint);
            hintButton.interactable = !hintShown;
            SetActive(reviewButton, lesson != null);
            UpdateSubmitState();

            UIFocus.Clear();
            if (IsTyped)
                answerInput.ActivateInputField();
        }

        /// <summary>
        /// Shows the result. <paramref name="feedback"/> explains why a correct answer is right, or what is
        /// wrong with a wrong one (without giving the right answer away).
        /// </summary>
        public void ShowResult(bool correct, string feedback, int pointsAwarded, string clueReward, bool caughtByHazard = false)
        {
            choicesRoot.SetActive(false);
            typedAnswerRoot.SetActive(false);
            resultRoot.SetActive(true);

            if (caughtByHazard)
                resultTitleText.text = correct ? "CORRECT!  YOU'RE FREE - CAREFUL THIS TIME!" : "NOT QUITE - STILL CAUGHT. READ THE FEEDBACK AND TRY AGAIN";
            else
                resultTitleText.text = correct
                    ? (pointsAwarded > 0 ? $"CORRECT!  +{pointsAwarded} POINTS" : "CORRECT!")
                    : "NOT QUITE - READ THE FEEDBACK, THEN TRY AGAIN";
            resultTitleText.color = correct ? correctColor : incorrectColor;
            explanationText.text = correct
                ? feedback
                : $"<noparse>{feedback}</noparse>" + (lesson != null ? ReviewReminder : string.Empty);

            bool hasClue = correct && !string.IsNullOrWhiteSpace(clueReward);
            clueRewardText.gameObject.SetActive(hasClue);
            clueRewardText.text = hasClue ? "NEW CLUE: " + clueReward : string.Empty;

            submitButton.gameObject.SetActive(false);
            retryButton.gameObject.SetActive(!correct);
            continueButton.gameObject.SetActive(correct);
            hintButton.gameObject.SetActive(!correct && question.HasHint);
            SetActive(reviewButton, !correct && lesson != null);
            UIFocus.Clear();
        }

        public void ShowHint(string hint)
        {
            hintShown = true;
            hintPanel.SetActive(true);
            hintText.text = "HINT: " + hint;
            hintButtonLabel.text = "Hint shown";
            hintButton.interactable = false;
        }

        public void SetScore(int score, int hintsUsed)
        {
            scoreText.text = $"Score: {score}    Hints used: {hintsUsed}";
        }

        public void Hide()
        {
            if (overlay != null)
                overlay.SetActive(false);
            SetActive(lessonPanel, false);
            question = null;
            lesson = null;
            questionShown = false;
            selectedChoice = -1;
            caught = false;
            if (caughtBanner != null)
                caughtBanner.SetActive(false);
            if (closeButton != null)
                closeButton.gameObject.SetActive(true);
            UIFocus.Clear();
        }

        // ---------- Button / input callbacks (wired in the Inspector) ----------

        /// <summary>Continue on the learning card: opens the question, or returns to it after a review.</summary>
        public void OnLessonContinueClicked()
        {
            if (!IsLessonOpen)
                return;
            if (!questionShown)
            {
                BeginQuestion();
                return;
            }
            // Back from a review: the question, the chosen answer and any result are exactly as they were.
            lessonPanel.SetActive(false);
            if (IsTyped && typedAnswerRoot.activeSelf)
                answerInput.ActivateInputField();
            else
                UIFocus.Clear();
        }

        /// <summary>Review Concept: shows the learning card again without resetting the challenge.</summary>
        public void OnReviewConceptClicked()
        {
            if (IsOpen && !IsLessonOpen && lesson != null)
                ShowLesson(true);
        }

        public void OnChoiceClicked(int index)
        {
            if (question == null || IsLessonOpen || index < 0 || index >= question.ChoiceCount)
                return;
            selectedChoice = index;
            RefreshChoiceColours();
            UpdateSubmitState();
        }

        public void OnAnswerTextChanged(string text)
        {
            UpdateSubmitState();
            if (IsRunCode)
                codeText.text = BuildRunCodeView(text);
        }

        /// <summary>Enter pressed in the typed-answer field.</summary>
        public void OnAnswerSubmitted(string text)
        {
            // Enter on a Run Code question previews the code; CHECK is a deliberate button press.
            if (IsRunCode)
                OnRunClicked();
            else
                OnSubmitClicked();
        }

        public void OnSubmitClicked()
        {
            if (question == null || IsLessonOpen || !submitButton.gameObject.activeInHierarchy || Manager == null)
                return;

            if (IsRunCode)
            {
                if (!string.IsNullOrWhiteSpace(answerInput.text))
                    Manager.SubmitCode(answerInput.text);
            }
            else if (IsTyped)
            {
                if (!string.IsNullOrWhiteSpace(answerInput.text))
                    Manager.SubmitTypedAnswer(answerInput.text);
            }
            else if (selectedChoice >= 0)
            {
                Manager.SubmitChoice(selectedChoice);
            }
        }

        /// <summary>RUN: previews the typed code. Not an answer - no score and no penalty.</summary>
        public void OnRunClicked()
        {
            if (question == null || IsLessonOpen || !IsRunCode || Manager == null || !runButton.gameObject.activeInHierarchy)
                return;
            Manager.RunPreview(answerInput.text);
        }

        /// <summary>Shows the output console under the code (called by the manager).</summary>
        public void ShowRunOutput(string text, bool isError)
        {
            SetActive(outputPanel, true);
            if (outputText == null)
                return;
            outputText.text = "<noparse>" + text + "</noparse>";
            outputText.color = isError ? incorrectColor : correctColor;
        }

        // Hint, Retry and Continue sit behind the learning card while it is open; ignore them then
        // (e.g. when reached with the keyboard), so reading the card never uses a hint or changes the challenge.
        public void OnHintClicked()
        {
            if (Manager != null && !IsLessonOpen)
                Manager.UseHint();
        }

        public void OnRetryClicked()
        {
            if (Manager != null && !IsLessonOpen)
                Manager.Retry();
        }

        public void OnContinueClicked()
        {
            if (Manager != null && !IsLessonOpen)
                Manager.Continue();
        }

        public void OnCloseClicked()
        {
            if (Manager != null)
                Manager.CloseChallenge();
        }

        // ---------- Helpers ----------

        void BeginQuestion()
        {
            questionShown = true;
            SetActive(lessonPanel, false);
            ShowAnswerInput();
        }

        void ShowLesson(bool reviewing)
        {
            if (lesson == null || lessonPanel == null)
                return;

            if (lessonBadgeText != null)
                lessonBadgeText.text = $"{lesson.Language.ToDisplayName().ToUpperInvariant()} LESSON";
            SetText(lessonSymbolText, lesson.Symbol);
            SetText(lessonTitleText, lesson.ConceptTitle);
            SetText(lessonDefinitionText, lesson.Definition);
            SetText(lessonHowText, lesson.HowItWorks);
            SetActive(lessonExampleRoot, lesson.HasExample);
            if (lessonCodeText != null)
                lessonCodeText.text = lesson.HasExample ? string.Format(CodeFormat, lesson.ExampleCode) : string.Empty;
            SetText(lessonExampleText, lesson.ExampleExplanation);
            SetActive(lessonTipRoot, lesson.HasTip);
            if (lessonTipText != null)
                lessonTipText.text = lesson.HasTip ? $"<b>TIP:</b>  <noparse>{lesson.Tip}</noparse>" : string.Empty;
            if (lessonContinueLabel != null)
                lessonContinueLabel.text = reviewing ? "BACK TO CHALLENGE" : "CONTINUE TO CHALLENGE";

            if (!reviewing)
            {
                // Before the question has been shown, none of its controls are usable.
                choicesRoot.SetActive(false);
                typedAnswerRoot.SetActive(false);
                resultRoot.SetActive(false);
                submitButton.gameObject.SetActive(false);
                retryButton.gameObject.SetActive(false);
                continueButton.gameObject.SetActive(false);
                hintButton.gameObject.SetActive(false);
                SetActive(reviewButton, false);
            }
            lessonPanel.SetActive(true);
            UIFocus.Select(lessonContinueButton);
        }

        // The template with the blank replaced by what the player has typed so far, and the first test's setup above it.
        string BuildRunCodeView(string typed)
        {
            var sb = new System.Text.StringBuilder();
            RunTest[] tests = question.RunTests;
            if (tests != null && tests.Length > 0 && !string.IsNullOrWhiteSpace(tests[0].setup))
                sb.Append("<color=#8B98A5><noparse>").Append(tests[0].setup.Trim()).Append("</noparse></color>\n");
            string template = question.Code;
            int at = template.IndexOf(CodeRunner.Blank, System.StringComparison.Ordinal);
            if (at < 0)
                sb.Append("<noparse>").Append(template).Append("</noparse>");
            else
            {
                string shown = string.IsNullOrWhiteSpace(typed) ? CodeRunner.Blank : typed.Trim();
                sb.Append("<noparse>").Append(template.Substring(0, at)).Append("</noparse>")
                  .Append("<color=#4ADE80><noparse>").Append(shown).Append("</noparse></color>")
                  .Append("<noparse>").Append(template.Substring(at + CodeRunner.Blank.Length)).Append("</noparse>");
            }
            // Not CodeFormat: it wraps everything in <noparse>, which would also show the colour tags as text.
            return "<mspace=0.66em>" + sb + "</mspace>";
        }

        // Run Code questions put the input on the left, RUN beside it and the output console on the right.
        void ApplyAnswerLayout()
        {
            var inputRect = (RectTransform)answerInput.transform;
            inputRect.anchoredPosition = IsRunCode ? new Vector2(-460f, inputDefaultPosition.y) : inputDefaultPosition;
            inputRect.sizeDelta = IsRunCode ? new Vector2(600f, inputDefaultSize.y) : inputDefaultSize;
            if (answerPrompt != null)
                answerPrompt.text = IsRunCode ? "Type the missing code, then press RUN to try it, or CHECK to submit it." : promptDefault;
            if (answerInput.placeholder is TMP_Text placeholder)
                placeholder.text = IsRunCode ? "Type the code for the blank..." : placeholderDefault;
            TMP_Text submitLabel = submitButton.GetComponentInChildren<TMP_Text>(true);
            if (submitLabel != null)
                submitLabel.text = IsRunCode ? "CHECK" : submitDefault;
        }

        void RefreshChoiceColours()
        {
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                if (choiceButtons[i] != null && choiceButtons[i].targetGraphic != null)
                    choiceButtons[i].targetGraphic.color = i == selectedChoice ? selectedChoiceColor : choiceColor;
            }
        }

        void UpdateSubmitState()
        {
            if (question == null)
                return;
            submitButton.interactable = IsTyped ? !string.IsNullOrWhiteSpace(answerInput.text) : selectedChoice >= 0;
        }

        static void SetText(TMP_Text label, string text)
        {
            if (label != null)
                label.text = string.IsNullOrEmpty(text) ? string.Empty : $"<noparse>{text}</noparse>";
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

        static string FormatChoice(int index, string text, bool monospace)
        {
            string body = monospace ? string.Format(CodeFormat, text) : $"<noparse>{text}</noparse>";
            return $"<b>{Letters[index]}</b>    {body}";
        }
    }
}
