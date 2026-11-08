using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using CodeEscape.Audio;
using CodeEscape.Game;
using CodeEscape.Programming;
using CodeEscape.Story;

namespace CodeEscape.Cutscenes
{
    /// <summary>
    /// The prologue ("The Empty House") as an animated 2D motion comic in the Intro scene: three comic pages whose
    /// panels appear in reading order while the view glides from panel to panel, with captions, speech bubbles, sound
    /// words and moving layers (drifting clouds, a glowing sun, dust, a flickering screen with code typing itself).
    /// It ends with the player's first line of code: the old family computer waits for a command, the player types
    /// print("Find Sam") in the normal challenge window (no points), and SENTINEL takes over the screen.
    /// Click / Space / Enter / E / NEXT continues; Esc / SKIP skips the story (it stops at the computer), and skipping
    /// there skips the typing too. Everything is built in code from flat 2D layers. If something it needs is missing, the old
    /// cutscene (<see cref="fallback"/>) plays instead.
    /// </summary>
    public class PrologueComic : MonoBehaviour, IChallengeSource
    {
        const string CutsceneId = "intro";
        const float GlideSeconds = 0.75f;

        [Header("Fonts")]
        [SerializeField] TMP_FontAsset titleFont;
        [SerializeField] TMP_FontAsset codeFont;
        [Header("Art")]
        [SerializeField] Sprite houseArt;
        [SerializeField] Sprite walkHomeArt;
        [SerializeField] Sprite roomArt;
        [SerializeField] Sprite computerArt;
        [SerializeField] Sprite alexPortrait;
        [SerializeField] Sprite samPortrait;
        [SerializeField] Sprite sentinelScreen;
        [Header("First line of code")]
        [SerializeField, Tooltip("The Run Code question typed at the computer (print(\"Find Sam\")).")]
        QuestionData firstCommand;
        [SerializeField, Tooltip("The old slideshow cutscene, played instead if the comic cannot run.")]
        CutsceneTrigger fallback;

        static readonly Color PageColor = new Color(0.06f, 0.08f, 0.12f);
        static readonly Color InkColor = new Color(0.07f, 0.07f, 0.09f);
        static readonly Color CaptionColor = new Color(0.99f, 0.84f, 0.29f);
        static readonly Color Cyan = new Color(0.13f, 0.83f, 0.93f);
        static readonly Color ScreenGreen = new Color(0.29f, 0.87f, 0.5f);
        static readonly Color SentinelPurple = new Color(0.65f, 0.55f, 0.98f);

        RectTransform stage;
        CanvasGroup fade;
        TMP_Text hint;
        readonly List<Action<float>> ambient = new List<Action<float>>();
        GameObject buttons;
        bool fastForward;
        bool solved;
        bool advanceRequested;
        bool skipRequested;
        string language = "Python";

        // ---------- IChallengeSource (the family computer) ----------

        public string ObstacleId => "prologue_computer";
        public string DisplayName => "Family computer";
        public bool IsMandatory => false;
        public bool CountsAsObstacle => false; // the prologue gives no points
        public bool HasClueReward => false;
        public string ClueRewardId => null;
        public string ClueRewardText => null;

        public QuestionData ResolveQuestion(ProgrammingLanguage lang, QuestionDatabase database, ICollection<string> completedQuestionIds)
        {
            return firstCommand != null && firstCommand.Language == lang ? firstCommand : null;
        }

        public void OnChallengeSolved()
        {
            solved = true;
        }

        // ---------- Flow ----------

        void Start()
        {
            if (houseArt == null || roomArt == null || computerArt == null || alexPortrait == null)
            {
                Debug.LogWarning("[Prologue] Comic art is missing - playing the old cutscene instead.");
                if (fallback != null)
                    fallback.Trigger();
                return;
            }
            if (LanguageManager.Current.IsValid())
                language = LanguageManager.Current.ToDisplayName();
            Build();
            StartCoroutine(Play());
        }

        void Update()
        {
            float t = Time.unscaledTime;
            foreach (Action<float> step in ambient)
                step(t);
            if (hint != null)
                hint.alpha = 0.55f + 0.35f * Mathf.Sin(t * 3f);
        }

