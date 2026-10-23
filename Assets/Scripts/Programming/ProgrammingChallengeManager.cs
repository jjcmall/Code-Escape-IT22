using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using CodeEscape.Execution;
using CodeEscape.Game;
using CodeEscape.Obstacles;
using CodeEscape.UI;

namespace CodeEscape.Programming
{
    /// <summary>
    /// Runs programming challenges: picks the obstacle's question for the selected language and the lesson
    /// (learning card) that teaches it, pauses the game, drives ChallengeUI (card first, then the question),
    /// checks answers against the stored solution, keeps score and unlocks
    /// the obstacle. Hazards that catch Alex open a mandatory challenge through the same path: it can't
    /// be closed until answered correctly, gives no points and doesn't count as a solved obstacle.
    /// Lives on the persistent GameSystems object, so score carries across scenes.
    /// </summary>
    public class ProgrammingChallengeManager : MonoBehaviour
    {
        public const int PointsPerCorrectAnswer = 100;
        public const int HintPenalty = 25;
        /// <summary>Wrong answers on one question before SHOW START (the first part of the answer) is offered.</summary>
        public const int StarterAfterWrongAnswers = 3;
        /// <summary>Wrong answers on one question before SHOW ANSWER is offered, so no one stays stuck.</summary>
        public const int AnswerAfterWrongAnswers = 5;
        const string WrongAnswerFallback = "Not quite. Go through the code one step at a time, or review the concept and try again.";

        public static ProgrammingChallengeManager Instance { get; private set; }

        /// <summary>Raised with (score, hints used).</summary>
        public static event Action<int, int> ScoreChanged;
        public static event Action<QuestionData> ChallengeOpened;
        public static event Action ChallengeClosed;
        /// <summary>Raised with (obstacle ID, question) when a solved challenge closes and the obstacle unlocks.</summary>
        public static event Action<string, QuestionData> ChallengeCompleted;
        /// <summary>Raised with (clue ID, clue text) when an obstacle with a clue reward unlocks.</summary>
        public static event Action<string, string> ClueRewarded;
        /// <summary>Raised with true/false each time an answer is checked.</summary>
        public static event Action<bool> AnswerChecked;
        /// <summary>Raised whenever a hint is shown.</summary>
        public static event Action HintShown;
        /// <summary>Raised when RUN previews a Run Code answer: true if the code ran without an error.</summary>
        public static event Action<bool> CodeRun;
        /// <summary>Raised with the hazard ID when a hazard catches Alex (its challenge has just opened).</summary>
        public static event Action<string> HazardCaught;
        /// <summary>Raised with the hazard ID when Alex escapes (the caught challenge was solved and closed).</summary>
        public static event Action<string> HazardEscaped;

        [SerializeField] QuestionDatabase questionDatabase;
        [SerializeField] ChallengeUI challengeUI;

        readonly HashSet<string> completedObstacleIds = new HashSet<string>();
        readonly HashSet<string> completedQuestionIds = new HashSet<string>();
        readonly HashSet<string> hintedQuestionIds = new HashSet<string>();
        // Help for a player who is stuck (this session only): wrong answers per question, and which
        // questions had their start or their whole answer shown.
        readonly Dictionary<string, int> wrongAnswers = new Dictionary<string, int>();
        readonly HashSet<string> starterShownIds = new HashSet<string>();
        readonly HashSet<string> revealedIds = new HashSet<string>();
        // Hints given free to a stuck player: shown, but not penalised (kept apart from hintedQuestionIds,
        // which the score and Cloud Save count as penalised).
        readonly HashSet<string> freeHintIds = new HashSet<string>();
        // Obstacles opened with SHOW ANSWER: solved, but they earned no points (saved, and left out of the cloud score).
        readonly HashSet<string> unscoredObstacleIds = new HashSet<string>();

        // Challenges solved in a row with the first CHECK (no wrong answer, no shown answer). Just for fun: no points.
        int streak;

        /// <summary>Raised with the new first-try streak whenever it changes.</summary>
        public static event Action<int> StreakChanged;

        /// <summary>The next help the hint button gives for the open question.</summary>
        public enum HelpStep { None, Hint, Starter, Answer }

        int score;
        int hintsUsed;
        IChallengeSource activeSource;
        QuestionData activeQuestion;
        bool activeSolved;
        LessonData activeLesson;

