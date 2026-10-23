using System.Collections;
using TMPro;
using UnityEngine;
using CodeEscape.Audio;
using CodeEscape.Programming;

namespace CodeEscape.Obstacles
{
    /// <summary>
    /// Shows what the player's code did in the level. Put it on a code lock and give the question the same effect
    /// ID. A small device is mounted on the wall by the lock from the start (dim, locked). When the challenge is
    /// solved and its window closes, the device plays the program's output and keeps showing the result; RUN
    /// previews it in the output console as one line.
    /// </summary>
    public abstract class CodeEffect : MonoBehaviour
    {
        public const string Keypad = "keypad";
        public const string Counter = "counter";
        public const string Lasers = "lasers";
        public const string Gate = "gate";
        public const string Function = "function";

        /// <summary>Every effect ID a question may use (checked by QuestionData.Validate).</summary>
        public static readonly string[] KnownIds = { Keypad, Counter, Lasers, Gate, Function };

        [SerializeField, Tooltip("Must match the question's effect ID.")]
        string effectId;
        [SerializeField, Tooltip("Where the device hangs, relative to this object: on the back wall beside the door " +
            "(positive z, just in front of the wall), above Alex's head and the door's \"Press E\" prompt.")]
        Vector3 offset = new Vector3(-1.6f, 1.9f, 1.55f);
        [SerializeField, Tooltip("World units per canvas pixel.")]
        float pixelSize = WorldPanel.DefaultPixelSize;
        [SerializeField, Tooltip("Outdoors (no wall behind): stand the device on a post down to the floor.")]
        bool standOnPost;
        [SerializeField] float floorY = -3.5f;
        [SerializeField] protected TMP_FontAsset font;
        [SerializeField] protected Color accent = new Color(0.13f, 0.83f, 0.93f);

        protected static readonly Color Pass = new Color(0.29f, 0.87f, 0.5f);
        protected static readonly Color Fail = new Color(0.97f, 0.44f, 0.44f);
        protected static readonly Color Dim = new Color(0.45f, 0.5f, 0.57f);

        WorldPanel panel;

        public string EffectId => effectId;

        protected TMP_FontAsset Font => font;
        protected WorldPanel Panel => panel;

        void Start()
        {
            EnsureDevice();
        }

        void OnDestroy()
        {
            if (panel != null)
                Destroy(panel.gameObject);
        }

        /// <summary>One line for the RUN console, e.g. "Keypad would type in: 15".</summary>
        public abstract string Describe(CodeExecution run);

        /// <summary>
        /// Plays the program's output on the device; the result stays on it. A wrong program (run.Passed false)
        /// plays too and ends in red, so the bug can be seen in the level.
        /// </summary>
        public void Play(CodeExecution run)
        {
            EnsureDevice();
            // The camera visits the device (between it and the door, so both are in view) and the device plays
            // there, so the player sees what their code did.
            SolveMoment.Enqueue(new SolveMoment.Moment
            {
                Point = Vector3.Lerp(panel.transform.position, transform.position, 0.35f),
                Begin = () =>
                {
                    panel.SetLit(true, run.Passed ? accent : Fail);
                    panel.Run(Animate(run));
                    if (run.Passed)
                        UnlockBurst.Play(panel.transform.position + new Vector3(0f, 0f, -0.3f), accent, 5f, 6f);
                },
                Busy = () => panel != null && panel.IsRunning,
                End = () =>
                {
                    if (AudioManager.Instance != null)
                        AudioManager.Instance.PlaySfx(run.Passed ? SfxType.Correct : SfxType.Alarm);
                },
                Hold = run.Passed ? 0.9f : 1.2f
            });
        }

        void EnsureDevice()
        {
            if (panel != null)
                return;
            panel = WorldPanel.Create(transform.position + offset, PanelSize, Title, accent, font, pixelSize);
            if (standOnPost)
                panel.AddPost(floorY);
            BuildDevice(panel);
            if (IsAlreadySolved())
            {
                panel.SetLit(true, accent);
                ShowSolved();
            }
            else
            {
                panel.SetLit(false, accent);
                ShowIdle();
            }
        }

        // A lock solved earlier in this playthrough (the scene was reloaded) shows its solved state.
        bool IsAlreadySolved()
        {
            ProgrammingObstacle obstacle = GetComponentInParent<ProgrammingObstacle>();
            ProgrammingChallengeManager manager = ProgrammingChallengeManager.Instance;
            return obstacle != null && manager != null && manager.IsObstacleCompleted(obstacle.ObstacleId);
        }

        protected abstract string Title { get; }
        protected virtual Vector2 PanelSize => new Vector2(300f, 220f);

        /// <summary>Creates the device's elements once (keep references to update them later).</summary>
        protected abstract void BuildDevice(WorldPanel device);
        /// <summary>Locked, waiting for the player's code.</summary>
        protected abstract void ShowIdle();
        /// <summary>Solved before (the program's output isn't kept, so no numbers).</summary>
        protected abstract void ShowSolved();
        /// <summary>Plays the program's output (run.Passed false: a wrong program, shown in red); the final frame stays on the device.</summary>
        protected abstract IEnumerator Animate(CodeExecution run);

        public static bool IsKnown(string id)
        {
            foreach (string known in KnownIds)
                if (known == id)
                    return true;
            return false;
        }
    }
}
