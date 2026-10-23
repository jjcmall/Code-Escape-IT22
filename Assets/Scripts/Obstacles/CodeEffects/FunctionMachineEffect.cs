using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CodeEscape.Programming;

namespace CodeEscape.Obstacles
{
    /// <summary>
    /// A function machine on the wall: the input goes in, the machine (named after the player's function) runs,
    /// and the value the program printed comes out. Used by function questions, so calling a function you wrote
    /// is something you can watch.
    /// </summary>
    public class FunctionMachineEffect : CodeEffect
    {
        [SerializeField] string title = "FUNCTION MACHINE";
        [SerializeField, Tooltip("The call shown on the machine, e.g. double(num).")]
        string callLabel = "double(num)";

        static readonly Color Box = new Color(0f, 0.02f, 0.03f, 1f);
        static readonly Color Amber = new Color(0.98f, 0.8f, 0.2f);

        TMP_Text input;
        TMP_Text output;
        TMP_Text status;
        Image machine;
        TMP_Text arrows;

        protected override string Title => title;
        protected override Vector2 PanelSize => new Vector2(360f, 210f);

        public override string Describe(CodeExecution run)
        {
            string value = run.Lines.Count > 0 ? run.Lines[0] : "(nothing)";
            return $"Function machine would output: {value}";
        }

        protected override void BuildDevice(WorldPanel device)
        {
            Color caption = new Color(0.55f, 0.6f, 0.68f);
            device.AddImage("InputBox", new Vector2(-118f, 26f), new Vector2(96f, 50f), Box);
            input = device.AddText("Input", string.Empty, Font, 17f, new Color(0.75f, 0.8f, 0.86f), new Vector2(-118f, 26f), new Vector2(92f, 48f), TextAlignmentOptions.Center);
            device.AddText("InputCaption", "IN", Font, 14f, caption, new Vector2(-118f, -10f), new Vector2(96f, 18f), TextAlignmentOptions.Center);

            machine = device.AddImage("Machine", new Vector2(0f, 26f), new Vector2(118f, 60f), new Color(0.13f, 0.17f, 0.22f));
            device.AddText("Call", $"<noparse>{callLabel}</noparse>", Font, 16f, accent, new Vector2(0f, 26f), new Vector2(114f, 56f), TextAlignmentOptions.Center);
            arrows = device.AddText("Arrows", string.Empty, Font, 16f, accent, new Vector2(0f, -10f), new Vector2(118f, 18f), TextAlignmentOptions.Center);

            device.AddImage("OutputBox", new Vector2(118f, 26f), new Vector2(96f, 50f), Box);
            output = device.AddText("Output", string.Empty, Font, 30f, Amber, new Vector2(118f, 26f), new Vector2(92f, 48f), TextAlignmentOptions.Center);
            device.AddText("OutputCaption", "OUT", Font, 14f, caption, new Vector2(118f, -10f), new Vector2(96f, 18f), TextAlignmentOptions.Center);

            status = device.AddText("Status", string.Empty, Font, 18f, Dim, new Vector2(0f, -56f), new Vector2(330f, 22f), TextAlignmentOptions.Center);
        }

        protected override void ShowIdle()
        {
            input.text = "?";
            output.text = "?";
            output.color = Dim;
            arrows.text = string.Empty;
            status.text = "WAITING FOR CODE";
            status.color = Fail;
        }

        protected override void ShowSolved()
        {
            input.text = "-";
            output.text = "OK";
            output.color = Pass;
            arrows.text = string.Empty;
            status.text = "FUNCTION READY";
            status.color = Pass;
        }

        protected override IEnumerator Animate(CodeExecution run)
        {
            input.text = $"<noparse>{run.SetupSummary}</noparse>";
            output.text = string.Empty;
            status.text = "CALLING " + callLabel.ToUpperInvariant();
            status.color = accent;
            Color idle = machine.color;
            for (int i = 1; i <= 3; i++)
            {
                arrows.text = new string('>', i);
                machine.color = Color.Lerp(idle, accent, 0.45f);
                yield return WorldPanel.Wait(0.14f);
                machine.color = idle;
                yield return WorldPanel.Wait(0.1f);
            }
            arrows.text = string.Empty;
            output.text = run.Lines.Count > 0 ? $"<noparse>{run.Lines[0]}</noparse>" : "-";
            output.color = run.Passed ? Amber : Fail;
            status.text = run.Passed ? $"RETURNED {(run.Lines.Count > 0 ? run.Lines[0] : "-")}" : "WRONG RESULT";
            status.color = run.Passed ? Pass : Fail;
            if (run.Passed)
                status.text = $"<noparse>{status.text}</noparse>";
        }
    }
}