        public int Score => score;
        public int HintsUsed => hintsUsed;
        public bool IsChallengeOpen => activeQuestion != null;
        public bool IsActiveChallengeSolved => activeSolved;
        public QuestionData ActiveQuestion => activeQuestion;
        public IChallengeSource ActiveSource => activeSource;
        /// <summary>The lesson shown before the open challenge's question (null when it has none).</summary>
        public LessonData ActiveLesson => activeLesson;
        /// <summary>True while a hazard's challenge is open: it can only end with a correct answer.</summary>
        public bool IsActiveChallengeMandatory => activeSource != null && activeSource.IsMandatory;
        public QuestionDatabase Database => questionDatabase;
        public IEnumerable<string> CompletedObstacleIds => completedObstacleIds;
        public IEnumerable<string> CompletedQuestionIds => completedQuestionIds;
        public IEnumerable<string> HintedQuestionIds => hintedQuestionIds;
        /// <summary>Solved obstacles that earned points (all solved ones except those opened with SHOW ANSWER).</summary>
        public IEnumerable<string> ScoredObstacleIds
        {
            get
            {
                foreach (string id in completedObstacleIds)
                    if (!unscoredObstacleIds.Contains(id))
                        yield return id;
            }
        }
        public IEnumerable<string> UnscoredObstacleIds => unscoredObstacleIds;
        /// <summary>Challenges solved in a row with the first CHECK.</summary>
        public int Streak => streak;
        public int CompletedObstacleCount => completedObstacleIds.Count;
        /// <summary>Wrong answers so far on the open question (kept if the challenge is closed and opened again).</summary>
        public int ActiveWrongAnswers => activeQuestion != null && wrongAnswers.TryGetValue(activeQuestion.Id, out int n) ? n : 0;
        /// <summary>True when SHOW ANSWER was used on the open question (solving it then gives no points or badge).</summary>
        public bool ActiveAnswerRevealed => activeQuestion != null && revealedIds.Contains(activeQuestion.Id);

