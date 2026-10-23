using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CodeEscape.Obstacles
{
    /// <summary>
    /// A small screen mounted in the level (world-space canvas) that a CodeEffect draws on: a dark metal bezel
    /// with a thin accent edge, a label strip and a screen. It stays in the level; animations run in unscaled time.
    /// </summary>
    public class WorldPanel : MonoBehaviour
    {
        /// <summary>World units per canvas pixel: a 300 px wide device is about 1.7 units wide, readable from the gameplay camera.</summary>
        public const float DefaultPixelSize = 0.0055f;

        static readonly Color Bezel = new Color(0.09f, 0.11f, 0.14f, 1f);
        static readonly Color Screen = new Color(0.02f, 0.04f, 0.06f, 1f);

        RectTransform content;
        Image edge;
        TMP_Text title;
        Coroutine running;

        public RectTransform Content => content;

        /// <summary>Creates a device centred at <paramref name="worldPosition"/>, <paramref name="size"/> in canvas pixels.</summary>
        public static WorldPanel Create(Vector3 worldPosition, Vector2 size, string titleText, Color accent, TMP_FontAsset font, float pixelSize = DefaultPixelSize)
        {
            var go = new GameObject("CodeEffectDevice", typeof(RectTransform), typeof(Canvas));
            go.transform.position = worldPosition;
            // Flush with the back wall, which faces the camera.
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * pixelSize;
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            // Drawn before other see-through things (e.g. a door's "Press E" prompt in front of it), so they stay on top.
            canvas.sortingOrder = -90;
            ((RectTransform)go.transform).sizeDelta = size;

            var panel = go.AddComponent<WorldPanel>();
            panel.AddImage("Bezel", Vector2.zero, size + new Vector2(16f, 16f), Bezel);
            panel.edge = panel.AddImage("Edge", Vector2.zero, size, new Color(accent.r, accent.g, accent.b, 0.55f));
            panel.AddImage("Screen", Vector2.zero, size - new Vector2(6f, 6f), Screen);
            panel.content = NewRect("Content", go.transform);
            panel.content.sizeDelta = size - new Vector2(20f, 20f);
            panel.title = panel.AddText("Title", titleText, font, 16f, accent, new Vector2(0f, size.y * 0.5f - 22f), new Vector2(size.x - 24f, 22f), TextAlignmentOptions.Center);
            return panel;
        }

        /// <summary>A post under the device down to <paramref name="floorY"/> (world), for places with no wall to hang it on.</summary>
        public void AddPost(float floorY)
        {
            var rect = (RectTransform)transform;
            float scale = transform.lossyScale.y;
            float bottom = transform.position.y - (rect.sizeDelta.y * 0.5f + 8f) * scale;
            float length = (bottom - floorY) / scale;
            if (length <= 0f)
                return;
            RectTransform post = NewRect("Post", transform);
            post.SetAsFirstSibling(); // behind the bezel
            post.anchoredPosition = new Vector2(0f, -(rect.sizeDelta.y * 0.5f + 8f) - length * 0.5f + 4f);
            post.sizeDelta = new Vector2(16f, length + 8f);
            var image = post.gameObject.AddComponent<Image>();
            image.color = Bezel;
            image.raycastTarget = false;
        }

        /// <summary>Dims the edge and label (idle) or lights them (active).</summary>
        public void SetLit(bool lit, Color accent)
        {
            if (edge != null)
                edge.color = new Color(accent.r, accent.g, accent.b, lit ? 0.9f : 0.35f);
            if (title != null)
                title.color = lit ? accent : new Color(accent.r, accent.g, accent.b, 0.5f);
        }

        /// <summary>True while an animation started with <see cref="Run"/> is playing.</summary>
        public bool IsRunning => running != null;

        /// <summary>Runs an animation on the device, replacing any that is still running. What it shows at the end stays.</summary>
        public void Run(IEnumerator animation)
        {
            if (running != null)
                StopCoroutine(running);
            running = animation != null ? StartCoroutine(Track(animation)) : null;
        }

        IEnumerator Track(IEnumerator animation)
        {
            yield return animation;
            running = null;
        }

        /// <summary>Shakes the device in place for a moment (unscaled time).</summary>
        public IEnumerator Shake(float strength, float duration)
        {
            Vector3 rest = transform.position;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float fall = 1f - t / duration;
                transform.position = rest + new Vector3(Mathf.Sin(t * 61f), Mathf.Cos(t * 47f), 0f) * strength * fall;
                yield return null;
            }
            transform.position = rest;
        }

        public static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
                yield return null;
        }

        public Image AddImage(string name, Vector2 centre, Vector2 size, Color color)
        {
            RectTransform r = NewRect(name, content != null ? content : transform);
            r.anchoredPosition = centre;
            r.sizeDelta = size;
            var image = r.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public TMP_Text AddText(string name, string value, TMP_FontAsset font, float size, Color color, Vector2 centre, Vector2 dimensions, TextAlignmentOptions alignment)
        {
            RectTransform r = NewRect(name, content != null ? content : transform);
            r.anchoredPosition = centre;
            r.sizeDelta = dimensions;
            var text = r.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null)
                text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            // Overflow, not Truncate: a line slightly taller than its box would otherwise be hidden completely.
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.richText = true;
            text.text = value;
            return text;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            return r;
        }
    }
}
