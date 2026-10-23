using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CodeEscape.Audio;
using CodeEscape.Programming;
using CodeEscape.UI;

namespace CodeEscape.Obstacles
{
    /// <summary>
    /// The final boss: SENTINEL's core, a large screen in the last level with one shield per code lock there.
    /// Each lock solved breaks its shield (the camera visits the core, it shakes, SENTINEL gets desperate); closing
    /// a lock after wrong code makes SENTINEL "repair" (just a taunt - no progress is lost). With every shield down
    /// the core goes offline. Driven by ProgrammingChallengeManager.CodeExecuted, so it follows the doors' devices.
    /// </summary>
    public class SentinelCore : MonoBehaviour
    {
        static readonly Color Red = new Color(0.97f, 0.27f, 0.27f);
        static readonly Color Amber = new Color(0.98f, 0.8f, 0.2f);
        static readonly Color Off = new Color(0.22f, 0.25f, 0.3f);
        static readonly string[] BreakLines =
        {
            "Shield one down? A lucky guess.",
            "Two shields... who taught you to code?!",
            "No... NO! Core... going... offline..."
        };
        static readonly string[] RepairLines = { "Wrong. Shields holding.", "Your code failed. I grow stronger.", "Error detected. Not mine - yours." };

        [SerializeField, Tooltip("The code locks guarding the core, one shield each (their obstacle IDs).")]
        string[] shieldObstacleIds = new string[0];
        [SerializeField] string[] shieldLabels = new string[0];
        [SerializeField, Tooltip("World position of the core's screen (on the back wall, beyond the last door, at device height).")]
        Vector3 screenPosition = new Vector3(6.3f, 0.3f, 1.8f);
        [SerializeField] TMP_FontAsset font;
        [SerializeField] float pixelSize = WorldPanel.DefaultPixelSize;

        WorldPanel panel;
        Image eye;
        Image eyeGlow;
        Image[] bars;
        TMP_Text[] barLabels;
        TMP_Text speech;
        bool[] broken;
        bool animating;

        int BrokenCount
        {
            get
            {
                int n = 0;
                foreach (bool b in broken)
                    if (b) n++;
                return n;
            }
        }

        void OnEnable()
        {
            ProgrammingChallengeManager.CodeExecuted += HandleCodeExecuted;
        }

        void OnDisable()
        {
            ProgrammingChallengeManager.CodeExecuted -= HandleCodeExecuted;
        }

        void Start()
        {
            Build();
            ProgrammingChallengeManager manager = ProgrammingChallengeManager.Instance;
            for (int i = 0; i < broken.Length; i++)
                broken[i] = manager != null && manager.IsObstacleCompleted(shieldObstacleIds[i]);
            ShowState();
        }

        void OnDestroy()
        {
            if (panel != null)
                Destroy(panel.gameObject);
        }

        void HandleCodeExecuted(CodeExecution run)
        {
            if (panel == null || run == null || run.Source == null)
                return;
            int index = System.Array.IndexOf(shieldObstacleIds, run.Source.ObstacleId);
            if (index < 0)
                return;
            if (run.Passed && !broken[index])
                Enqueue(() => StartCoroutine(Break(index)));
            else if (!run.Passed && BrokenCount < broken.Length)
                Enqueue(() => StartCoroutine(Repair()));
        }

        void Enqueue(System.Action begin)
        {
            SolveMoment.Enqueue(new SolveMoment.Moment
            {
                Point = panel.transform.position + new Vector3(0f, -0.4f, 0f),
                SizeFactor = 0.85f,
                Begin = () => { animating = true; begin(); },
                Busy = () => animating,
                Hold = 1.1f
            });
        }

        IEnumerator Break(int index)
        {
            broken[index] = true;
            int down = BrokenCount;
            bool last = down == broken.Length;
            Say(BreakLines[Mathf.Clamp(down - 1, 0, BreakLines.Length - 1)]);
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySfx(SfxType.Door);
            StartCoroutine(panel.Shake(last ? 0.12f : 0.07f, last ? 0.9f : 0.5f));
            for (int i = 0; i < 3; i++)
            {
                bars[index].color = Color.white;
                yield return WorldPanel.Wait(0.08f);
                bars[index].color = Red;
                yield return WorldPanel.Wait(0.08f);
            }
            SetBar(index);
            UnlockBurst.Play(panel.transform.position + new Vector3(0f, 0f, -0.4f), last ? Amber : Red, last ? 10f : 5f, last ? 9f : 6f);
            if (last)
            {
                for (int i = 0; i < 5; i++)
                {
                    SetEye(i % 2 == 0 ? Off : Red, i % 2 != 0);
                    yield return WorldPanel.Wait(0.12f);
                }
            }
            ShowState();
            animating = false;
        }