        /// <summary>
        /// The help ladder for the open question: the hint first (as before), then after
        /// <see cref="StarterAfterWrongAnswers"/> wrong answers the start of the answer, and after
        /// <see cref="AnswerAfterWrongAnswers"/> the whole answer.
        /// </summary>
        public HelpStep NextHelp
        {
            get
            {
                if (!IsChallengeOpen || activeSolved)
                    return HelpStep.None;
                string id = activeQuestion.Id;
                if (activeQuestion.HasHint && !HintSeen(id))
                    return HelpStep.Hint;
                if (revealedIds.Contains(id))
                    return HelpStep.None;
                int wrong = ActiveWrongAnswers;
                if (wrong >= AnswerAfterWrongAnswers)
                    return HelpStep.Answer;
                if (wrong >= StarterAfterWrongAnswers && !starterShownIds.Contains(id) && activeQuestion.GetStarter() != null)
                    return HelpStep.Starter;
                return HelpStep.None;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            ScoreChanged = null;
            ChallengeOpened = null;
            ChallengeClosed = null;
            ChallengeCompleted = null;
            StreakChanged = null;
            ClueRewarded = null;
            AnswerChecked = null;
            HintShown = null;
            CodeRun = null;
            HazardCaught = null;
            HazardEscaped = null;
            CodeExecuted = null;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (questionDatabase == null)
                Debug.LogError("[Challenge] No QuestionDatabase assigned.");
            else
            {
                foreach (string problem in questionDatabase.Validate())
                    Debug.LogWarning($"[Challenge] Question problem: {problem}");
            }

            // Restore score, hints and solved challenges from the saved playthrough.
            Load(SaveSystem.SaveManager.LoadProgress());
        }

        void Load(SaveSystem.ProgressData progress)
        {
            completedObstacleIds.Clear();
            completedQuestionIds.Clear();
            hintedQuestionIds.Clear();
            unscoredObstacleIds.Clear();
            score = 0;
            hintsUsed = 0;
            if (progress == null)
                return;
            score = Mathf.Max(0, progress.score);
            hintsUsed = Mathf.Max(0, progress.hintsUsed);
            foreach (string id in progress.completedChallenges) completedObstacleIds.Add(id);
            foreach (string id in progress.completedQuestions) completedQuestionIds.Add(id);
            foreach (string id in progress.hintedQuestions) hintedQuestionIds.Add(id);
            if (progress.unscoredChallenges != null)
                foreach (string id in progress.unscoredChallenges) unscoredObstacleIds.Add(id);
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            LanguageManager.LanguageChanged += HandleLanguageChanged;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            LanguageManager.LanguageChanged -= HandleLanguageChanged;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        bool HintSeen(string questionId) => hintedQuestionIds.Contains(questionId) || freeHintIds.Contains(questionId);

        public bool IsObstacleCompleted(string obstacleId)
        {
            return !string.IsNullOrEmpty(obstacleId) && completedObstacleIds.Contains(obstacleId);
        }

        /// <summary>Opens the obstacle's challenge in the selected language. Returns false if it can't open now.</summary>
        public bool TryOpenChallenge(ProgrammingObstacle obstacle)
        {
            return TryOpenChallenge((IChallengeSource)obstacle);
        }

        /// <summary>Opens a challenge for an obstacle or a hazard. Returns false if it can't open now.</summary>
        public bool TryOpenChallenge(IChallengeSource obstacle)
        {
            if (obstacle == null || IsChallengeOpen || !CanStartChallenge())
                return false;
            if (challengeUI == null)
            {
                Debug.LogError("[Challenge] No ChallengeUI assigned.");
                return false;
            }

            ProgrammingLanguage language = LanguageManager.Current;
            if (!language.IsValid())
            {
                Debug.LogWarning("[Challenge] No programming language selected yet - choose one on the Language Select screen.");
                return false;
            }

            QuestionData question = obstacle.ResolveQuestion(language, questionDatabase, completedQuestionIds);
            if (question == null)
            {
                Debug.LogError($"[Challenge] '{obstacle.DisplayName}' has no {language.ToDisplayName()} question.");
                return false;
            }
            if (question.Language != language)
            {
                // Safety net: never show a question from another language.
                Debug.LogError($"[Challenge] Refused {question.Id}: it is {question.Language}, but {language} is selected.");
                return false;
            }

            pendingEffect = null;
            pendingExecution = null;
            activeSource = obstacle;
            activeQuestion = question;
            activeSolved = false;
            activeLesson = questionDatabase != null ? questionDatabase.FindLesson(question) : null;

            Time.timeScale = 0f;
            if (GameManager.Instance != null)
                GameManager.Instance.SetState(GameState.Challenge);

            challengeUI.Show(question, obstacle.DisplayName, HintSeen(question.Id), obstacle.IsMandatory, activeLesson);
            // Help already given on this question earlier in the session stays visible.
            if (starterShownIds.Contains(question.Id))
                challengeUI.ShowHelp("START", question.GetStarter());
            if (revealedIds.Contains(question.Id))
                challengeUI.ShowHelp("ANSWER", question.GetRevealedAnswer());
            challengeUI.SetScore(score, hintsUsed);
            Debug.Log($"[Challenge] Opened {question.Id} ({language.ToDisplayName()}) at '{obstacle.DisplayName}'{(obstacle.IsMandatory ? " - caught" : string.Empty)}, lesson {(activeLesson != null ? activeLesson.Id : "none")}");
            ChallengeOpened?.Invoke(question);
            if (obstacle.IsMandatory)
                HazardCaught?.Invoke(obstacle.ObstacleId);
            return true;
        }

        public void SubmitChoice(int choiceIndex)
        {
            if (CanAnswer())
                Evaluate(activeQuestion.IsCorrectChoice(choiceIndex), choiceIndex);
        }

        public void SubmitTypedAnswer(string answer)
        {
            if (CanAnswer())
                Evaluate(activeQuestion.IsCorrectTypedAnswer(answer), -1);
        }

        /// <summary>
        /// Run Code questions: runs the player's fill against every test in the sandboxed interpreter and
        /// checks the printed output. The code never leaves the interpreter (no files, network or imports).
        /// </summary>
        public void SubmitCode(string fill)
        {
            if (!CanAnswer() || !activeQuestion.IsRunCode)
                return;

            string problem = CodeRunner.ValidateFill(fill);
            if (problem != null)
            {
                challengeUI.ShowRunOutput(problem, true);
                return;
            }

            JudgeResult judged = CodeRunner.Judge(activeQuestion.Code, fill, activeQuestion.RunTests);
            string detail = judged.Passed ? null : judged.Message;
            if (!judged.Passed)
            {
                string tip = activeQuestion.GetWrongAnswerFeedback(-1);
                if (!string.IsNullOrWhiteSpace(tip))
                    detail += "\n\n" + tip;
            }
            // The lock's device shows what this code did when the window closes, right or wrong.
            QueueWorldEffect(fill, judged.Passed);
            Evaluate(judged.Passed, -1, detail);
        }

        // ---------- World effects (the code's result shown in the level) ----------

        CodeEffect pendingEffect;
        CodeExecution pendingExecution;

        /// <summary>Raised when a challenge's world effect plays (after its window closes; Passed tells whether it was solved).</summary>
        public static event Action<CodeExecution> CodeExecuted;

        /// <summary>The lock's (or hazard's) effect for the open question, if it has one with the question's effect ID.</summary>
        CodeEffect FindWorldEffect()
        {
            if (activeQuestion == null || string.IsNullOrEmpty(activeQuestion.EffectId) || !(activeSource is Component component) || component == null)
                return null;
            foreach (CodeEffect effect in component.GetComponentsInChildren<CodeEffect>(true))
                if (effect.EffectId == activeQuestion.EffectId)
                    return effect;
            return null;
        }

        // The program as shown in the level: the first test's setup and what the solved code printed with it.
        CodeExecution BuildExecution(string fill, bool passed)
        {
            RunTest[] tests = activeQuestion.RunTests;
            string setup = tests != null && tests.Length > 0 ? tests[0].setup : string.Empty;
            RunResult run = CodeRunner.RunWithSetup(activeQuestion.Code, fill, setup);
            return new CodeExecution
            {
                Question = activeQuestion,
                Source = activeSource,
                Fill = fill,
                Setup = setup ?? string.Empty,
                Output = run.Success ? run.Output : string.Empty,
                Passed = passed && run.Success
            };
        }

        // The latest checked answer is the one shown: a wrong one in red, until a right one replaces it.
        void QueueWorldEffect(string fill, bool passed)
        {
            pendingEffect = FindWorldEffect();
            pendingExecution = pendingEffect != null ? BuildExecution(fill, passed) : null;
        }

        void PlayPendingWorldEffect()
        {
            CodeEffect effect = pendingEffect;
            CodeExecution execution = pendingExecution;
            pendingEffect = null;
            pendingExecution = null;
            if (effect == null || execution == null)
                return;
            Debug.Log($"[Challenge] World effect '{effect.EffectId}' for {execution.Question.Id}: {execution.Output.Trim().Replace('\n', ' ')}");
            effect.Play(execution);
            CodeExecuted?.Invoke(execution);
        }

        /// <summary>Runs the fill with the first test's setup and shows what it prints. Not an answer: no score, no penalty.</summary>
        public void RunPreview(string fill)
        {
            if (!CanAnswer() || !activeQuestion.IsRunCode)
                return;

            RunTest[] tests = activeQuestion.RunTests;
            string setup = tests != null && tests.Length > 0 ? tests[0].setup : null;
            RunResult result = CodeRunner.RunWithSetup(activeQuestion.Code, fill, setup);
            CodeRun?.Invoke(result.Success);
            if (!result.Success)
            {
                // Line numbers count the setup lines shown above the code, so they match the code on screen.
                int line = result.ErrorLine > 0 ? result.ErrorLine + CodeRunner.ShownSetupLines(tests) : 0;
                challengeUI.ShowRunOutput(line > 0 ? "Line " + line + ": " + result.Error : result.Error, true);
                return;
            }
            string shown = string.IsNullOrEmpty(result.Output.Trim()) ? "(nothing was printed)" : result.Output.TrimEnd();
            // Preview of what the lock in the level would do with this output.
            CodeEffect effect = FindWorldEffect();
            if (effect != null)
            {
                var preview = new CodeExecution { Question = activeQuestion, Source = activeSource, Fill = fill, Setup = setup ?? string.Empty, Output = result.Output };
                shown += "\n> " + effect.Describe(preview);
            }
            challengeUI.ShowRunOutput(shown, false);
        }

        /// <summary>True when the open question's hint costs nothing: the player is stuck (see <see cref="StarterAfterWrongAnswers"/>).</summary>
        public bool IsHintFree => ActiveWrongAnswers >= StarterAfterWrongAnswers;

        /// <summary>
        /// Shows the hint. The penalty applies the first time each question's hint is used, unless the player is
        /// already stuck on it (then the hint is free).
        /// </summary>
        public void UseHint()
        {
            if (!IsChallengeOpen || activeSolved || !activeQuestion.HasHint)
                return;

            string id = activeQuestion.Id;
            if (!HintSeen(id))
            {
                hintsUsed++;
                // A free hint is kept apart, so the score here and in Cloud Save never count it as penalised.
                if (IsHintFree)
                    freeHintIds.Add(id);
                else
                    hintedQuestionIds.Add(id);
                AddScore(IsHintFree ? 0 : -HintPenalty);
                Debug.Log($"[Challenge] Hint used on {id} ({(IsHintFree ? "free" : "-" + HintPenalty)}). Score {score}");
            }
            challengeUI.ShowHint(activeQuestion.Hint);
            HintShown?.Invoke();
        }

        /// <summary>
        /// The hint button: gives the next step of <see cref="NextHelp"/>. SHOW START and SHOW ANSWER cost no
        /// points but count as help (no "no hints" star); after SHOW ANSWER the player still presses CHECK and
        /// reads why it works, but earns no points for that question.
        /// </summary>
        public void UseHelp()
        {
            switch (NextHelp)
            {
                case HelpStep.Hint:
                    UseHint();
                    break;
                case HelpStep.Starter:
                    starterShownIds.Add(activeQuestion.Id);
                    challengeUI.ShowHelp("START", activeQuestion.GetStarter());
                    Debug.Log($"[Challenge] Start of the answer shown on {activeQuestion.Id}");
                    HintShown?.Invoke();
                    break;
                case HelpStep.Answer:
                    revealedIds.Add(activeQuestion.Id);
                    string answer = activeQuestion.GetRevealedAnswer();
                    // Back to the answer box with the answer typed in: CHECK runs it, and the feedback explains it.
                    challengeUI.ShowAnswerInput();
                    challengeUI.ShowHelp("ANSWER", answer);
                    challengeUI.FillAnswer(answer);
                    Debug.Log($"[Challenge] Answer shown on {activeQuestion.Id}");
                    HintShown?.Invoke();
                    break;
            }
        }

        /// <summary>Retry after a wrong answer: shows the same question again.</summary>
        public void Retry()
        {
            if (IsChallengeOpen && !activeSolved)
                challengeUI.ShowAnswerInput();
        }

        /// <summary>Continue after a correct answer: closes the challenge and unlocks the obstacle.</summary>
        public void Continue()
        {
            if (IsChallengeOpen && activeSolved)
                CloseChallenge();
        }

        /// <summary>
        /// Closes the challenge. A solved challenge unlocks its obstacle (or releases Alex from a hazard);
        /// an unsolved obstacle stays locked. A hazard's challenge can't be closed until it is solved.
        /// </summary>
        public void CloseChallenge()
        {
            if (!IsChallengeOpen)
                return;
            if (activeSource != null && activeSource.IsMandatory && !activeSolved)
            {
                Debug.Log("[Challenge] Caught - the challenge must be solved to escape.");
                return;
            }

            IChallengeSource obstacle = activeSource;
            QuestionData question = activeQuestion;
            bool solved = activeSolved;
            ClearActiveChallenge();

            if (solved && obstacle != null)
            {
                obstacle.OnChallengeSolved();
                if (obstacle.CountsAsObstacle)
                {
                    if (obstacle.HasClueReward)
                    {
                        Debug.Log($"[Challenge] Clue reward: {obstacle.ClueRewardId}");
                        ClueRewarded?.Invoke(obstacle.ClueRewardId, obstacle.ClueRewardText);
                    }
                    ChallengeCompleted?.Invoke(obstacle.ObstacleId, question);
                }
                else
                {
                    HazardEscaped?.Invoke(obstacle.ObstacleId);
                }
            }
            Debug.Log($"[Challenge] Closed {question.Id} (solved: {solved})");
            ChallengeClosed?.Invoke();
            // The window is gone, so the code's effect can be seen in the level: what the solving code did, or
            // (closed unsolved) what the last checked code did, so the bug is visible on the device.
            PlayPendingWorldEffect();
        }

        /// <summary>Clears score, hints and completed challenges (a new playthrough).</summary>
        public void ResetProgress()
        {
            completedObstacleIds.Clear();
            completedQuestionIds.Clear();
            hintedQuestionIds.Clear();
            unscoredObstacleIds.Clear();
            score = 0;
            hintsUsed = 0;
            SetStreak(0);
            ScoreChanged?.Invoke(score, hintsUsed);
        }

        /// <summary>Restores score, hints and solved challenges from a saved state (a level started from the level map).</summary>
        public void ApplyProgress(SaveSystem.ProgressData progress)
        {
            if (IsChallengeOpen)
                ClearActiveChallenge();
            Load(progress);
            ScoreChanged?.Invoke(score, hintsUsed);
        }

        // choiceIndex is the picked choice, or -1 for a typed answer.
        void Evaluate(bool correct, int choiceIndex, string feedbackOverride = null)
        {
            int pointsAwarded = 0;
            bool revealed = ActiveAnswerRevealed;
            if (!correct)
            {
                wrongAnswers[activeQuestion.Id] = ActiveWrongAnswers + 1;
                SetStreak(0);
            }
            if (correct)
            {
                activeSolved = true;
                // Wrong answers are counted per question, so closing and reopening doesn't make a first try.
                SetStreak(ActiveWrongAnswers > 0 || revealed ? 0 : streak + 1);
                // Escaping a hazard frees Alex but earns no points and isn't a solved obstacle.
                if (activeSource.CountsAsObstacle)
                {
                    string obstacleId = activeSource.ObstacleId;
                    bool firstTimeOpened = !string.IsNullOrEmpty(obstacleId) && !completedObstacleIds.Contains(obstacleId);
                    // A shown answer still opens the door, but earns no points.
                    if (completedQuestionIds.Add(activeQuestion.Id) && !revealed)
                        pointsAwarded = PointsPerCorrectAnswer;
                    if (revealed && firstTimeOpened)
                        unscoredObstacleIds.Add(obstacleId);
                    if (!string.IsNullOrEmpty(obstacleId))
                        completedObstacleIds.Add(obstacleId);
                    AddScore(pointsAwarded);
                }
            }

            // A correct answer explains why it is right. A wrong one explains the mistake in the chosen
            // answer instead, so the right answer isn't given away and the player can try again.
            string feedback = correct ? activeQuestion.Explanation : activeQuestion.GetWrongAnswerFeedback(choiceIndex);
            if (!correct && !string.IsNullOrWhiteSpace(feedbackOverride))
                feedback = feedbackOverride;
            if (!correct && string.IsNullOrWhiteSpace(feedback))
                feedback = WrongAnswerFallback;
            // Predict the output: after a correct prediction, show what the code really printed when the game ran it.
            // (Never after a wrong one, which would give the answer away.)
            if (correct && activeQuestion.TryGetRealOutput(out string realOutput))
                feedback += $"\n\n<color=#4ADE80>The game ran the code. It printed: <noparse>{realOutput.Replace("\n", " / ")}</noparse></color>";
            if (correct && revealed)
                feedback = "<color=#FACC15>The answer was shown, so no points this time. Read why it works:</color>\n" + feedback;

            Debug.Log($"[Challenge] {activeQuestion.Id} answered {(correct ? "correctly" : "incorrectly")}. Score {score}");
            challengeUI.ShowResult(correct, feedback, pointsAwarded, correct ? activeSource.ClueRewardText : null, activeSource.IsMandatory);
            AnswerChecked?.Invoke(correct);
        }

        void SetStreak(int value)
        {
            if (value == streak)
                return;
            streak = value;
            StreakChanged?.Invoke(streak);
        }

        void AddScore(int delta)
        {
            score = Mathf.Max(0, score + delta);
            if (challengeUI != null)
                challengeUI.SetScore(score, hintsUsed);
            ScoreChanged?.Invoke(score, hintsUsed);
        }

        void ClearActiveChallenge()
        {
            activeSource = null;
            activeQuestion = null;
            activeSolved = false;
            activeLesson = null;
            if (challengeUI != null)
                challengeUI.Hide();

            Time.timeScale = 1f;
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Challenge)
                GameManager.Instance.SetState(GameState.Playing);
        }

        bool CanStartChallenge()
        {
            if (SceneTransitionManager.Instance != null && SceneTransitionManager.Instance.IsTransitioning)
                return false;
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
                return false; // e.g. the pause menu is open
            return Time.timeScale > 0f;
        }

        bool CanAnswer()
        {
            // Answers only count once the learning card has been closed and the question is on screen.
            return IsChallengeOpen && !activeSolved && (challengeUI == null || !challengeUI.IsLessonOpen);
        }

        void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Defensive: a challenge never survives a scene change.
            if (IsChallengeOpen)
                ClearActiveChallenge();
        }

        void HandleLanguageChanged(ProgrammingLanguage language)
        {
            // Choosing a language starts a new playthrough.
            ResetProgress();
        }
    }
}
