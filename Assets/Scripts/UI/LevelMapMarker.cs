using UnityEngine;

namespace CodeEscape.UI
{
    /// <summary>
    /// A small "you are here" marker for the level map: it sits on the route below the level to play next,
    /// bobs gently, and slides along the route when the next level has changed since the map was last shown
    /// (a level was just cleared). Purely visual: it only reads the next level from LevelMapUI and never
    /// changes progress. Uses unscaled time.
    /// </summary>
    public class LevelMapMarker : MonoBehaviour
    {
        // The next level the marker stood at the last time the map was open (survives scene changes).
        static int lastShownIndex = -1;

        [SerializeField] LevelMapUI map;
        [SerializeField, Tooltip("The Levels object: its children are the level cards, in order.")]
        RectTransform levelsRoot;
        [SerializeField, Tooltip("Height of the marker on the route, in this object's parent space.")]
        float routeY = -300f;
        [SerializeField, Min(0.1f)] float slideSeconds = 1.3f;
        [SerializeField] float bobHeight = 5f;

        RectTransform rect;
        RectTransform parentRect;
        int fromIndex;
        int toIndex;
        float slideStart = -10f;
        int target = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            lastShownIndex = -1;
        }

        void Awake()
        {
            rect = (RectTransform)transform;
            parentRect = rect.parent as RectTransform;
        }

        void LateUpdate()
        {
            if (map == null || levelsRoot == null || parentRect == null)
                return;
            int next = map.NextLevelIndex;
            if (next < 0 || next >= levelsRoot.childCount)
                return;

            if (target < 0)
            {
                // First frame with a ready map: start where the marker was last time (or at the target).
                int start = lastShownIndex >= 0 && lastShownIndex < levelsRoot.childCount ? lastShownIndex : next;
                fromIndex = toIndex = start;
                target = start;
            }
            if (next != target)
            {
                fromIndex = target;
                toIndex = next;
                slideStart = Time.unscaledTime;
                target = next;
                lastShownIndex = next;
            }
            else if (lastShownIndex < 0)
            {
                lastShownIndex = next;
            }

            float t = Mathf.Clamp01((Time.unscaledTime - slideStart) / slideSeconds);
            float eased = t * t * (3f - 2f * t);
            // Positions are read from the cards every frame, so a resize or aspect change keeps the marker in place.
            float x = Mathf.Lerp(XOf(fromIndex), XOf(toIndex), eased);
            float bob = Mathf.Sin(Time.unscaledTime * 3f) * bobHeight;
            rect.anchoredPosition = new Vector2(x, routeY + bob);
        }

        // Horizontal position of a card's centre in the parent's space.
        float XOf(int index)
        {
            Vector3 world = levelsRoot.GetChild(index).position;
            Vector3 local = parentRect.InverseTransformPoint(world);
            return local.x;
        }
    }
}