        IEnumerator Play()
        {
            Music(MusicTrack.Exploration);
            yield return FadeFromBlack(0.8f);

            // ----- Page 1: coming home
            Page page1 = NewPage("PROLOGUE", "THE EMPTY HOUSE");
            Panel a = page1.AddPanel(60, 120, 1800, 460);
            Panel b = page1.AddPanel(60, 606, 1000, 414);
            Panel c = page1.AddPanel(1088, 606, 772, 414);
            HouseShot(a);
            BusShot(b);
            Caption(a, "TUESDAY, 4:10 PM.", 24, 24);
            Caption(b, "The school bus rumbles away down Maple Street.", 24, 24, 560);
            Portrait(c, alexPortrait, new Color(0.98f, 0.55f, 0.2f), false);
            Bubble alexHome = Speech(c, "I'm home! Sam, if you ate the last of the crackers again, I'm telling Mom!", 330, 30, 420, true);

            yield return ShowPage(page1, null);
            yield return TitleCard(page1);
            yield return Reveal(a);
            yield return Beat();
            yield return Reveal(b);
            Sfx(SfxType.Cutscene);
            yield return Beat();
            yield return Reveal(c);
            yield return alexHome.Show(this);
            yield return Beat();
            yield return Overview();
            yield return Beat(1.2f);

            // ----- Page 2: nobody home
            Page page2 = NewPage("PROLOGUE", "NO ANSWER");
            Panel d = page2.AddPanel(60, 120, 1800, 460);
            Panel e = page2.AddPanel(60, 606, 772, 414);
            Panel f = page2.AddPanel(860, 606, 1000, 414);
            RoomShot(d);
            Caption(d, "No answer. No footsteps on the stairs. No cartoon noise from the TV.", 24, 24, 760);
            Portrait(e, alexPortrait, new Color(0.25f, 0.45f, 0.8f), false);
            Bubble worried = Speech(e, "Sam? ...Bag's gone. Shoes are gone. You were supposed to be home an hour ago.", 300, 26, 420, true);
            PhotoShot(f);
            Bubble detective = Speech(f, "Okay. Don't panic. Mom and Dad are back at six. Think like a detective.", 520, 40, 440, false);

            yield return ShowPage(page2, page1);
            yield return Reveal(d);
            yield return Beat();
            yield return Reveal(e);
            yield return worried.Show(this);
            yield return Beat();
            yield return Reveal(f);
            yield return detective.Show(this);
            yield return Beat();
            yield return Overview();
            yield return Beat(1.2f);

            // ----- Page 3: the computer
            Music(MusicTrack.Mystery);
            Page page3 = NewPage("PROLOGUE", "THE COMPUTER");
            Panel g = page3.AddPanel(60, 120, 1110, 460);
            Panel h = page3.AddPanel(1198, 120, 662, 460);
            Panel i = page3.AddPanel(60, 606, 1800, 414);
            ComputerShot(g);
            Caption(g, "Through the little window in the hallway door, Alex can see the old family computer. It's on, and lines of code are scrolling across the screen.", 24, 24, 640);
            Portrait(h, alexPortrait, new Color(0.15f, 0.55f, 0.35f), true);
            Bubble running = Speech(h, $"That's {language}. Somebody left this running on purpose.", 24, 290, 614, false);
            Terminal terminal = TerminalShot(i);

            yield return ShowPage(page3, page2);
            yield return Reveal(g);
            yield return Beat();
            yield return Reveal(h);
            yield return running.Show(this);
            yield return Beat();

            // The first line of code. Skipping the story stops here.
            fastForward = false;
            yield return Reveal(i);
            yield return terminal.Type(this, "CODE QUEST  v1.0\nWaiting for a command...\n> ");
            yield return FirstCommand(terminal);

            // SENTINEL wakes up.
            yield return SentinelTakeover(i, terminal);
            Bubble sentinel = SentinelBubble(i, "VISITOR DETECTED. CHECKPOINT 1 IS LOCKED. SOLVE THE CODE TO CONTINUE.");
            yield return sentinel.Show(this);
            yield return Beat();
            Bubble keypad = Speech(i, "Checkpoint? Is this some kind of game? ...And since when does our hallway door have a keypad?", 1250, 160, 500, false);
            yield return keypad.Show(this);
            yield return Beat();

            yield return EndCard();
            Finish();
        }

        // The family computer asks for its first command: the normal challenge window opens on it.
        IEnumerator FirstCommand(Terminal terminal)
        {
            ProgrammingChallengeManager manager = ProgrammingChallengeManager.Instance;
            bool canType = manager != null && firstCommand != null && firstCommand.Language == LanguageManager.Current;
            if (!canType)
            {
                terminal.Append("print(\"Find Sam\")\nFind Sam\n");
                yield return Beat(1.5f);
                yield break;
            }

            SetHint("Click or press E to type your first command    ·    Esc to skip");
            while (!solved)
            {
                if (SkipPressed())
                    break;
                if (AdvancePressed() || !terminal.Prompted)
                {
                    terminal.Prompted = true;
                    GameManager game = GameManager.Instance;
                    GameState before = game != null ? game.CurrentState : GameState.Intro;
                    // Challenges only open during play; the prologue switches to it for this one window.
                    if (game != null)
                        game.SetState(GameState.Playing);
                    if (!manager.TryOpenChallenge(this))
                    {
                        if (game != null)
                            game.SetState(before);
                        terminal.Append("print(\"Find Sam\")\nFind Sam\n");
                        break;
                    }
                    while (manager.IsChallengeOpen)
                        yield return null;
                    if (game != null)
                        game.SetState(before);
                    if (!solved)
                        SetHint("The computer is still waiting. Click or press E to try again    ·    Esc to skip");
                }
                yield return null;
            }
            if (solved)
                terminal.Append("print(\"Find Sam\")\n<color=#FFFFFF>Find Sam</color>\n");
            SetHint("Click or press SPACE to continue    ·    Esc to skip");
            yield return Beat(0.8f);
        }

        void Finish()
        {
            StopAllCoroutines();
            if (StoryManager.Instance != null)
                StoryManager.Instance.CompleteEvent(CutsceneManager.PlayedEventPrefix + CutsceneId);
            Debug.Log("[Prologue] Finished the motion comic");
            SceneTransitionManager.Load(SceneNames.Home);
        }

        // ---------- Beats, input and the "camera" ----------

        /// <summary>NEXT: the same as a click, Space or Enter.</summary>
        public void Advance()
        {
            advanceRequested = true;
        }

