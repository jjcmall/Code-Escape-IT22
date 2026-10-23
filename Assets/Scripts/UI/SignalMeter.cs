using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using CodeEscape.Game;
using CodeEscape.Programming;
using CodeEscape.Rewards;

namespace CodeEscape.UI
{
    /// <summary>
    /// "SIGNAL TO SAM": six signal bars, one per level, filled for each level cleared, so the player always sees
    /// how close Alex is to finding Sam. Shown in the levels (bottom left, above the controls bar) and on the
    /// level map (beside the levels-cleared line). Pulses when a level is cleared. Built in code; lives on GameSystems.
    /// </summary>
    public class SignalMeter : MonoBehaviour
    {
        [SerializeField] TMP_FontAsset font;
        [SerializeField] Color onColor = new Color(0.29f, 0.87f, 0.5f);
        [SerializeField] Color offColor = new Color(0.25f, 0.3f, 0.36f);

        CanvasGroup group;
        RectTransform panel;
        TMP_Text label;
        Image[] bars = new Image[0];
        Coroutine pop;

        void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            LevelProgressManager.LevelCleared += HandleLevelCleared;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            LevelProgressManager.LevelCleared -= HandleLevelCleared;
        }

        void Start()
        {
            Refresh(false);
        }

        void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single)
                Refresh(false);
        }

        void HandleLevelCleared(ProgrammingLanguage language, LevelInfo cleared, LevelInfo next)
        {
            Refresh(true);
        }

        void Refresh(bool animate)
        {
            LevelManager levels = LevelManager.Instance;
            int total = levels != null ? Mathf.Max(1, levels.LevelCount) : 6;
            Build(total);
            string scene = SceneManager.GetActiveScene().name;
            bool onMap = scene == SceneNames.LevelMap;
            bool inLevel = levels != null && levels.GetInfo(scene) != null;
            group.alpha = onMap || inLevel ? 1f : 0f;
            if (group.alpha == 0f)
                return;

            // Level map: beside "n / 6 LEVELS CLEARED" (top left), above the glow of the selected card. Levels:
            // bottom left above the controls bar, clear of the HUD panels and the signs on the walls.
            panel.anchorMin = panel.anchorMax = panel.pivot = onMap ? new Vector2(0f, 1f) : new Vector2(0f, 0f);
            panel.anchoredPosition = onMap ? new Vector2(380f, -161f) : new Vector2(24f, 92f);

            int cleared = Mathf.Clamp(AchievementCatalog.ClearedLevels(), 0, total);
            for (int i = 0; i < bars.Length; i++)
                bars[i].color = i < cleared ? onColor : offColor;
            label.text = cleared >= total ? "SAM FOUND" : $"SIGNAL TO SAM  {cleared}/{total}";
            label.color = cleared > 0 ? onColor : new Color(0.7f, 0.76f, 0.83f);
            if (animate)
            {
                if (pop != null)
                    StopCoroutine(pop);
                pop = StartCoroutine(Pop());
            }
        }

        IEnumerator Pop()
        {
            for (float t = 0f; t < 0.45f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.45f;
                panel.localScale = Vector3.one * (1f + 0.3f * Mathf.Sin(k * Mathf.PI) * (1f - k));
                yield return null;
            }
            panel.localScale = Vector3.one;
            pop = null;
        }

        void Build(int total)
        {
            if (group != null && bars.Length == total)
                return;
            if (group == null)
            {
                var canvasObject = new GameObject("SignalCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
                canvasObject.transform.SetParent(transform, false);
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 5; // above the HUD and level map, under notifications and challenges
                var scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                group = canvasObject.GetComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;

                panel = NewRect("Panel", canvasObject.transform);
                panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0f, 1f);
                panel.sizeDelta = new Vector2(330f, 44f);
                var background = panel.gameObject.AddComponent<Image>();
                background.color = new Color(0.04f, 0.09f, 0.13f, 0.88f);
                background.raycastTarget = false;

                RectTransform text = NewRect("Label", panel);
                text.anchorMin = new Vector2(0f, 0f);
                text.anchorMax = new Vector2(0f, 1f);
                text.pivot = new Vector2(0f, 0.5f);
                text.anchoredPosition = new Vector2(14f, 0f);
                text.sizeDelta = new Vector2(210f, 0f);
                label = text.gameObject.AddComponent<TextMeshProUGUI>();
                if (font != null)
                    label.font = font;
                label.fontSize = 17f;
                label.characterSpacing = 2f;
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.raycastTarget = false;
            }

            foreach (Image bar in bars)
                if (bar != null)
                    Destroy(bar.gameObject);
            // Phone-style bars of rising height, right-aligned in the panel.
            bars = new Image[total];
            for (int i = 0; i < total; i++)
            {
                RectTransform r = NewRect("Bar" + i, panel);
                r.anchorMin = r.anchorMax = new Vector2(1f, 0f);
                r.pivot = new Vector2(1f, 0f);
                float height = 8f + 22f * (i + 1) / total;
                r.sizeDelta = new Vector2(8f, height);
                r.anchoredPosition = new Vector2(-14f - (total - 1 - i) * 12f, 8f);
                bars[i] = r.gameObject.AddComponent<Image>();
                bars[i].raycastTarget = false;
            }
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
    }
}
