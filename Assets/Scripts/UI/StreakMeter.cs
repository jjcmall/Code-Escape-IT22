using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using CodeEscape.Game;
using CodeEscape.Programming;

namespace CodeEscape.UI
{
    /// <summary>
    /// A small "FIRST-TRY STREAK x3" badge in the levels (bottom left, above the controls bar), shown while two or
    /// more challenges in a row were solved with the first CHECK. It pops when the streak grows and fades away
    /// quietly when it ends. Just for excitement: it gives no points. Built in code; lives on GameSystems.
    /// </summary>
    public class StreakMeter : MonoBehaviour
    {
        const int ShowFrom = 2;

        [SerializeField] TMP_FontAsset font;
        [SerializeField] Color accent = new Color(0.98f, 0.62f, 0.15f);

        CanvasGroup group;
        RectTransform panel;
        TMP_Text label;
        Coroutine routine;
        int shown;

        void OnEnable()
        {
            ProgrammingChallengeManager.StreakChanged += HandleStreak;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        void OnDisable()
        {
            ProgrammingChallengeManager.StreakChanged -= HandleStreak;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        void Start()
        {
            Build();
            ProgrammingChallengeManager manager = ProgrammingChallengeManager.Instance;
            Show(manager != null ? manager.Streak : 0, false);
        }

        void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ProgrammingChallengeManager manager = ProgrammingChallengeManager.Instance;
            Show(manager != null ? manager.Streak : 0, false);
        }

        void HandleStreak(int streak)
        {
            Show(streak, true);
        }

        void Show(int streak, bool animate)
        {
            if (group == null)
                return;
            bool visible = streak >= ShowFrom && InLevel();
            if (routine != null)
                StopCoroutine(routine);
            routine = null;
            if (visible)
            {
                label.text = $"FIRST-TRY STREAK  <size=130%>x{streak}</size>";
                if (animate && streak > shown)
                    routine = StartCoroutine(Pop());
                else
                    group.alpha = 1f;
            }
            else if (animate && group.alpha > 0f)
                routine = StartCoroutine(FadeOut());
            else
                group.alpha = 0f;
            shown = visible ? streak : 0;
        }

        static bool InLevel()
        {
            LevelManager levels = LevelManager.Instance;
            return levels != null && levels.GetInfo(SceneManager.GetActiveScene().name) != null;
        }

        IEnumerator Pop()
        {
            group.alpha = 1f;
            for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.35f;
                panel.localScale = Vector3.one * (1f + 0.35f * Mathf.Sin(k * Mathf.PI) * (1f - k));
                yield return null;
            }
            panel.localScale = Vector3.one;
            routine = null;
        }

        IEnumerator FadeOut()
        {
            float start = group.alpha;
            for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(start, 0f, t / 0.6f);
                yield return null;
            }
            group.alpha = 0f;
            routine = null;
        }

        void Build()
        {
            if (group != null)
                return;
            var canvasObject = new GameObject("StreakCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30; // under notifications, the pause menu and challenges
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            group = canvasObject.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 0f;

            // Bottom left, above the "Signal to Sam" meter and the controls bar.
            panel = NewRect("Panel", canvasObject.transform);
            panel.anchorMin = panel.anchorMax = Vector2.zero;
            panel.pivot = new Vector2(0f, 0f);
            panel.anchoredPosition = new Vector2(24f, 146f);
            panel.sizeDelta = new Vector2(410f, 58f);
            AddImage(panel, new Color(accent.r, accent.g, accent.b, 0.9f));
            RectTransform inner = NewRect("Inner", panel);
            inner.anchorMin = Vector2.zero;
            inner.anchorMax = Vector2.one;
            inner.offsetMin = new Vector2(2f, 2f);
            inner.offsetMax = new Vector2(-2f, -2f);
            AddImage(inner, new Color(0.06f, 0.05f, 0.04f, 0.95f));

            RectTransform diamond = NewRect("Diamond", panel);
            diamond.anchorMin = diamond.anchorMax = new Vector2(0f, 0.5f);
            diamond.anchoredPosition = new Vector2(30f, 0f);
            diamond.sizeDelta = new Vector2(20f, 20f);
            diamond.localRotation = Quaternion.Euler(0f, 0f, 45f);
            AddImage(diamond, accent);

            RectTransform text = NewRect("Label", panel);
            text.anchorMin = Vector2.zero;
            text.anchorMax = Vector2.one;
            text.offsetMin = new Vector2(54f, 0f);
            text.offsetMax = new Vector2(-12f, 0f);
            label = text.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null)
                label.font = font;
            label.fontSize = 22f;
            label.color = accent;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void AddImage(RectTransform target, Color color)
        {
            var image = target.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }
    }
}