        /// <summary>SKIP: the same as Esc.</summary>
        public void Skip()
        {
            skipRequested = true;
        }

        static bool ChallengeOpen()
        {
            ProgrammingChallengeManager manager = ProgrammingChallengeManager.Instance;
            return manager != null && manager.IsChallengeOpen;
        }

        bool AdvancePressed()
        {
            bool requested = advanceRequested;
            advanceRequested = false;
            if (ChallengeOpen())
                return false;
            Keyboard k = Keyboard.current;
            Mouse m = Mouse.current;
            Gamepad g = Gamepad.current;
            return requested
                || (k != null && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame || k.eKey.wasPressedThisFrame))
                || (m != null && m.leftButton.wasPressedThisFrame)
                || (g != null && g.buttonSouth.wasPressedThisFrame);
        }

        bool SkipPressed()
        {
            bool requested = skipRequested;
            skipRequested = false;
            if (ChallengeOpen())
                return false;
            Keyboard k = Keyboard.current;
            Gamepad g = Gamepad.current;
            return requested || (k != null && k.escapeKey.wasPressedThisFrame) || (g != null && g.startButton.wasPressedThisFrame);
        }

        // Waits for the player (or `auto` seconds). Esc fast-forwards the story up to the computer.
        IEnumerator Beat(float auto = 0f)
        {
            float start = Time.unscaledTime;
            yield return null;
            while (!fastForward)
            {
                if (SkipPressed())
                {
                    fastForward = true;
                    break;
                }
                if (Time.unscaledTime - start > 0.15f && AdvancePressed())
                    break;
                if (auto > 0f && Time.unscaledTime - start >= auto)
                    break;
                yield return null;
            }
        }