        IEnumerator Repair()
        {
            Say(RepairLines[Random.Range(0, RepairLines.Length)]);
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySfx(SfxType.Alarm);
            for (int pulse = 0; pulse < 3; pulse++)
            {
                for (int i = 0; i < bars.Length; i++)
                    if (!broken[i]) bars[i].color = Amber;
                SetEye(Amber, true);
                yield return WorldPanel.Wait(0.14f);
                for (int i = 0; i < bars.Length; i++)
                    SetBar(i);
                SetEye(Red, true);
                yield return WorldPanel.Wait(0.14f);
            }
            animating = false;
        }

        // ---------- Display ----------

        void ShowState()
        {
            int down = BrokenCount;
            for (int i = 0; i < bars.Length; i++)
                SetBar(i);
            if (down == broken.Length)
            {
                SetEye(Off, false);
                speech.text = "CORE OFFLINE";
                speech.color = new Color(0.29f, 0.87f, 0.5f);
                speech.fontStyle = FontStyles.Bold;
            }
            else
            {
                SetEye(Red, true);
                if (string.IsNullOrEmpty(speech.text) || speech.fontStyle == FontStyles.Bold)
                    Say(down == 0 ? "No intruder gets past my shields." : "Shields failing... stay back!", false);
            }
        }

        // The line shows on the core's screen; during a moment it also pops up as a SENTINEL message.
        void Say(string line, bool notify = true)
        {
            speech.fontStyle = FontStyles.Italic;
            speech.color = new Color(1f, 0.55f, 0.55f);
            speech.text = $"\"<noparse>{line}</noparse>\"";
            if (notify && NotificationUI.Instance != null)
                NotificationUI.Instance.Show("SENTINEL", line, Red, 3f);
        }

        void SetBar(int i)
        {
            bars[i].color = broken[i] ? Off : Red;
            barLabels[i].text = $"<noparse>{LabelOf(i)}</noparse>  {(broken[i] ? "DOWN" : "ACTIVE")}";
            barLabels[i].color = broken[i] ? new Color(0.55f, 0.6f, 0.68f) : Color.white;
        }

        string LabelOf(int i) => i < shieldLabels.Length && !string.IsNullOrEmpty(shieldLabels[i]) ? shieldLabels[i] : "SHIELD " + (i + 1);

        void SetEye(Color color, bool glowing)
        {
            eye.color = color;
            eyeGlow.color = glowing ? new Color(color.r, color.g, color.b, 0.3f) : Color.clear;
        }

        void Build()
        {
            broken = new bool[shieldObstacleIds.Length];
            bars = new Image[shieldObstacleIds.Length];
            barLabels = new TMP_Text[shieldObstacleIds.Length];

            var size = new Vector2(460f, 230f);
            panel = WorldPanel.Create(screenPosition, size, "SENTINEL CORE", Red, font, pixelSize);

            // The eye: a large diamond with a dark pupil and a glow.
            eyeGlow = panel.AddImage("EyeGlow", new Vector2(-150f, 6f), new Vector2(118f, 118f), Color.clear);
            eyeGlow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            eye = panel.AddImage("Eye", new Vector2(-150f, 6f), new Vector2(84f, 84f), Red);
            eye.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image pupil = panel.AddImage("Pupil", new Vector2(-150f, 6f), new Vector2(30f, 30f), new Color(0.05f, 0.02f, 0.02f));
            pupil.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            // One shield bar per lock.
            int count = bars.Length;
            for (int i = 0; i < count; i++)
            {
                float y = 44f - i * 40f;
                bars[i] = panel.AddImage("Shield" + i, new Vector2(70f, y), new Vector2(250f, 32f), Red);
                barLabels[i] = panel.AddText("ShieldLabel" + i, string.Empty, font, 16f, Color.white, new Vector2(70f, y), new Vector2(240f, 30f), TextAlignmentOptions.Center);
            }
            speech = panel.AddText("Speech", string.Empty, font, 17f, Red, new Vector2(0f, -86f), new Vector2(430f, 24f), TextAlignmentOptions.Center);
        }
    }
}
