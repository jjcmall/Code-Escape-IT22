using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CodeEscape.UI
{
    /// <summary>
    /// Gives the main menu background a little life without touching any menu logic: a slow drift and
    /// "breathing" zoom of the whole picture, a small shift towards the mouse, twinkling stars, softly
    /// pulsing window and lamp glows, and mist drifting across the street. Everything is purely visual, uses
    /// unscaled time (so it also runs while the game is paused) and is scaled by one Motion Strength value
    /// (0 = completely still). The picture is zoomed in a little so its edges never show.
    /// </summary>
    public class MenuBackgroundMotion : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f), Tooltip("0 turns all movement off; 1 is the designed amount.")]
        float motionStrength = 1f;
        [SerializeField, Range(1.02f, 1.2f), Tooltip("Base zoom. Must stay above the largest drift so no edge shows.")]
        float baseZoom = 1.07f;
        [SerializeField, Range(0f, 1f), Tooltip("When the screen is narrower than the picture: 1 keeps the right side in view, 0 the left.")]
        float focusRight = 1f;

        [Header("Drift")]
        [SerializeField, Min(0f), Tooltip("Sideways drift, as a fraction of the picture width.")]
        float driftX = 0.012f;
        [SerializeField, Min(0f), Tooltip("Up and down drift, as a fraction of the picture height.")]
        float driftY = 0.007f;
        [SerializeField, Min(1f), Tooltip("Seconds for one full drift cycle.")]
        float driftPeriod = 38f;
        [SerializeField, Min(0f), Tooltip("Zoom in and out by this much around the base zoom.")]
        float breathe = 0.008f;
        [SerializeField, Min(0f), Tooltip("Shift towards the mouse, as a fraction of the picture size.")]
        float mouseShift = 0.006f;

        [Header("Details")]
        [SerializeField, Tooltip("Small glow dots over the stars. They fade in and out at different speeds.")]
        Graphic[] stars = new Graphic[0];
        [SerializeField, Tooltip("Window and lamp glows. They pulse slowly.")]
        Graphic[] glows = new Graphic[0];
        [SerializeField, Tooltip("Wide soft shapes that drift sideways like mist.")]
        RectTransform[] mist = new RectTransform[0];
        [SerializeField, Min(0f), Tooltip("How far the mist travels, as a fraction of the picture width.")]
        float mistTravel = 0.18f;

        RectTransform rect;
        RectTransform parentRect;
        Vector2 mouse;
        float[] starBase;
        float[] glowBase;
        float[] mistBase;
        float[] mistStart;

        void Awake()
        {
            rect = (RectTransform)transform;
            parentRect = rect.parent as RectTransform;
            starBase = RememberAlpha(stars);
            glowBase = RememberAlpha(glows);
            mistBase = new float[mist.Length];
            mistStart = new float[mist.Length];
            for (int i = 0; i < mist.Length; i++)
            {
                mistStart[i] = mist[i] != null ? mist[i].anchoredPosition.x : 0f;
                Graphic g = mist[i] != null ? mist[i].GetComponent<Graphic>() : null;
                mistBase[i] = g != null ? g.color.a : 0f;
            }
        }

        void OnDisable()
        {
            Apply(0f, true);
        }

        void LateUpdate()
        {
            Apply(Time.unscaledTime, false);
        }

        void Apply(float t, bool reset)
        {
            float k = reset ? 0f : motionStrength;

            // Whole picture: zoom, drift and a small shift towards the mouse.
            Vector2 target = Vector2.zero;
            Mouse pointer = Mouse.current;
            if (pointer != null && Screen.width > 0 && Screen.height > 0)
            {
                Vector2 p = pointer.position.ReadValue();
                target = new Vector2(Mathf.Clamp(p.x / Screen.width * 2f - 1f, -1f, 1f), Mathf.Clamp(p.y / Screen.height * 2f - 1f, -1f, 1f));
            }
            mouse = Vector2.Lerp(mouse, target, 1f - Mathf.Exp(-3f * Time.unscaledDeltaTime));

            float w = rect.rect.width;
            float h = rect.rect.height;
            float angle = t / driftPeriod * Mathf.PI * 2f;
            float x = (Mathf.Sin(angle) * driftX + mouse.x * -mouseShift) * w * k;
            float y = (Mathf.Sin(angle * 0.63f + 1.3f) * driftY + mouse.y * -mouseShift) * h * k;

            // Screens narrower than the picture show the side that matters (the vista on the right).
            float overflow = parentRect != null ? Mathf.Max(0f, w - parentRect.rect.width) : 0f;
            float crop = -overflow * 0.5f * (focusRight * 2f - 1f);

            float zoom = baseZoom + Mathf.Sin(angle * 1.7f) * breathe * k;
            rect.localScale = new Vector3(zoom, zoom, 1f);
            rect.anchoredPosition = new Vector2(crop + x, y);

            for (int i = 0; i < stars.Length; i++)
            {
                if (stars[i] == null)
                    continue;
                float speed = 0.7f + (i * 0.37f % 1.1f);
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * speed + i * 2.1f);
                SetAlpha(stars[i], starBase[i] * Mathf.Lerp(1f, 0.15f + pulse, k));
            }

            for (int i = 0; i < glows.Length; i++)
            {
                if (glows[i] == null)
                    continue;
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * (0.55f + i * 0.11f) + i * 1.7f);
                SetAlpha(glows[i], glowBase[i] * Mathf.Lerp(1f, 0.55f + 0.55f * pulse, k));
            }

            for (int i = 0; i < mist.Length; i++)
            {
                if (mist[i] == null)
                    continue;
                float phase = t / (driftPeriod * (0.8f + 0.3f * i)) * Mathf.PI * 2f + i * 2.4f;
                Vector2 pos = mist[i].anchoredPosition;
                pos.x = mistStart[i] + Mathf.Sin(phase) * mistTravel * w * k;
                mist[i].anchoredPosition = pos;
                Graphic g = mist[i].GetComponent<Graphic>();
                if (g != null)
                    SetAlpha(g, mistBase[i] * Mathf.Lerp(1f, 0.6f + 0.4f * Mathf.Sin(phase * 0.7f), k));
            }
        }

        static float[] RememberAlpha(Graphic[] graphics)
        {
            var alphas = new float[graphics.Length];
            for (int i = 0; i < graphics.Length; i++)
                alphas[i] = graphics[i] != null ? graphics[i].color.a : 0f;
            return alphas;
        }

        static void SetAlpha(Graphic graphic, float alpha)
        {
            Color c = graphic.color;
            c.a = Mathf.Clamp01(alpha);
            graphic.color = c;
        }
    }
}
