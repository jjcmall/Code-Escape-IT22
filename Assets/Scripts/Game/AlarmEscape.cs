using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using CodeEscape.Audio;
using CodeEscape.Obstacles;
using CodeEscape.Programming;
using CodeEscape.UI;

namespace CodeEscape.Game
{
    /// <summary>
    /// The alarm escape: when the last required lock of a level opens (from the third level on), SENTINEL sounds
    /// the alarm. The screen edges pulse red, the music turns tense, hazards move faster and a banner says to run
    /// for the exit. There is no timer and nothing is lost: being caught opens the normal, paused challenge.
    /// It ends when the scene changes. Built in code; lives on GameSystems.
    /// </summary>
    public class AlarmEscape : MonoBehaviour
    {
        [SerializeField, Min(0), Tooltip("Level index (0 = first) from which levels end with an alarm escape.")]
        int firstAlarmLevel = 2;
        [SerializeField, Min(1f)] float hazardSpeed = 1.5f;
        [SerializeField] TMP_FontAsset font;
        [SerializeField] Color alarmColor = new Color(0.94f, 0.2f, 0.2f);

        CanvasGroup group;
        Image[] edges;
        TMP_Text banner;
        Coroutine pulse;

        public static bool IsActive { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            IsActive = false;
        }

        void OnEnable()
        {
            ProgrammingChallengeManager.ChallengeCompleted += HandleChallengeCompleted;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        void OnDisable()
        {
            ProgrammingChallengeManager.ChallengeCompleted -= HandleChallengeCompleted;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single)
                Stop();
        }

        void HandleChallengeCompleted(string obstacleId, QuestionData question)
        {
            if (IsActive)
                return;
            LevelManager levels = LevelManager.Instance;
            LevelInfo level = levels != null ? levels.CurrentLevelInfo : null;
            if (level == null || levels.IndexOf(level) < firstAlarmLevel)
                return;
            if (!levels.AreRequiredChallengesComplete(levels.CurrentLevel, out _))
                return;
            StartCoroutine(StartWhenFree(levels.IndexOf(level) == levels.LevelCount - 1));
        }

        // Waits for the solve moments and any cutscene to finish, so the alarm starts in normal play.
        IEnumerator StartWhenFree(bool finalLevel)
        {
            yield return null;
            while (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
                yield return null;
            Begin(finalLevel);
        }

        void Begin(bool finalLevel)
        {
            IsActive = true;
            HazardMotion.SpeedMultiplier = hazardSpeed;
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySfx(SfxType.Alarm);
                AudioManager.Instance.SetSceneMusicOverride(MusicTrack.Challenge);
            }
            string line = finalLevel ? "The core is shutting down. Get to Sam - run!" : "SENTINEL is sealing this place. Run for the exit!";
            if (NotificationUI.Instance != null)
                NotificationUI.Instance.Show("ALARM!", line, alarmColor, 4f);
            Build();
            banner.text = finalLevel ? "ALARM  //  GET TO SAM" : "ALARM  //  REACH THE EXIT";
            group.alpha = 1f;
            pulse = StartCoroutine(Pulse());
            Debug.Log("[Alarm] Alarm escape started");
        }

        void Stop()
        {
            if (!IsActive)
                return;
            IsActive = false;
            HazardMotion.SpeedMultiplier = 1f;
            if (pulse != null)
                StopCoroutine(pulse);
            pulse = null;
            if (group != null)
                group.alpha = 0f;
        }

        // Red edges breathe in and out (unscaled, so they keep going while a challenge window is open).
        IEnumerator Pulse()
        {
            while (true)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
                var color = new Color(alarmColor.r, alarmColor.g, alarmColor.b, Mathf.Lerp(0.12f, 0.42f, k));
                foreach (Image edge in edges)
                    edge.color = color;
                banner.alpha = Mathf.Lerp(0.65f, 1f, k);
                yield return null;
            }
        }

        void Build()
        {
            if (group != null)
                return;
            var canvasObject = new GameObject("AlarmCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -10; // under the HUD and every menu
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            group = canvasObject.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 0f;

            // A red frame of four bars around the screen edges, so the level itself stays readable.
            var bars = new[]
            {
                (new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -70f), Vector2.zero),
                (new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 70f)),
                (new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(70f, 0f)),
                (new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-70f, 0f), Vector2.zero)
            };
            edges = new Image[bars.Length];
            for (int i = 0; i < bars.Length; i++)
            {
                var edge = new GameObject("Edge", typeof(RectTransform), typeof(Image));
                edge.transform.SetParent(canvasObject.transform, false);
                var er = (RectTransform)edge.transform;
                er.anchorMin = bars[i].Item1;
                er.anchorMax = bars[i].Item2;
                er.offsetMin = bars[i].Item3;
                er.offsetMax = bars[i].Item4;
                edges[i] = edge.GetComponent<Image>();
                edges[i].raycastTarget = false;
            }

            // Banner at the top centre, below where notifications appear.
            var bannerObject = new GameObject("Banner", typeof(RectTransform));
            bannerObject.transform.SetParent(canvasObject.transform, false);
            var br = (RectTransform)bannerObject.transform;
            br.anchorMin = br.anchorMax = br.pivot = new Vector2(0.5f, 1f);
            br.anchoredPosition = new Vector2(0f, -178f);
            br.sizeDelta = new Vector2(700f, 44f);
            banner = bannerObject.AddComponent<TextMeshProUGUI>();
            if (font != null)
                banner.font = font;
            banner.fontSize = 30f;
            banner.characterSpacing = 6f;
            banner.alignment = TextAlignmentOptions.Center;
            banner.color = alarmColor;
            banner.raycastTarget = false;
        }
    }
}
