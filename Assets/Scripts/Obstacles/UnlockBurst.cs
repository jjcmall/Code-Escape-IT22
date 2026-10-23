using UnityEngine;

namespace CodeEscape.Obstacles
{
    /// <summary>
    /// A short burst of light where a code lock opens (green) - created by the obstacle when it unlocks,
    /// then removes itself. Uses unscaled time so it plays as the challenge window closes.
    /// </summary>
    public class UnlockBurst : MonoBehaviour
    {
        const float Duration = 0.9f;

        Light burstLight;
        float peak;
        float started;

        public static void Play(Vector3 position, Color color, float intensity = 8f, float range = 7f)
        {
            var go = new GameObject("UnlockBurst");
            go.transform.position = position;
            var burst = go.AddComponent<UnlockBurst>();
            burst.burstLight = go.AddComponent<Light>();
            burst.burstLight.type = LightType.Point;
            burst.burstLight.color = color;
            burst.burstLight.range = range;
            burst.burstLight.intensity = 0f;
            burst.burstLight.shadows = LightShadows.None;
            burst.peak = intensity;
            burst.started = Time.unscaledTime;
        }

        void Update()
        {
            float t = (Time.unscaledTime - started) / Duration;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            // Quick rise, slow fade.
            float shape = t < 0.15f ? t / 0.15f : 1f - (t - 0.15f) / 0.85f;
            burstLight.intensity = peak * shape * shape;
        }
    }
}
