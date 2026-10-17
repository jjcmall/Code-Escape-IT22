using System;
using UnityEngine;

namespace CodeEscape.Programming
{
    /// <summary>
    /// A short lesson (learning card) shown before a programming question, so the player learns the idea
    /// the question tests: a definition, how it works, a small example and what the example does. Each
    /// lesson belongs to one language and teaches the questions listed by ID (or, as a fallback, every
    /// question of that language with one of its topics). Kept in the QuestionDatabase.
    /// </summary>
    [CreateAssetMenu(fileName = "Lesson", menuName = "Code Escape/Lesson Data")]
    public class LessonData : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField, Tooltip("Unique ID, e.g. PY_VARIABLES.")]
        string id;
        [SerializeField] ProgrammingLanguage language = ProgrammingLanguage.Python;
        [SerializeField, Tooltip("Concept name shown as the card's title.")]
        string conceptTitle;
        [SerializeField, Tooltip("A few characters shown in the badge next to the title, e.g. \"x = 1\" or \"for\".")]
        string symbol;

        [Header("Which questions it teaches")]
        [SerializeField, Tooltip("IDs of the questions that test this lesson.")]
        string[] relatedQuestionIds = new string[0];
        [SerializeField, Tooltip("Question topics this lesson covers. Used for questions that aren't listed by ID.")]
        string[] topics = new string[0];

        [Header("Lesson")]
        [SerializeField, TextArea(2, 5), Tooltip("What the concept is, in one or two simple sentences.")]
        string definition;
        [SerializeField, TextArea(2, 6), Tooltip("How the concept is written and used.")]
        string howItWorks;
        [SerializeField, TextArea(3, 12), Tooltip("A small example in this lesson's language. It must not be the question's own code.")]
        string exampleCode;
        [SerializeField, TextArea(2, 5), Tooltip("What the example does, in plain English.")]
        string exampleExplanation;
        [SerializeField, TextArea(1, 3), Tooltip("Optional tip shown at the bottom of the card.")]
        string tip;

        public string Id => id;
        public ProgrammingLanguage Language => language;
        public string ConceptTitle => conceptTitle;
        public string Symbol => symbol;
        public string Definition => definition;
        public string HowItWorks => howItWorks;
        public string ExampleCode => exampleCode;
        public string ExampleExplanation => exampleExplanation;
        public string Tip => tip;
        public bool HasExample => !string.IsNullOrWhiteSpace(exampleCode);
        public bool HasTip => !string.IsNullOrWhiteSpace(tip);

        /// <summary>True when the question is listed by ID in this lesson (and uses the same language).</summary>
        public bool ListsQuestion(QuestionData question)
        {
            if (question == null || question.Language != language || relatedQuestionIds == null)
                return false;
            foreach (string questionId in relatedQuestionIds)
            {
                if (string.Equals(questionId, question.Id, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        /// <summary>True when the lesson covers the question's topic (and uses the same language).</summary>
        public bool CoversTopic(QuestionData question)
        {
            if (question == null || question.Language != language || topics == null || string.IsNullOrWhiteSpace(question.Topic))
                return false;
            foreach (string topic in topics)
            {
                if (string.Equals(topic, question.Topic, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>Checks for authoring mistakes. Returns null when the lesson is valid.</summary>
        public string Validate()
        {
            if (string.IsNullOrWhiteSpace(id)) return "missing ID";
            if (!language.IsValid()) return "language is not set";
            if (string.IsNullOrWhiteSpace(conceptTitle)) return "missing concept title";
            if (string.IsNullOrWhiteSpace(definition)) return "missing definition";
            if (string.IsNullOrWhiteSpace(howItWorks)) return "missing 'how it works' text";
            if (HasExample && string.IsNullOrWhiteSpace(exampleExplanation)) return "example has no explanation";
            return null;
        }
    }
}
