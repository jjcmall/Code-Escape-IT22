using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodeEscape.Programming
{
    /// <summary>
    /// All predefined questions, and the lessons (learning cards) that teach them. Lookups always filter
    /// by programming language.
    /// </summary>
    [CreateAssetMenu(fileName = "QuestionDatabase", menuName = "Code Escape/Question Database")]
    public class QuestionDatabase : ScriptableObject
    {
        [SerializeField] List<QuestionData> questions = new List<QuestionData>();
        [SerializeField, Tooltip("Learning cards shown before a question. Each question should be taught by one lesson of its language.")]
        List<LessonData> lessons = new List<LessonData>();

        public IReadOnlyList<QuestionData> Questions => questions;
        public IReadOnlyList<LessonData> Lessons => lessons;

        public List<QuestionData> GetQuestions(ProgrammingLanguage language)
        {
            var result = new List<QuestionData>();
            foreach (QuestionData question in questions)
            {
                if (question != null && question.Language == language)
                    result.Add(question);
            }
            return result;
        }

        public QuestionData GetById(string id)
        {
            foreach (QuestionData question in questions)
            {
                if (question != null && question.Id == id)
                    return question;
            }
            return null;
        }

        /// <summary>
        /// Finds a question in the given language, optionally by topic and difficulty. Prefers questions
        /// not listed in <paramref name="excludeIds"/>, but falls back to one of them rather than nothing.
        /// </summary>
        public QuestionData FindQuestion(ProgrammingLanguage language, string topic = null,
            QuestionDifficulty? difficulty = null, ICollection<string> excludeIds = null)
        {
            QuestionData fallback = null;
            foreach (QuestionData question in questions)
            {
                if (question == null || question.Language != language)
                    continue;
                if (!string.IsNullOrEmpty(topic) && !string.Equals(question.Topic, topic, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (difficulty.HasValue && question.Difficulty != difficulty.Value)
                    continue;

                if (excludeIds == null || !excludeIds.Contains(question.Id))
                    return question;
                if (fallback == null)
                    fallback = question;
            }
            return fallback;
        }

        /// <summary>
        /// The lesson that teaches a question: one that lists the question by ID, otherwise one of the same
        /// language that covers its topic. Null when there is none (the question is then asked directly).
        /// </summary>
        public LessonData FindLesson(QuestionData question)
        {
            if (question == null)
                return null;
            foreach (LessonData lesson in lessons)
            {
                if (lesson != null && lesson.ListsQuestion(question))
                    return lesson;
            }
            foreach (LessonData lesson in lessons)
            {
                if (lesson != null && lesson.CoversTopic(question))
                    return lesson;
            }
            return null;
        }

        /// <summary>Lists authoring problems (empty entries, invalid questions or lessons, duplicate IDs, multiple-choice questions, questions without a lesson).</summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            var ids = new HashSet<string>();
            for (int i = 0; i < questions.Count; i++)
            {
                QuestionData question = questions[i];
                if (question == null)
                {
                    problems.Add($"Entry {i} is empty");
                    continue;
                }

                string error = question.Validate();
                if (error != null)
                    problems.Add($"{question.name}: {error}");
                if (!string.IsNullOrEmpty(question.Id) && !ids.Add(question.Id))
                    problems.Add($"{question.name}: duplicate ID '{question.Id}'");
                if (question.AnswerFormat == AnswerFormat.Choices)
                    problems.Add($"{question.name}: uses multiple choice (questions should be typed)");
                if (lessons.Count > 0 && FindLesson(question) == null)
                    problems.Add($"{question.name}: no lesson teaches this question");
            }

            var lessonIds = new HashSet<string>();
            for (int i = 0; i < lessons.Count; i++)
            {
                LessonData lesson = lessons[i];
                if (lesson == null)
                {
                    problems.Add($"Lesson entry {i} is empty");
                    continue;
                }
                string error = lesson.Validate();
                if (error != null)
                    problems.Add($"{lesson.name}: {error}");
                if (!string.IsNullOrEmpty(lesson.Id) && !lessonIds.Add(lesson.Id))
                    problems.Add($"{lesson.name}: duplicate lesson ID '{lesson.Id}'");
            }
            return problems;
        }
    }
}
