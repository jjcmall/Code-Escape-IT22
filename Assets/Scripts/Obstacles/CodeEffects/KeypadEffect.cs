using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CodeEscape.Programming;

namespace CodeEscape.Obstacles
{
    /// <summary>
    /// A door keypad on the wall that types in whatever the player's program printed, pressing each key in turn,
    /// then grants access. Used by a variables question: the printed value is the access code.
    /// </summary>
    public class KeypadEffect : CodeEffect
    {
        [SerializeField] string title = "HALLWAY KEYPAD";
        static readonly string[] Keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "-", "0", "OK" };
        static readonly Color KeyOff = new Color(0.13f, 0.17f, 0.22f);

        TMP_Text display;
        TMP_Text status;
        Image[] keys;

        protected override string Title => title;
        // Wide and short (keys in two rows of six), so it fits between the ceiling and the door's prompt.
        protected override Vector2 PanelSize => new Vector2(360f, 210f);

        public override string Describe(CodeExecution run)
        {
            string code = run.Lines.Count > 0 ? run.Lines[0] : "(nothing)";
            return $"Keypad would type in: {code}";
        }

        protected override void BuildDevice(WorldPanel device)
        {
            device.AddImage("Display", new Vector2(0f, 50f), new Vector2(320f, 42f), new Color(0f, 0.02f, 0.03f, 1f));
            display = device.AddText("Digits", string.Empty, Font, 38f, Pass, new Vector2(0f, 50f), new Vector2(310f, 40f), TextAlignmentOptions.Center);

            keys = new Image[Keys.Length];
            for (int i = 0; i < Keys.Length; i++)
            {
                int col = i % 6, row = i / 6;
                Vector2 pos = new Vector2(-137.5f + col * 55f, 6f - row * 30f);
                keys[i] = device.AddImage("Key" + Keys[i], pos, new Vector2(48f, 24f), KeyOff);
                device.AddText("KeyLabel" + Keys[i], Keys[i], Font, 17f, new Color(0.7f, 0.76f, 0.83f), pos, new Vector2(48f, 24f), TextAlignmentOptions.Center);
            }
            status = device.AddText("Status", string.Empty, Font, 22f, Dim, new Vector2(0f, -64f), new Vector2(330f, 24f), TextAlignmentOptions.Center);
        }

        protected override void ShowIdle()
        {
            display.text = "- - - -";
            display.color = Dim;
            status.text = "LOCKED";
            status.color = Fail;
        }

        protected override void ShowSolved()
        {
            display.text = "* * * *";
            display.color = Pass;
            keys[Keys.Length - 1].color = Pass;
            status.text = "ACCESS GRANTED";
            status.color = Pass;
        }

        protected override IEnumerator Animate(CodeExecution run)
        {
            string code = run.Lines.Count > 0 ? run.Lines[0] : string.Empty;
            display.color = Pass;
            display.text = string.Empty;
            status.text = "ENTERING CODE";
            status.color = accent;

            string typed = string.Empty;
            foreach (char c in code)
            {
                int index = System.Array.IndexOf(Keys, c.ToString());
                typed += c;
                display.text = $"<noparse>{typed}</noparse>";
                if (index >= 0)
                    keys[index].color = accent;
                yield return WorldPanel.Wait(0.22f);
                if (index >= 0)
                    keys[index].color = KeyOff;
            }
            keys[Keys.Length - 1].color = run.Passed ? Pass : Fail;
            if (!run.Passed && code.Length == 0)
                display.text = "(nothing)";
            display.color = run.Passed ? Pass : Fail;
            status.text = run.Passed ? "ACCESS GRANTED" : "WRONG CODE: DENIED";
            status.color = run.Passed ? Pass : Fail;
        }
    }
}