        // Glides the view so the panel fills most of the screen.
        IEnumerator Reveal(Panel panel)
        {
            panel.Root.gameObject.SetActive(true);
            Rect r = panel.Area;
            float k = Mathf.Clamp(Mathf.Min(1920f * 0.9f / r.width, 1080f * 0.84f / r.height), 1f, 1.9f);
            var target = new Vector2(-(r.center.x - 960f) * k, (r.center.y - 540f) * k);
            Sfx(SfxType.Button);
            yield return Animate(fastForward ? 0f : GlideSeconds, p =>
            {
                panel.Group.alpha = p;
                panel.Root.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, p);
            }, stage, Vector3.one * k, target);
        }

        IEnumerator Overview()
        {
            // A little smaller and higher than the screen, so the NEXT and SKIP buttons never cover the bottom panels.
            yield return Animate(fastForward ? 0f : GlideSeconds, null, stage, Vector3.one * 0.93f, new Vector2(0f, 20f));
        }

        IEnumerator Animate(float seconds, Action<float> each, RectTransform moving, Vector3 toScale, Vector2 toPosition)
        {
            Vector3 fromScale = moving.localScale;
            Vector2 fromPosition = moving.anchoredPosition;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float p = Mathf.SmoothStep(0f, 1f, t / seconds);
                moving.localScale = Vector3.LerpUnclamped(fromScale, toScale, p);
                moving.anchoredPosition = Vector2.LerpUnclamped(fromPosition, toPosition, p);
                each?.Invoke(p);
                yield return null;
            }
            moving.localScale = toScale;
            moving.anchoredPosition = toPosition;
            each?.Invoke(1f);
        }

        // Turns the page: the old one slides away to the left, the new one comes in from the right.
        IEnumerator ShowPage(Page next, Page previous)
        {
            yield return Overview();
            next.Root.gameObject.SetActive(true);
            Sfx(SfxType.Cutscene);
            float seconds = fastForward ? 0f : 0.7f;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float p = Mathf.SmoothStep(0f, 1f, t / seconds);
                next.Root.anchoredPosition = new Vector2(Mathf.Lerp(2000f, 0f, p), 0f);
                next.Root.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-3f, 0f, p));
                if (previous != null)
                {
                    previous.Root.anchoredPosition = new Vector2(Mathf.Lerp(0f, -2000f, p), 0f);
                    previous.Root.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 3f, p));
                }
                yield return null;
            }
            next.Root.anchoredPosition = Vector2.zero;
            next.Root.localRotation = Quaternion.identity;
            if (previous != null)
                previous.Root.gameObject.SetActive(false);
        }

        IEnumerator TitleCard(Page page)
        {
            page.Title.gameObject.SetActive(true);
            yield return Animate(fastForward ? 0f : 0.6f, p =>
            {
                page.Title.alpha = p;
                page.Title.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.3f, 1f, p);
            }, stage, Vector3.one, Vector2.zero);
            yield return Beat(2.2f);
            yield return Animate(fastForward ? 0f : 0.35f, p => page.Title.alpha = 1f - p, stage, Vector3.one, Vector2.zero);
            page.Title.gameObject.SetActive(false);
        }

        IEnumerator FadeFromBlack(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                fade.alpha = 1f - t / seconds;
                yield return null;
            }
            fade.alpha = 0f;
        }

        IEnumerator EndCard()
        {
            SetHint(string.Empty);
            if (buttons != null)
                buttons.SetActive(false);
            TMP_Text card = NewText("EndCard", fade.transform, titleFont, 64, Color.white, TextAlignmentOptions.Center);
            Stretch(card.rectTransform);
            card.text = $"<size=50%><color=#{ColorUtility.ToHtmlStringRGB(Cyan)}>LEVEL 1</color></size>\nHOME\n<size=45%><color=#9DB0C6>Find Sam.</color></size>";
            for (float t = 0f; t < 0.8f; t += Time.unscaledDeltaTime)
            {
                fade.alpha = t / 0.8f;
                yield return null;
            }
            fade.alpha = 1f;
            yield return new WaitForSecondsRealtime(1.6f);
        }

        void SetHint(string text)
        {
            if (hint != null)
                hint.text = text;
        }

        static void Music(MusicTrack track)
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayMusic(track);
        }

        static void Sfx(SfxType type)
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySfx(type);
        }

        // ---------- Shots (the moving art inside each panel) ----------

        void HouseShot(Panel panel)
        {
            RectTransform art = Art(panel, houseArt, 1.18f);
            // Clouds drift across the sky; the sun glows.
            for (int n = 0; n < 4; n++)
            {
                RectTransform cloud = Cloud(panel.Inner, 0.9f + 0.25f * (n % 2));
                float speed = 18f + 9f * n, y = -40f - 38f * (n % 3), offset = n * 520f;
                ambient.Add(t => cloud.anchoredPosition = new Vector2(Mathf.Repeat(offset + t * speed, panel.Area.width + 400f) - 200f, y));
            }
            Image sun = NewImage("SunGlow", panel.Inner, new Color(1f, 0.92f, 0.6f, 0.55f), GlowSprite());
            RectTransform sr = sun.rectTransform;
            TopLeft(sr, panel.Area.width * 0.79f, 70f, 260f, 260f, true);
            ambient.Add(t => sr.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(t * 1.3f)));
            KenBurns(art, 1.0f);
        }

        void BusShot(Panel panel)
        {
            RectTransform art = Art(panel, walkHomeArt != null ? walkHomeArt : houseArt, 1.15f);
            KenBurns(art, 1.4f);
            // Speed lines and the sound of the bus.
            for (int n = 0; n < 6; n++)
            {
                Image line = NewImage("SpeedLine", panel.Inner, new Color(1f, 1f, 1f, 0.35f), null);
                RectTransform lr = line.rectTransform;
                float y = -150f - 38f * n, speed = 900f + 140f * n, length = 120f + 40f * (n % 3), offset = n * 230f;
                TopLeft(lr, 0f, y, length, 5f, false);
                ambient.Add(t => lr.anchoredPosition = new Vector2(Mathf.Repeat(t * speed + offset, panel.Area.width + 300f) - 200f, y));
            }
            Sound(panel, "RUMBLE\nRUMBLE...", panel.Area.width - 330f, -230f, -10f, new Color(0.98f, 0.55f, 0.2f));
        }

        void RoomShot(Panel panel)
        {
            RectTransform art = Art(panel, roomArt, 1.16f);
            KenBurns(art, 0.8f);
            // Dust floating in the window light.
            var rng = new System.Random(7);
            for (int n = 0; n < 26; n++)
            {
                Image mote = NewImage("Dust", panel.Inner, new Color(1f, 0.9f, 0.7f, 0.5f), CircleSprite());
                RectTransform mr = mote.rectTransform;
                float x = 900f + (float)rng.NextDouble() * 800f, y = -40f - (float)rng.NextDouble() * 380f;
                float size = 4f + (float)rng.NextDouble() * 6f, phase = (float)rng.NextDouble() * 10f;
                TopLeft(mr, x, y, size, size, true);
                ambient.Add(t => mr.anchoredPosition = new Vector2(x + 18f * Mathf.Sin(t * 0.4f + phase), y + 14f * Mathf.Sin(t * 0.6f + phase * 1.7f)));
            }
            Sound(panel, ". . .", panel.Area.width * 0.42f, -300f, 0f, new Color(1f, 1f, 1f, 0.6f));
        }

        void PhotoShot(Panel panel)
        {
            NewImage("Corkboard", panel.Inner, new Color(0.42f, 0.29f, 0.18f), null).rectTransform.Fill();
            // Sam's photo, pinned slightly crooked, swaying a little.
            Image frame = NewImage("Photo", panel.Inner, new Color(0.97f, 0.96f, 0.92f), null);
            RectTransform fr = frame.rectTransform;
            TopLeft(fr, 70f, 40f, 300f, 340f, false);
            Image photo = NewImage("Sam", fr, Color.white, samPortrait);
            TopLeft(photo.rectTransform, 18f, 18f, 264f, 264f, false);
            TMP_Text label = NewText("Label", fr, null, 30, InkColor, TextAlignmentOptions.Center);
            TopLeft(label.rectTransform, 0f, 286f, 300f, 46f, false);
            label.text = "SAM";
            label.fontStyle = FontStyles.Bold | FontStyles.Italic;
            Image pin = NewImage("Pin", fr, new Color(0.9f, 0.2f, 0.2f), CircleSprite());
            TopLeft(pin.rectTransform, 140f, -8f, 22f, 22f, false);
            ambient.Add(t => fr.localRotation = Quaternion.Euler(0f, 0f, -4f + 1.2f * Mathf.Sin(t * 0.9f)));
        }

        void ComputerShot(Panel panel)
        {
            RectTransform art = Art(panel, computerArt, 1.05f);
            // The monitor's screen (in the 960 x 540 picture: x 292-668, y 82-328), with code typing itself.
            float scale = art.sizeDelta.x / 960f;
            var screen = NewRect("Screen", art);
            screen.anchorMin = screen.anchorMax = screen.pivot = new Vector2(0f, 1f);
            screen.anchoredPosition = new Vector2(292f * scale, -82f * scale);
            screen.sizeDelta = new Vector2(376f * scale, 246f * scale);
            screen.gameObject.AddComponent<RectMask2D>();
            Image glow = NewImage("Glow", screen, new Color(0.05f, 0.25f, 0.12f, 0.9f), null);
            glow.rectTransform.Fill();
            TMP_Text code = NewText("Code", screen, codeFont, 15, ScreenGreen, TextAlignmentOptions.TopLeft);
            code.rectTransform.Fill(10f);
            string program = "clues = [\"bag\", \"shoes\", \"map\"]\nfor c in clues:\n    print(\"found\", c)\n\ndef checkpoint(n):\n    return \"LOCKED\"\n\nprint(checkpoint(1))\nwhile True:\n    wait_for_visitor()\n";
            code.text = program;
            ambient.Add(t =>
            {
                code.maxVisibleCharacters = (int)Mathf.Repeat(t * 22f, program.Length + 40f);
                glow.color = new Color(0.05f, 0.25f, 0.12f, 0.82f + 0.1f * Mathf.PerlinNoise(t * 6f, 0f));
            });
            KenBurns(art, 0.6f);
        }

        Terminal TerminalShot(Panel panel)
        {
            NewImage("Screen", panel.Inner, new Color(0.02f, 0.05f, 0.04f), null).rectTransform.Fill();
            // Scanlines.
            for (int n = 0; n < 42; n++)
            {
                Image line = NewImage("Scan", panel.Inner, new Color(0f, 0f, 0f, 0.25f), null);
                RectTransform lr = line.rectTransform;
                lr.anchorMin = new Vector2(0f, 1f);
                lr.anchorMax = new Vector2(1f, 1f);
                lr.pivot = new Vector2(0.5f, 1f);
                lr.sizeDelta = new Vector2(0f, 3f);
                lr.anchoredPosition = new Vector2(0f, -n * 10f);
            }
            TMP_Text text = NewText("Terminal", panel.Inner, codeFont, 34, ScreenGreen, TextAlignmentOptions.TopLeft);
            text.rectTransform.Fill(40f);
            text.richText = true;
            var terminal = new Terminal(text);
            ambient.Add(terminal.Blink);
            return terminal;
        }

        IEnumerator SentinelTakeover(Panel panel, Terminal terminal)
        {
            Sfx(SfxType.Alarm);
            Image screen = NewImage("Sentinel", panel.Inner, Color.white, sentinelScreen);
            screen.preserveAspect = true;
            screen.rectTransform.Fill(10f);
            screen.rectTransform.anchorMax = new Vector2(0.4f, 1f); // left side; the speech goes on the right
            screen.color = new Color(1f, 1f, 1f, 0f);
            var bars = new List<Image>();
            var rng = new System.Random(3);
            for (int n = 0; n < 9; n++)
            {
                Image bar = NewImage("Glitch", panel.Inner, new Color(0.94f, 0.2f, 0.3f, 0.6f), null);
                bars.Add(bar);
            }
            Vector2 home = panel.Root.anchoredPosition;
            for (float t = 0f; t < 1.4f; t += Time.unscaledDeltaTime)
            {
                foreach (Image bar in bars)
                {
                    bool on = rng.NextDouble() < 0.5;
                    bar.enabled = on;
                    if (on)
                        TopLeft(bar.rectTransform, (float)rng.NextDouble() * 1400f, -(float)rng.NextDouble() * 400f, 200f + (float)rng.NextDouble() * 900f, 6f + (float)rng.NextDouble() * 22f, false);
                }
                panel.Root.anchoredPosition = home + new Vector2((float)rng.NextDouble() * 16f - 8f, (float)rng.NextDouble() * 10f - 5f);
                screen.color = new Color(1f, 1f, 1f, Mathf.Clamp01(t / 1.2f));
                terminal.Text.alpha = 1f - Mathf.Clamp01(t / 0.8f);
                yield return null;
            }
            foreach (Image bar in bars)
                bar.enabled = false;
            panel.Root.anchoredPosition = home;
            screen.color = Color.white;
            panel.Border.color = SentinelPurple;
        }

        // ---------- Building blocks ----------

        void Build()
        {
            var go = new GameObject("PrologueCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20; // under notifications (45) and the challenge window (60)
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            NewImage("Desk", go.transform, new Color(0.02f, 0.03f, 0.05f), null).rectTransform.Fill();
            stage = NewRect("Stage", go.transform);
            stage.sizeDelta = new Vector2(1920f, 1080f);

            hint = NewText("Hint", go.transform, null, 22, new Color(0.75f, 0.82f, 0.9f), TextAlignmentOptions.BottomRight);
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = hint.rectTransform.pivot = new Vector2(1f, 0f);
            hint.rectTransform.anchoredPosition = new Vector2(-330f, 26f);
            hint.rectTransform.sizeDelta = new Vector2(1100f, 40f);
            hint.text = "Click or press SPACE to continue    ·    Esc to skip";

            // NEXT and SKIP buttons, bottom right, for mouse and touch.
            buttons = NewRect("Buttons", go.transform).gameObject;
            RectTransform br = (RectTransform)buttons.transform;
            br.anchorMin = br.anchorMax = br.pivot = new Vector2(1f, 0f);
            br.anchoredPosition = new Vector2(-24f, 16f);
            br.sizeDelta = new Vector2(300f, 54f);
            MakeButton(br, "NEXT  >", 0f, 170f, Cyan, Advance);
            MakeButton(br, "SKIP", 180f, 120f, new Color(0.6f, 0.68f, 0.78f), Skip);

            Image black = NewImage("Fade", go.transform, Color.black, null);
            black.rectTransform.Fill();
            black.raycastTarget = false;
            fade = black.gameObject.AddComponent<CanvasGroup>();
            fade.blocksRaycasts = false;
            fade.alpha = 1f;
        }

        Page NewPage(string kicker, string title)
        {
            var page = new Page { Root = NewRect("Page", stage) };
            page.Root.sizeDelta = new Vector2(1920f, 1080f);
            NewImage("Paper", page.Root, PageColor, null).rectTransform.Fill();
            // Halftone dots in the page corners, comic style.
            for (int n = 0; n < 60; n++)
            {
                Image dot = NewImage("Dot", page.Root, new Color(1f, 1f, 1f, 0.05f), CircleSprite());
                float x = 1700f + (n % 10) * 22f, y = -6f - (n / 10) * 18f;
                TopLeft(dot.rectTransform, x, y, 10f - (n / 10) * 1.3f, 10f - (n / 10) * 1.3f, true);
            }
            TMP_Text header = NewText("Header", page.Root, titleFont, 26, Cyan, TextAlignmentOptions.MidlineLeft);
            TopLeft(header.rectTransform, 60f, 30f, 1200f, 60f, false);
            header.text = $"{kicker}  <color=#9DB0C6>//</color>  <color=#FFFFFF>{title}</color>";
            header.characterSpacing = 6f;
            page.Title = NewText("Title", page.Root, titleFont, 120, Color.white, TextAlignmentOptions.Center);
            page.Title.rectTransform.sizeDelta = new Vector2(1800f, 400f);
            page.Title.text = $"<size=45%><color=#{ColorUtility.ToHtmlStringRGB(Cyan)}>{kicker}</color></size>\n{title}";
            page.Title.characterSpacing = 8f;
            page.Title.gameObject.SetActive(false);
            // Built active but off screen (text is measured while building); ShowPage slides it in.
            page.Root.anchoredPosition = new Vector2(4000f, 0f);
            return page;
        }

        // Fills a panel with a picture a little larger than the panel (cropped), so it can drift.
        RectTransform Art(Panel panel, Sprite sprite, float overscan)
        {
            Image image = NewImage("Art", panel.Inner, Color.white, sprite);
            RectTransform r = image.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            float aspect = sprite != null ? sprite.rect.width / sprite.rect.height : 16f / 9f;
            float width = Mathf.Max(panel.Area.width, panel.Area.height * aspect) * overscan;
            r.sizeDelta = new Vector2(width, width / aspect);
            return r;
        }

        void KenBurns(RectTransform art, float speed)
        {
            Vector2 start = art.anchoredPosition;
            ambient.Add(t =>
            {
                float s = 1f + 0.035f * Mathf.Sin(t * 0.08f * speed);
                art.localScale = Vector3.one * s;
                art.anchoredPosition = start + new Vector2(18f * Mathf.Sin(t * 0.11f * speed), 8f * Mathf.Sin(t * 0.07f * speed));
            });
        }

        void Portrait(Panel panel, Sprite sprite, Color background, bool screenLight)
        {
            NewImage("Back", panel.Inner, background * 0.45f + Color.black * 0.55f, null).rectTransform.Fill();
            Image burst = NewImage("Burst", panel.Inner, new Color(background.r, background.g, background.b, 0.55f), GlowSprite());
            TopLeft(burst.rectTransform, 20f, -20f, 460f, 460f, false);
            Image face = NewImage("Face", panel.Inner, Color.white, sprite);
            RectTransform fr = face.rectTransform;
            TopLeft(fr, 40f, 30f, 360f, 360f, false);
            if (screenLight)
            {
                face.color = new Color(0.75f, 1f, 0.82f);
                ambient.Add(t => face.color = Color.Lerp(new Color(0.7f, 1f, 0.78f), Color.white, 0.4f + 0.3f * Mathf.PerlinNoise(t * 5f, 1f)));
            }
            ambient.Add(t => fr.anchoredPosition = new Vector2(40f, -30f + 4f * Mathf.Sin(t * 1.6f)));
        }

        void Caption(Panel panel, string text, float x, float y, float width = 420f)
        {
            Image box = NewImage("Caption", panel.Inner, CaptionColor, RoundedSprite());
            box.type = Image.Type.Sliced;
            TMP_Text label = NewText("Text", box.rectTransform, null, 26, InkColor, TextAlignmentOptions.TopLeft);
            label.fontStyle = FontStyles.Bold;
            label.text = text;
            Vector2 size = label.GetPreferredValues(text, width - 36f, 0f);
            TopLeft(box.rectTransform, x, y, Mathf.Min(width, size.x + 40f), size.y + 28f, false);
            label.rectTransform.Fill(18f, 12f);
            box.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 1.2f);
        }

        void Sound(Panel panel, string text, float x, float y, float angle, Color color)
        {
            TMP_Text shadow = NewText("SoundShadow", panel.Inner, titleFont, 64, new Color(0f, 0f, 0f, 0.6f), TextAlignmentOptions.Center);
            TMP_Text word = NewText("Sound", panel.Inner, titleFont, 64, color, TextAlignmentOptions.Center);
            foreach (TMP_Text t in new[] { shadow, word })
            {
                t.text = text;
                t.fontStyle = FontStyles.Bold;
                TopLeft(t.rectTransform, x + (t == shadow ? 6f : 0f), y - (t == shadow ? 6f : 0f), 420f, 200f, true);
                t.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
            ambient.Add(t => word.rectTransform.localScale = shadow.rectTransform.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(t * 9f)));
        }

        Bubble Speech(Panel panel, string text, float x, float y, float width, bool tailLeft)
        {
            Image box = NewImage("Bubble", panel.Inner, Color.white, RoundedSprite());
            box.type = Image.Type.Sliced;
            TMP_Text label = NewText("Text", box.rectTransform, null, 30, InkColor, TextAlignmentOptions.TopLeft);
            Vector2 size = label.GetPreferredValues(text, width - 48f, 0f);
            TopLeft(box.rectTransform, x, y, width, size.y + 40f, false);
            label.rectTransform.Fill(24f, 18f);
            Image tail = NewImage("Tail", box.rectTransform, Color.white, null);
            RectTransform tr = tail.rectTransform;
            tr.anchorMin = tr.anchorMax = new Vector2(tailLeft ? 0f : 1f, 0f);
            tr.sizeDelta = new Vector2(30f, 30f);
            tr.anchoredPosition = new Vector2(tailLeft ? 10f : -40f, 4f);
            tr.localRotation = Quaternion.Euler(0f, 0f, 45f);
            return new Bubble(box.rectTransform, label, text);
        }

        Bubble SentinelBubble(Panel panel, string text)
        {
            Image box = NewImage("SentinelBubble", panel.Inner, new Color(0.08f, 0.05f, 0.16f, 0.95f), null);
            Outline border = box.gameObject.AddComponent<Outline>();
            border.effectColor = SentinelPurple;
            border.effectDistance = new Vector2(4f, -4f);
            TMP_Text label = NewText("Text", box.rectTransform, codeFont, 32, SentinelPurple, TextAlignmentOptions.TopLeft);
            Vector2 size = label.GetPreferredValues("SENTINEL: " + text, 500f - 48f, 0f);
            TopLeft(box.rectTransform, 720f, 30f, 500f, size.y + 40f, false);
            label.rectTransform.Fill(24f, 18f);
            return new Bubble(box.rectTransform, label, "<b>SENTINEL:</b> " + text);
        }

        void MakeButton(RectTransform parent, string label, float x, float width, Color color, Action onClick)
        {
            Image back = NewImage(label, parent, new Color(0.06f, 0.1f, 0.16f, 0.92f), RoundedSprite());
            back.type = Image.Type.Sliced;
            back.raycastTarget = true;
            TopLeft(back.rectTransform, x, 0f, width, 54f, false);
            var button = back.gameObject.AddComponent<Button>();
            button.targetGraphic = back;
            button.onClick.AddListener(() => onClick());
            TMP_Text text = NewText("Label", back.rectTransform, titleFont, 22, color, TextAlignmentOptions.Center);
            text.rectTransform.Fill();
            text.text = label;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static Image NewImage(string name, Transform parent, Color color, Sprite sprite)
        {
            RectTransform r = NewRect(name, parent);
            var image = r.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static TMP_Text NewText(string name, Transform parent, TMP_FontAsset font, float size, Color color, TextAlignmentOptions alignment)
        {
            RectTransform r = NewRect(name, parent);
            var text = r.gameObject.AddComponent<TextMeshProUGUI>();
            // Set explicitly: bubbles are measured (GetPreferredValues) before TMP would load its default font.
            text.font = font != null ? font : TMP_Settings.defaultFontAsset;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }

        // Places a child by its top-left corner inside a top-left-anchored parent (y grows downwards).
        static void TopLeft(RectTransform r, float x, float y, float w, float h, bool centred)
        {
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot = centred ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(x, y < 0f ? y : -y);
            r.sizeDelta = new Vector2(w, h);
        }

        static void Stretch(RectTransform r)
        {
            r.Fill();
        }

        RectTransform Cloud(RectTransform parent, float scale)
        {
            RectTransform cloud = NewRect("Cloud", parent);
            cloud.anchorMin = cloud.anchorMax = new Vector2(0f, 1f);
            cloud.pivot = new Vector2(0.5f, 0.5f);
            cloud.sizeDelta = new Vector2(220f, 90f) * scale;
            float[,] puffs = { { -60f, -6f, 90f }, { 0f, 10f, 120f }, { 62f, -4f, 96f }, { 20f, -20f, 110f } };
            for (int n = 0; n < puffs.GetLength(0); n++)
            {
                Image puff = NewImage("Puff", cloud, new Color(1f, 1f, 1f, 0.7f), CircleSprite());
                RectTransform pr = puff.rectTransform;
                pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0.5f, 0.5f);
                pr.anchoredPosition = new Vector2(puffs[n, 0], puffs[n, 1]) * scale;
                pr.sizeDelta = Vector2.one * puffs[n, 2] * scale * 0.8f;
            }
            return cloud;
        }

        // ---------- Pages, panels, bubbles, terminal ----------

        class Page
        {
            public RectTransform Root;
            public TMP_Text Title;

            public Panel AddPanel(float x, float y, float w, float h)
            {
                var panel = new Panel { Area = new Rect(x, y, w, h) };
                Image border = NewImage("Panel", Root, Color.white, null);
                panel.Border = border;
                panel.Root = border.rectTransform;
                TopLeft(panel.Root, x + w * 0.5f, y + h * 0.5f, w, h, true);
                panel.Group = border.gameObject.AddComponent<CanvasGroup>();
                panel.Group.alpha = 0f;
                // Inner area, inset by the ink border, clipping its art.
                panel.Inner = NewRect("Inner", panel.Root);
                panel.Inner.anchorMin = Vector2.zero;
                panel.Inner.anchorMax = Vector2.one;
                panel.Inner.offsetMin = new Vector2(6f, 6f);
                panel.Inner.offsetMax = new Vector2(-6f, -6f);
                panel.Inner.pivot = new Vector2(0f, 1f);
                panel.Inner.gameObject.AddComponent<RectMask2D>();
                NewImage("Back", panel.Inner, new Color(0.1f, 0.12f, 0.16f), null).rectTransform.Fill();
                return panel; // hidden by its CanvasGroup (alpha 0) until Reveal
            }
        }

        class Panel
        {
            public Rect Area;
            public RectTransform Root;
            public RectTransform Inner;
            public Image Border;
            public CanvasGroup Group;
        }

        class Bubble
        {
            readonly RectTransform root;
            readonly TMP_Text text;

            public Bubble(RectTransform root, TMP_Text text, string content)
            {
                this.root = root;
                this.text = text;
                text.text = content;
                text.maxVisibleCharacters = 0;
                root.localScale = Vector3.zero;
            }

            // Pops in, then types its words (a click shows them all at once).
            public IEnumerator Show(PrologueComic comic)
            {
                Sfx(SfxType.Button);
                float seconds = comic.fastForward ? 0f : 0.22f;
                for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
                {
                    float p = t / seconds;
                    root.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(p * Mathf.PI)) * p;
                    yield return null;
                }
                root.localScale = Vector3.one;
                text.ForceMeshUpdate();
                int total = text.textInfo.characterCount;
                float start = Time.unscaledTime;
                while (!comic.fastForward && text.maxVisibleCharacters < total)
                {
                    if (Time.unscaledTime - start > 0.1f && comic.AdvancePressed())
                        break;
                    text.maxVisibleCharacters = (int)((Time.unscaledTime - start) * 55f);
                    yield return null;
                }
                text.maxVisibleCharacters = 99999;
            }
        }

        class Terminal
        {
            public readonly TMP_Text Text;
            public bool Prompted;
            string content = string.Empty;

            public Terminal(TMP_Text text)
            {
                Text = text;
            }

            public IEnumerator Type(PrologueComic comic, string line)
            {
                for (int n = 0; n < line.Length; n++)
                {
                    content += line[n];
                    if (!comic.fastForward && line[n] != ' ')
                        yield return new WaitForSecondsRealtime(0.025f);
                }
            }

            public void Append(string line)
            {
                content += line;
            }

            public void Blink(float t)
            {
                Text.text = content + (Mathf.Repeat(t, 1f) < 0.5f ? "_" : " ");
            }
        }

        // ---------- Sprites made in code (no image files needed) ----------

        static Sprite rounded, circle, glow;

        static Sprite RoundedSprite()
        {
            if (rounded != null)
                return rounded;
            const int size = 64;
            const float radius = 22f;
            rounded = MakeSprite(size, (x, y) =>
            {
                float qx = Mathf.Max(radius - x, x - (size - radius), 0f);
                float qy = Mathf.Max(radius - y, y - (size - radius), 0f);
                return Mathf.Clamp01(radius - Mathf.Sqrt(qx * qx + qy * qy) + 0.5f);
            }, new Vector4(radius + 2f, radius + 2f, radius + 2f, radius + 2f));
            return rounded;
        }

        static Sprite CircleSprite()
        {
            if (circle == null)
                circle = MakeSprite(128, (x, y) => Mathf.Clamp01(63.5f - Vector2.Distance(new Vector2(x, y), new Vector2(64f, 64f)) + 0.5f), Vector4.zero);
            return circle;
        }

        static Sprite GlowSprite()
        {
            if (glow == null)
                glow = MakeSprite(128, (x, y) =>
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(64f, 64f)) / 64f;
                    return Mathf.Pow(Mathf.Clamp01(1f - d), 2f);
                }, Vector4.zero);
            return glow;
        }

        static Sprite MakeSprite(int size, Func<float, float, float> alpha, Vector4 border)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255f * alpha(x + 0.5f, y + 0.5f)));
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }
    }

    static class PrologueRectExtensions
    {
        /// <summary>Stretches a rect over its parent, with an optional inset.</summary>
        public static void Fill(this RectTransform r, float inset = 0f, float insetY = -1f)
        {
            float iy = insetY < 0f ? inset : insetY;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.offsetMin = new Vector2(inset, iy);
            r.offsetMax = new Vector2(-inset, -iy);
        }
    }
}
