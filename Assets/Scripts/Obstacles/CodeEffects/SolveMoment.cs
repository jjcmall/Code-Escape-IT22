using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using CodeEscape.Cutscenes;
using CodeEscape.Game;
using CodeEscape.Player;

namespace CodeEscape.Obstacles
{
    /// <summary>
    /// Short "look at that" moments after a challenge window closes: the game freezes like a cutscene, the camera
    /// moves to what the player's code changed (a door's device, the SENTINEL core), the change plays, and the
    /// camera returns to Alex. Moments queue up and play one after another. Enter, Space, E or Esc skips the
    /// waiting. A real cutscene is never interrupted (cutscenes only start while Playing); when the game can't
    /// be directed, the changes still happen, just without the camera.
    /// </summary>
    public class SolveMoment : MonoBehaviour
    {
        /// <summary>One moment: where to look, what to start there, how long it is busy, what to do at the end.</summary>
        public sealed class Moment
        {
            public Vector3 Point;
            public float SizeFactor = 0.8f;
            public Action Begin;
            public Func<bool> Busy;
            public Action End;
            public float Hold = 0.9f;
        }

        static SolveMoment host;
        readonly Queue<Moment> queue = new Queue<Moment>();
        bool running;

        /// <summary>True when the player pressed a key to skip the current moment's waiting.</summary>
        public static bool SkipRequested { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            host = null;
            SkipRequested = false;
        }

        /// <summary>Plays <paramref name="moment"/> after any queued ones.</summary>
        public static void Enqueue(Moment moment)
        {
            if (moment == null)
                return;
            if (host == null)
                host = new GameObject("SolveMoments").AddComponent<SolveMoment>(); // lives in the level scene
            host.queue.Enqueue(moment);
            if (!host.running)
                host.StartCoroutine(host.RunQueue());
        }

        /// <summary>Waits on unscaled time, ending early when the player skips.</summary>
        public static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds && !SkipRequested; t += Time.unscaledDeltaTime)
                yield return null;
        }

        IEnumerator RunQueue()
        {
            running = true;
            // Wait a frame, so moments queued by the same event (a door's device, then the core) play together, in order.
            yield return null;
            if (!CanDirect())
            {
                // No camera work now: just make the changes.
                while (queue.Count > 0)
                {
                    Moment moment = queue.Dequeue();
                    moment.Begin?.Invoke();
                    moment.End?.Invoke();
                }
                running = false;
                yield break;
            }

            GameManager.Instance.SetState(GameState.Cutscene);
            Time.timeScale = 0f;
            CameraController cam = CameraController.Current;
            while (queue.Count > 0)
            {
                Moment moment = queue.Dequeue();
                SkipRequested = false;
                Coroutine watch = StartCoroutine(WatchSkip());
                yield return cam.FocusPoint(moment.Point, 0.5f, moment.SizeFactor);
                moment.Begin?.Invoke();
                while (moment.Busy != null && moment.Busy() && !SkipRequested)
                    yield return null;
                moment.End?.Invoke();
                yield return Wait(moment.Hold);
                StopCoroutine(watch);
            }
            SkipRequested = false;

            yield return cam.ResetView(0.45f);
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Cutscene && !CutsceneManager.IsActive)
                GameManager.Instance.SetState(GameState.Playing);
            running = false;
        }

        static bool CanDirect()
        {
            return CameraController.Current != null && GameManager.Instance != null
                && GameManager.Instance.CurrentState == GameState.Playing && !CutsceneManager.IsActive;
        }

        IEnumerator WatchSkip()
        {
            // Ignore the key that closed the challenge window.
            yield return null;
            while (!SkipRequested)
            {
                Keyboard keyboard = Keyboard.current;
                if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame
                    || keyboard.eKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
                    SkipRequested = true;
                yield return null;
            }
        }

        void OnDestroy()
        {
            if (host == this)
                host = null;
        }
    }
}
