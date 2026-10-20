using System;
using System.Text;
using CodeEscape.Execution;
using UnityEngine;

namespace CodeEscape.Programming
{
    public enum QuestionType
    {
        MultipleChoice = 0,
        PredictOutput = 1,
        FillMissingCode = 2,
        FixIncorrectCode = 3,
        ChooseCorrectCode = 4
    }

    public enum QuestionDifficulty
    {
        Easy = 0,
        Medium = 1,
        Hard = 2
    }

    /// <summary>
    /// How the player answers. All current questions are typed (TypedAnswer or RunCode); Choices is kept
    /// only so older assets still load, and QuestionDatabase.Validate() flags any question that uses it.
    /// </summary>
    public enum AnswerFormat
    {
        Choices = 0,
        TypedAnswer = 1,
        /// <summary>Fill the blank in a short Python snippet; it is run in the game's sandboxed interpreter.</summary>
        RunCode = 2
    }

    /// <summary>
    /// One predefined programming question. Answers are only compared with the stored correct answer:
    /// typed text is never compiled or run by Unity or the system. Run Code questions use the game's own limited
    /// Python interpreter (CodeRunner), and SQL questions use fictional sample tables.
    /// </summary>
    [CreateAssetMenu(fileName = "Question", menuName = "Code Escape/Question Data")]
    public class QuestionData : ScriptableObject
    {
        public const int MaxChoices = 4;

        [Header("Identity")]
        [SerializeField, Tooltip("Unique ID, e.g. PY_01_VARIABLES.")]
        string id;
        [SerializeField] ProgrammingLanguage language = ProgrammingLanguage.Python;
        [SerializeField, Tooltip("Beginner topic, e.g. Variables, Loops, SELECT.")]
        string topic;
        [SerializeField] QuestionDifficulty difficulty;
        [SerializeField] QuestionType questionType;

        [Header("Question")]
        [SerializeField, TextArea(2, 5)] string questionText;
        [SerializeField, TextArea(3, 14), Tooltip("Optional code snippet or sample table, shown in a monospaced box.")]
        string code;

        [Header("Answer")]
        [SerializeField] AnswerFormat answerFormat = AnswerFormat.Choices;
        [SerializeField, TextArea(1, 4), Tooltip("Choices format: 2 to 4 choices.")]
        string[] choices = new string[0];
        [SerializeField, Tooltip("Choices format: index of the correct choice (0 = first).")]
        int correctChoiceIndex;
        [SerializeField, Tooltip("Show the choices in a monospaced code font.")]
        bool monospaceChoices;
        [SerializeField, Tooltip("Typed format: every accepted answer. Spacing differences are ignored.")]
        string[] acceptedAnswers = new string[0];
        [SerializeField, Tooltip("Typed format: must upper/lower case match exactly?")]
        bool caseSensitive = true;

        [Header("Run Code format")]
        [SerializeField, Tooltip("The correct fill for the blank. Only used to check the question at authoring time.")]
        string referenceFill;
        [SerializeField, Tooltip("Run Code format: the Code field is the template and must contain one ____ blank. Each test runs it after its setup lines and compares the printed output.")]
        RunTest[] runTests = new RunTest[0];

        [Header("Feedback")]
        [SerializeField, TextArea(2, 6), Tooltip("Shown after a correct answer: why the answer is right.")]
        string explanation;
        [SerializeField, TextArea(1, 4)] string hint;
        [SerializeField, TextArea(1, 4), Tooltip("Choices format: why each wrong choice is wrong, in the same order as the choices (the correct one is left empty). " +
            "Shown after a wrong answer instead of the explanation, so the answer isn't given away.")]
        string[] wrongChoiceFeedback = new string[0];
        [SerializeField, TextArea(1, 4), Tooltip("Shown after a wrong answer when there is no feedback for that choice (always used for typed answers).")]
        string wrongAnswerFeedback;

        public string Id => id;
        public ProgrammingLanguage Language => language;
        public string Topic => topic;
        public QuestionDifficulty Difficulty => difficulty;
        public QuestionType QuestionType => questionType;
        public string QuestionText => questionText;
        public string Code => code;
        public AnswerFormat AnswerFormat => answerFormat;
        public int ChoiceCount => choices != null ? choices.Length : 0;
        public int CorrectChoiceIndex => correctChoiceIndex;
        public bool MonospaceChoices => monospaceChoices;
        public string Explanation => explanation;
        public string Hint => hint;
        public string ReferenceFill => referenceFill;
        public RunTest[] RunTests => runTests;
        public bool IsRunCode => answerFormat == AnswerFormat.RunCode;
        public bool HasHint => !string.IsNullOrWhiteSpace(hint);

        /// <summary>
        /// Python "predict the output" questions are checked by running their code in the game's interpreter
        /// and comparing the player's prediction with what it really prints (the stored accepted answers are
        /// only used if the code can't be run).
        /// </summary>
        public bool ChecksByRunning => answerFormat == AnswerFormat.TypedAnswer && questionType == QuestionType.PredictOutput
            && language == ProgrammingLanguage.Python && !string.IsNullOrWhiteSpace(code);

        /// <summary>Runs the question's code and returns what it prints. False when it can't be run.</summary>
        public bool TryGetRealOutput(out string output)
        {
            output = null;
            if (!ChecksByRunning)
                return false;
            RunResult run = CodeRunner.Run(code);
            if (!run.Success)
                return false;
            output = run.Output.TrimEnd('\n', '\r', ' ');
            return true;
        }

