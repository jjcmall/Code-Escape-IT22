using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CodeEscape.Programming;

namespace CodeEscape.Obstacles
{
    /// <summary>
    /// A laser grid control screen that lights one beam for every line the program printed, so each turn of the
    /// player's loop is visible. Used by a loops question.
    /// </summary>
    public class LaserGridEffect : CodeEffect
    {
        const int MaxBeams = 12;
        const int IdleBeams = 5;
        const float GridWidth = 300f;

        [SerializeField] string title = "LASER GRID";
        [SerializeField] Color beamColor = new Color(0.97f, 0.27f, 0.27f);

        WorldPanel device;
        TMP_Text status;
        readonly List<GameObject> beamParts = new List<GameObject>();

        protected override string Title => title;
        protected override Vector2 PanelSize => new Vector2(340f, 230f);

        public override string Describe(CodeExecution run)
        {
            int n = run.Lines.Count;
            return $"Laser grid would light {n} beam{(n == 1 ? string.Empty : "s")} (one per printed line)";
        }

        protected override void BuildDevice(WorldPanel panel)
        {
            device = panel;
            status = panel.AddText("Status", string.Empty, Font, 18f, Dim, new Vector2(0f, -90f), new Vector2(320f, 22f), TextAlignmentOptions.Center);
        }

        protected override void ShowIdle()
        {
            MakeBeams(IdleBeams, null);
            status.text = "GRID OFFLINE";
            status.color = Dim;
        }

        protected override void ShowSolved()
        {
            MakeBeams(IdleBeams, null);
            status.text = "GRID DISARMED";
            status.color = Pass;
        }

        protected override IEnumerator Animate(CodeExecution run)
        {
            int n = Mathf.Min(run.Lines.Count, MaxBeams);
            Image[] beams = MakeBeams(n, run.Lines);
            for (int i = 0; i < n; i++)
            {
                beams[i].color = beamColor;
                status.text = $"LOOP TURN {i + 1}";
                status.color = accent;
                yield return WorldPanel.Wait(0.28f);
            }
            string count = $"{n} BEAM{(n == 1 ? string.Empty : "S")}";
            if (n == 0)
                status.text = "NO BEAMS - THE LOOP PRINTED NOTHING";
            else
                status.text = run.Passed ? $"GRID ONLINE: {count}" : $"WRONG: {count}";
            if (run.Lines.Count > MaxBeams)
                status.text += $" (+{run.Lines.Count - MaxBeams})";
            status.color = run.Passed && n > 0 ? Pass : Fail;
        }

        // Replaces the beams with n unlit ones, labelled with the printed lines when given.
        Image[] MakeBeams(int n, IReadOnlyList<string> labels)
        {
            foreach (GameObject part in beamParts)
                Destroy(part);
            beamParts.Clear();

            Color off = new Color(beamColor.r, beamColor.g, beamColor.b, 0.15f);
            float spacing = GridWidth / Mathf.Max(n, 1);
            float startX = -spacing * (n - 1) * 0.5f;
            var beams = new Image[n];
            for (int i = 0; i < n; i++)
            {
                float x = startX + spacing * i;
                beams[i] = device.AddImage("Beam" + i, new Vector2(x, 14f), new Vector2(Mathf.Min(8f, spacing * 0.4f), 104f), off);
                beamParts.Add(beams[i].gameObject);
                if (labels != null && i < labels.Count)
                {
                    TMP_Text label = device.AddText("BeamLabel" + i, $"<noparse>{labels[i]}</noparse>", Font, 16f, new Color(0.72f, 0.78f, 0.85f), new Vector2(x, -56f), new Vector2(Mathf.Max(24f, spacing), 20f), TextAlignmentOptions.Center);
                    beamParts.Add(label.gameObject);
                }
            }
            return beams;
        }
    }
}
