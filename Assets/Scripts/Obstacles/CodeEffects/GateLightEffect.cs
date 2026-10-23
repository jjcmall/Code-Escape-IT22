using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CodeEscape.Programming;

namespace CodeEscape.Obstacles
{
    /// <summary>
    /// A gate signal on the wall: shows the values the program was checked with, blinks while it "decides", then
    /// shows what the program printed with a green (right) or red (wrong) light. Used by condition, comparison
    /// and boolean questions, whose program prints a decision such as Allowed or True.
    /// </summary>
    public class GateLightEffect : CodeEffect
    {
        [SerializeField] string title = "SECURITY GATE";

        static readonly Color Amber = new Color(0.98f, 0.8f, 0.2f);

        TMP_Text inputs;
        TMP_Text result;
        TMP_Text status;
        Image lamp;
        Image lampGlow;

        protected override string Title => title;
        protected override Vector2 PanelSize => new Vector2(330f, 210f);

        public override string Describe(CodeExecution run)
        {
            string value = run.Lines.Count > 0 ? run.Lines[0] : "(nothing)";
            return $"{title} light would show: {value}";
        }

        protected override void BuildDevice(WorldPanel device)
        {
            inputs = device.AddText("Inputs", string.Empty, Font, 17f, new Color(0.6f, 0.68f, 0.77f), new Vector2(0f, 50f), new Vector2(300f, 22f), TextAlignmentOptions.Center);
            // A diamond signal lamp with a soft halo (no sprite needed).
            lampGlow = device.AddImage("LampGlow", new Vector2(-96f, -8f), new Vector2(70f, 70f), Color.clear);
            lampGlow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            lamp = device.AddImage("Lamp", new Vector2(-96f, -8f), new Vector2(46f, 46f), Dim);
            lamp.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            device.AddImage("Readout", new Vector2(40f, -8f), new Vector2(190f, 56f), new Color(0f, 0.02f, 0.03f, 1f));
            result = device.AddText("Result", string.Empty, Font, 30f, Dim, new Vector2(40f, -8f), new Vector2(180f, 52f), TextAlignmentOptions.Center);
            status = device.AddText("Status", string.Empty, Font, 18f, Dim, new Vector2(0f, -66f), new Vector2(310f, 22f), TextAlignmentOptions.Center);
        }

        protected override void ShowIdle()
        {
            inputs.text = "WAITING FOR CODE";
            SetLamp(Dim, false);
            result.text = "--";
            result.color = Dim;
            status.text = "GATE LOCKED";
            status.color = Fail;
        }

        protected override void ShowSolved()
        {
            inputs.text = string.Empty;
            SetLamp(Pass, true);
            result.text = "OPEN";
            result.color = Pass;
            status.text = "GATE OPEN";
            status.color = Pass;
        }

        protected override IEnumerator Animate(CodeExecution run)
        {
            inputs.text = $"<noparse>{run.SetupSummary}</noparse>";
            result.text = string.Empty;
            status.text = "CHECKING...";
            status.color = accent;
            for (int i = 0; i < 3; i++)
            {
                SetLamp(Amber, true);
                yield return WorldPanel.Wait(0.16f);
                SetLamp(Dim, false);
                yield return WorldPanel.Wait(0.12f);
            }

            Color outcome = run.Passed ? Pass : Fail;
            SetLamp(outcome, true);
            result.text = run.Lines.Count > 0 ? $"<noparse>{run.Lines[0]}</noparse>" : "(nothing)";
            result.color = outcome;
            status.text = run.Passed ? "GATE OPEN" : "WRONG RESULT - GATE SHUT";
            status.color = outcome;
        }

        void SetLamp(Color color, bool lit)
        {
            lamp.color = color;
            lampGlow.color = lit ? new Color(color.r, color.g, color.b, 0.3f) : Color.clear;
        }
    }
}
