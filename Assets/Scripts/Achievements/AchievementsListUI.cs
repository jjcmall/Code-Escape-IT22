using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CodeEscape.Rewards
{
    /// <summary>
    /// The Achievements page (main menu): a summary line (achievements earned and level stars) and every
    /// achievement in two columns - a filled diamond and its progress when earned, a dim one with the
    /// progress so far when not. Rebuilt each time it opens. Rows are built in code.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class AchievementsListUI : MonoBehaviour
    {
        [SerializeField] TMP_FontAsset headerFont;
        [SerializeField] TMP_FontAsset bodyFont;
        [SerializeField] Color earnedColor = new Color(0.98f, 0.8f, 0.2f);
        [SerializeField] Color lockedColor = new Color(0.42f, 0.47f, 0.54f);
        [SerializeField] Color textColor = new Color(0.9f, 0.93f, 0.97f);
        [SerializeField, Min(1)] int columns = 2;

        readonly List<GameObject> built = new List<GameObject>();

        void OnEnable()
        {
            Refresh();
        }

        public void Refresh()
        {
            foreach (GameObject go in built)
                Destroy(go);
            built.Clear();

            AchievementManager manager = AchievementManager.Instance;
            var rect = (RectTransform)transform;
            float width = rect.rect.width > 1f ? rect.rect.width : 1000f;
            float height = rect.rect.height > 1f ? rect.rect.height : 410f;

            if (manager == null)
            {
                AddText("Summary", "Achievements are not available right now.", bodyFont, 22f, lockedColor,
                    new Vector2(0f, 0f), new Vector2(width, 40f), TextAlignmentOptions.Top);
                return;
            }

            IReadOnlyList<AchievementDefinition> all = manager.Definitions;
            int earned = 0;
            foreach (AchievementDefinition d in all)
                if (manager.IsUnlocked(d.Id))
                    earned++;
            int levelCount = Mathf.Max(1, AchievementCatalog.LevelCount);
            AddText("Summary", $"<color=#FACC15>{earned} / {all.Count}</color> ACHIEVEMENTS EARNED      LEVEL STARS  <color=#FACC15>{manager.TotalStars} / {levelCount * AchievementManager.MaxStarsPerLevel}</color>",
                headerFont, 20f, textColor, new Vector2(0f, 0f), new Vector2(width, 32f), TextAlignmentOptions.Top);

            int rows = Mathf.CeilToInt(all.Count / (float)columns);
            float top = 44f;
            float rowHeight = Mathf.Min(34f, (height - top) / Mathf.Max(1, rows));
            float columnWidth = width / columns;
            for (int i = 0; i < all.Count; i++)
            {
                AchievementDefinition d = all[i];
                int column = i / rows;
                int row = i % rows;
                bool unlocked = manager.IsUnlocked(d.Id);
                int progress = manager.ProgressOf(d);
                Vector2 origin = new Vector2(column * columnWidth, top + row * rowHeight);

                var diamond = new GameObject("Badge_" + d.Id, typeof(RectTransform), typeof(Image));
                diamond.transform.SetParent(transform, false);
                var dr = (RectTransform)diamond.transform;
                dr.anchorMin = dr.anchorMax = new Vector2(0f, 1f);
                dr.sizeDelta = new Vector2(13f, 13f);
                dr.anchoredPosition = new Vector2(origin.x + 14f, -(origin.y + rowHeight * 0.5f));
                dr.localRotation = Quaternion.Euler(0f, 0f, 45f);
                Image image = diamond.GetComponent<Image>();
                image.color = unlocked ? earnedColor : new Color(lockedColor.r, lockedColor.g, lockedColor.b, 0.45f);
                image.raycastTarget = false;
                built.Add(diamond);

                string status = unlocked ? "EARNED" : d.Target > 1 ? $"{progress}/{d.Target}" : string.Empty;
                // Concept badges are self-explanatory ("Loops: for and range Badge"), so they show the name only.
                string label = d.Category == AchievementCategory.Concept
                    ? $"<b>{Escape(d.Name)}</b>"
                    : $"<b>{Escape(d.Name)}</b>  <color=#{ColorUtility.ToHtmlStringRGB(lockedColor)}><size=85%>{Escape(d.Description)}</size></color>";
                const float statusWidth = 96f;
                AddText("Name_" + d.Id, label, bodyFont, 18f, unlocked ? textColor : new Color(textColor.r, textColor.g, textColor.b, 0.6f),
                    new Vector2(origin.x + 32f, origin.y), new Vector2(columnWidth - 32f - statusWidth - 12f, rowHeight), TextAlignmentOptions.MidlineLeft);
                AddText("Status_" + d.Id, status, headerFont, 14f, unlocked ? earnedColor : lockedColor,
                    new Vector2(origin.x + columnWidth - statusWidth - 10f, origin.y), new Vector2(statusWidth, rowHeight), TextAlignmentOptions.MidlineRight);
            }
        }

        static string Escape(string text) => text?.Replace("<", "&lt;") ?? string.Empty;

        void AddText(string name, string value, TMP_FontAsset font, float size, Color color, Vector2 topLeft, Vector2 dimensions, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(topLeft.x, -topLeft.y);
            r.sizeDelta = dimensions;
            var text = go.AddComponent<TextMeshProUGUI>();
            if (font != null)
                text.font = font;
            text.fontSize = size;
            text.color = color;
            text.richText = true;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            // Ellipsis, so a line that doesn't fit ends in "..." instead of mid-word.
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.alignment = alignment;
            text.text = value;
            built.Add(go);
        }
    }
}
