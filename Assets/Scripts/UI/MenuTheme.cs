using UnityEngine;

namespace CodeEscape.UI
{
    /// <summary>
    /// The main menu's look, shared by the UI that is built in code: colours, the thin-outline and panel sprites
    /// (9-sliced), the hexagon badge and the soft glow, and the Orbitron font for legacy uGUI text. Scenes and prefabs
    /// use the same sprites and colours directly.
    /// </summary>
    public static class MenuTheme
    {
        public static readonly Color Ink = Color.white;
        /// <summary>Secondary text, icons and primary-action outlines.</summary>
        public static readonly Color Ice = new Color(0.72f, 0.9f, 1f);
        /// <summary>See-through panel and pill background.</summary>
        public static readonly Color Panel = new Color(0.04f, 0.1f, 0.18f, 0.82f);
        /// <summary>A darker, nearly solid panel for windows that cover the game.</summary>
        public static readonly Color PanelSolid = new Color(0.03f, 0.07f, 0.13f, 1f);
        public static readonly Color Line = new Color(1f, 1f, 1f, 0.35f);
        public static readonly Color Danger = new Color(1f, 0.55f, 0.55f);
        public static readonly Color Good = new Color(0.43f, 0.91f, 0.6f);
        public static readonly Color Amber = new Color(1f, 0.82f, 0.29f);

        static Sprite outline, panel, hexFill, hexRing, glow;
        static Font font;

        /// <summary>2 px border with the pills' cut corner, 9-sliced (a copy of NeonBorder).</summary>
        public static Sprite Outline => outline != null ? outline : outline = Resources.Load<Sprite>("UI/menu_outline");
        /// <summary>The matching fill, 9-sliced (a copy of NeonFill).</summary>
        public static Sprite PanelSprite => panel != null ? panel : panel = Resources.Load<Sprite>("UI/menu_panel");
        public static Sprite HexFill => hexFill != null ? hexFill : hexFill = Resources.Load<Sprite>("UI/menu_hex_fill");
        public static Sprite HexRing => hexRing != null ? hexRing : hexRing = Resources.Load<Sprite>("UI/menu_hex_ring");
        public static Sprite Glow => glow != null ? glow : glow = Resources.Load<Sprite>("UI/menu_glow");
        /// <summary>Orbitron for legacy uGUI Text (TextMeshPro text uses the Orbitron SDF asset).</summary>
        public static Font Font => font != null ? font : font = Resources.Load<Font>("Fonts/Orbitron-Variable");

        public static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }
    }
}
