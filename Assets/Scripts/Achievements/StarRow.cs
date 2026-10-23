using UnityEngine;
using UnityEngine.UI;

namespace CodeEscape.Rewards
{
    /// <summary>
    /// A row of small diamond "stars" (drawn shapes, so no font glyph is needed). Filled diamonds show the
    /// stars earned, dim ones the stars still to earn. Builds its own child images.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class StarRow : MonoBehaviour
    {
        [SerializeField, Min(1)] int count = 3;
        [SerializeField, Min(4f)] float size = 16f;
        [SerializeField, Min(0f)] float spacing = 8f;
        [SerializeField] Color earnedColor = new Color(0.98f, 0.8f, 0.2f);
        [SerializeField] Color emptyColor = new Color(0.3f, 0.34f, 0.4f, 0.8f);

        Image[] diamonds;

        public int Earned { get; private set; }

        /// <summary>Creates a star row under <paramref name="parent"/> (used by screens built in code).</summary>
        public static StarRow Create(Transform parent, int count, float size, float spacing)
        {
            var go = new GameObject("Stars", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var row = go.AddComponent<StarRow>();
            row.count = count;
            row.size = size;
            row.spacing = spacing;
            row.Build();
            return row;
        }

        void Awake()
        {
            Build();
        }

        void Build()
        {
            if (diamonds != null && diamonds.Length == count)
                return;
            foreach (Transform child in transform)
                Destroy(child.gameObject);

            var rect = (RectTransform)transform;
            float step = size * 1.42f + spacing;
            rect.sizeDelta = new Vector2(step * count - spacing, size * 1.42f);
            diamonds = new Image[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Star" + i, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                var r = (RectTransform)go.transform;
                r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
                r.sizeDelta = new Vector2(size, size);
                r.anchoredPosition = new Vector2(size * 0.71f + step * i, 0f);
                r.localRotation = Quaternion.Euler(0f, 0f, 45f);
                Image image = go.GetComponent<Image>();
                image.raycastTarget = false;
                diamonds[i] = image;
            }
            Set(Earned);
        }

        public void Set(int earned)
        {
            Earned = Mathf.Clamp(earned, 0, count);
            if (diamonds == null)
                return;
            for (int i = 0; i < diamonds.Length; i++)
                diamonds[i].color = i < Earned ? earnedColor : emptyColor;
        }

        public void SetColors(Color earned, Color empty)
        {
            earnedColor = earned;
            emptyColor = empty;
            Set(Earned);
        }
    }
}