        /// <summary>
        /// Feedback for a wrong answer: what the chosen answer gets wrong, without revealing the right one.
        /// <paramref name="choiceIndex"/> is -1 for a typed answer. Empty when none has been written.
        /// </summary>
        public string GetWrongAnswerFeedback(int choiceIndex)
        {
            if (choiceIndex >= 0 && wrongChoiceFeedback != null && choiceIndex < wrongChoiceFeedback.Length
                && !string.IsNullOrWhiteSpace(wrongChoiceFeedback[choiceIndex]))
                return wrongChoiceFeedback[choiceIndex];
            return wrongAnswerFeedback ?? string.Empty;
        }

        public string GetChoice(int index)
        {
            return index >= 0 && index < ChoiceCount ? choices[index] : string.Empty;
        }

        public bool IsCorrectChoice(int index)
        {
            return answerFormat == AnswerFormat.Choices && index >= 0 && index < ChoiceCount && index == correctChoiceIndex;
        }

        public bool IsCorrectTypedAnswer(string answer)
        {
            if (answerFormat != AnswerFormat.TypedAnswer || string.IsNullOrWhiteSpace(answer) || acceptedAnswers == null)
                return false;

            string given = NormalizeAnswer(answer);
            string givenCode = CodeText.Squash(answer);
            StringComparison comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            // Predict the output: the real output of the code decides (several printed lines may be typed on one line).
            if (TryGetRealOutput(out string realOutput))
                return string.Equals(given, NormalizeAnswer(realOutput), comparison);

            foreach (string accepted in acceptedAnswers)
            {
                if (string.IsNullOrWhiteSpace(accepted))
                    continue;
                // Exact (ignoring spacing) first; then the code-aware form, which also ignores spaces around
                // symbols and a final semicolon, so "age>3;" matches "age > 3".
                if (string.Equals(given, NormalizeAnswer(accepted), comparison)
                    || string.Equals(givenCode, CodeText.Squash(accepted), comparison))
                    return true;
            }
            return false;
        }

        /// <summary>Trims, collapses runs of whitespace and straightens curly quotes, so formatting isn't marked wrong.</summary>
        public static string NormalizeAnswer(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var builder = new StringBuilder(text.Length);
            bool pendingSpace = false;
            foreach (char raw in text.Trim())
            {
                char c = raw;
                if (c == '‘' || c == '’') c = '\'';
                else if (c == '“' || c == '”') c = '"';

                if (char.IsWhiteSpace(c))
                {
                    pendingSpace = true;
                    continue;
                }
                if (pendingSpace)
                {
                    builder.Append(' ');
                    pendingSpace = false;
                }
                builder.Append(c);
            }
            return builder.ToString();
        }

        /// <summary>Checks for authoring mistakes. Returns null when the question is valid.</summary>
        public string Validate()
        {
            if (string.IsNullOrWhiteSpace(id)) return "missing ID";
            if (!language.IsValid()) return "language is not set";
            if (string.IsNullOrWhiteSpace(questionText)) return "missing question text";
            if (string.IsNullOrWhiteSpace(explanation)) return "missing explanation";

            if (answerFormat == AnswerFormat.Choices)
            {
                if (ChoiceCount < 2 || ChoiceCount > MaxChoices) return $"needs 2-{MaxChoices} choices (has {ChoiceCount})";
                if (correctChoiceIndex < 0 || correctChoiceIndex >= ChoiceCount) return "correct choice index is out of range";
                for (int i = 0; i < ChoiceCount; i++)
                {
                    if (string.IsNullOrWhiteSpace(choices[i])) return $"choice {i} is empty";
                }
            }
            else if (answerFormat == AnswerFormat.RunCode)
            {
                if (string.IsNullOrEmpty(code) || !code.Contains(CodeRunner.Blank)) return "Run Code needs a code template containing ____";
                if (runTests == null || runTests.Length == 0) return "Run Code needs at least one test";
                if (string.IsNullOrWhiteSpace(referenceFill)) return "Run Code needs a reference fill";
                JudgeResult check = CodeRunner.Judge(code, referenceFill, runTests);
                if (!check.Passed) return "the reference fill fails its own tests: " + check.Message.Replace('\n', ' ');
            }
            else
            {
                bool any = false;
                if (acceptedAnswers != null)
                {
                    foreach (string accepted in acceptedAnswers)
                        any |= !string.IsNullOrWhiteSpace(accepted);
                }
                if (!any) return "no accepted typed answers";

                // Python is checked by running code: typed Python questions must be runnable predict-the-output ones.
                if (language == ProgrammingLanguage.Python)
                {
                    if (!ChecksByRunning) return "Python typed questions must be Predict the Output with code (use Run Code for writing code)";
                    if (!TryGetRealOutput(out string output)) return "its code doesn't run in the game's Python interpreter";
                    if (!string.Equals(NormalizeAnswer(acceptedAnswers[0]), NormalizeAnswer(output),
                            caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))
                        return $"its first accepted answer doesn't match the real output '{output}'";
                }
            }
            return null;
        }
    }

    public static class QuestionTypeExtensions
    {
        public static string ToDisplayName(this QuestionType type)
        {
            switch (type)
            {
                case QuestionType.PredictOutput: return "Predict the Output";
                case QuestionType.FillMissingCode: return "Fill in the Missing Code";
                case QuestionType.FixIncorrectCode: return "Fix the Incorrect Code";
                case QuestionType.ChooseCorrectCode: return "Write the Correct Code";
                default: return "Concept Check";
            }
        }
    }
}
