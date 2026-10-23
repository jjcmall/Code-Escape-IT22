using System.Collections;
using UnityEngine;
using TMPro;
using CodeEscape.Programming;

namespace CodeEscape.Obstacles
{
    /// <summary>
    /// A counter on the wall that shows the program's inputs and counts up to the number it printed. Used by
    /// questions whose program prints one number (arithmetic, data types, a loop total): it appears on the counter.
    /// </summary>
    public class CounterWallEffect : CodeEffect
    {
        [SerializeField] string title = "WAREHOUSE COUNTER";
        [SerializeField] string valueLabel = "TOTAL";

        static readonly Color Amber = new Color(0.98f, 0.8f, 0.2f);

        TMP_Text inputs;
        TMP_Text label;
        TMP_Text number;

        protected override string Title => title;
        protected override Vector2 PanelSize => new Vector2(320f, 220f);

        public override string Describe(CodeExecution run)
        {
            string value = run.Lines.Count > 0 ? run.Lines[0] : "(nothing)";
            return $"{title} would show: {valueLabel} {value}";
        }

        protected override void BuildDevice(WorldPanel device)
        {
            inputs = device.AddText("Inputs", string.Empty, Font, 17f, new Color(0.6f, 0.68f, 0.77f), new Vector2(0f, 52f), new Vector2(290f, 22f), TextAlignmentOptions.Center);
            label = device.AddText("Label", valueLabel, Font, 17f, accent, new Vector2(0f, 24f), new Vector2(290f, 20f), TextAlignmentOptions.Center);
            device.AddImage("Readout", new Vector2(0f, -32f), new Vector2(220f, 68f), new Color(0f, 0.02f, 0.03f, 1f));
            number = device.AddText("Number", string.Empty, Font, 50f, Amber, new Vector2(0f, -32f), new Vector2(210f, 64f), TextAlignmentOptions.Center);
        }

        protected override void ShowIdle()
        {
            inputs.text = "WAITING FOR CODE";
            number.text = "--";
            number.color = Dim;
        }

        protected override void ShowSolved()
        {
            inputs.text = string.Empty;
            number.text = "DONE";
            number.color = Pass;
        }

        protected override IEnumerator Animate(CodeExecution run)
        {
            inputs.text = $"<noparse>{run.SetupSummary}</noparse>";
            label.text = valueLabel;
            label.color = accent;
            number.color = Amber;
            if (run.Numbers.Count == 0)
                number.text = run.Lines.Count > 0 ? $"<noparse>{run.Lines[0]}</noparse>" : "-";
            else
            {
                long target = run.Numbers[0];
                const float duration = 0.9f;
                for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
                {
                    float eased = 1f - (1f - t / duration) * (1f - t / duration);
                    number.text = Mathf.RoundToInt(Mathf.Lerp(0f, target, eased)).ToString();
                    yield return null;
                }
                number.text = target.ToString();
            }
            if (!run.Passed)
            {
                // The wrong program's number stays on the counter, marked as rejected.
                number.color = Fail;
                label.text = valueLabel + " - WRONG";
                label.color = Fail;
            }
        }
    }
}
