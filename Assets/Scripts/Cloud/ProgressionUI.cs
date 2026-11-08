using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using CodeEscape.Game;
using CodeEscape.Programming;
using CodeEscape.UI;

namespace CodeEscape.Cloud
{
    /// <summary>
    /// The cloud's on-screen parts, built in code on its own overlay canvas so no scene needs editing: a gate that
    /// blocks the game until the student is signed in and the progress has loaded (welcome, sign in, create account,
    /// errors with Retry), the account menu (sign out, delete account), an account
    /// button on the main menu, a save-status chip with Retry, and a progression
    /// panel on the main menu and level map that shows the data last retrieved from, or confirmed by, Cloud Save.
    /// </summary>
    public class ProgressionUI : MonoBehaviour
    {
        const float SavedMessageSeconds = 4f;
        const int GateButtonCount = 4;
        const int GateFieldCount = 3;

        // The main menu's look (see MenuTheme).
        static readonly Color Panel = MenuTheme.Panel;
        static readonly Color Accent = MenuTheme.Ice;
        static readonly Color Bad = MenuTheme.Danger;
        static readonly Color Good = MenuTheme.Good;

        enum Page { None, Welcome, SignIn, Register, Account, DeleteConfirm }

        struct FieldSpec
        {
            public string Label;
            public InputField.ContentType Type;
            public int Limit;
            /// <summary>Filled in when the screen opens (e.g. the username typed on the previous screen). Never a password.</summary>
            public string Value;
            public FieldSpec(string label, InputField.ContentType type, int limit = 80, string value = null)
            {
                Label = label;
                Type = type;
                Limit = limit;
                Value = value;
            }
        }

        Font font;
        GameObject gate;
        Text gateTitle;
        Text gateDetail;
        Text gateMessage;
        RectTransform gateDetailRect;
        readonly Button[] gateButtons = new Button[GateButtonCount];
        readonly Action[] gateActions = new Action[GateButtonCount];
        readonly InputField[] gateFields = new InputField[GateFieldCount];
        readonly Text[] gateLabels = new Text[GateFieldCount];
        string appliedKey = string.Empty;
        Page page = Page.None;
        CloudState lastState = CloudState.Starting;
        bool busy;
        // Carried between the screens of one sign-in (never a password).
        string pendingUsername = string.Empty;
        GameObject chip;
        Text chipText;
        Button chipRetry;
        GameObject panel;
        Text panelText;
        GameObject panelBody;
        Text panelToggleLabel;
        bool dirty = true;
        // Starts collapsed so it doesn't cover the menu artwork; one click opens it.
        bool panelCollapsed = true;
        float savedShownUntil;
        bool chipHiddenForChallenge;
        CloudSaveStatus lastStatus = CloudSaveStatus.Synced;

        static ProgressionUI instance;

        void Awake()
        {
            instance = this;
            font = MenuTheme.Font != null ? MenuTheme.Font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildCanvas();
        }

        /// <summary>Opens the account menu (the main menu's ACCOUNT button). Does nothing until the student is signed in.</summary>
        public static void OpenAccountMenu()
        {
            CloudProgressService service = CloudProgressService.Instance;
            if (instance != null && service != null && service.State == CloudState.Ready)
                instance.GoTo(Page.Account);
        }

        void OnEnable()
        {
            CloudProgressService.Changed += OnChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            CloudProgressService.Changed -= OnChanged;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        void OnChanged()
        {
            dirty = true;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (CloudProgressService.Instance != null && CloudProgressService.Instance.State == CloudState.Ready)
                page = Page.None;
            dirty = true;
        }

        void Update()
        {
            CloudProgressService service = CloudProgressService.Instance;
            if (service == null)
                return;

            if (service.SaveStatus != lastStatus)
            {
                if (service.SaveStatus == CloudSaveStatus.Synced && lastStatus == CloudSaveStatus.Saving)
                    savedShownUntil = Time.unscaledTime + SavedMessageSeconds;
                lastStatus = service.SaveStatus;
            }

            if (service.State != lastState)
            {
                lastState = service.State;
                // A new sign-in situation starts on the right screen.
                if (service.State == CloudState.ChoosingAccount)
                    page = Page.Welcome;
                else if (service.State == CloudState.Ready)
                {
                    // Show the account menu once after sign-in when there is something to tell (e.g. a changed password).
                    page = !string.IsNullOrEmpty(service.AccountNotice) && SceneManager.GetActiveScene().name == SceneNames.MainMenu
                        ? Page.Account : Page.None;
                }
                dirty = true;
            }

            bool gated = IsGated(service);
            if (gated)
            {
                KeepFocusOnGate();
                SubmitOnEnter();
            }
            // The chip would cover the challenge window's buttons: hide it while one is open.
            ProgrammingChallengeManager challenges = ProgrammingChallengeManager.Instance;
            bool challengeOpen = challenges != null && challenges.IsChallengeOpen;
            if (challengeOpen != chipHiddenForChallenge)
            {
                chipHiddenForChallenge = challengeOpen;
                dirty = true;
            }
            if (dirty || (chip.activeSelf && Time.unscaledTime > savedShownUntil && service.SaveStatus == CloudSaveStatus.Synced))
            {
                dirty = false;
                Refresh(service);
            }
        }

        bool IsGated(CloudProgressService service)
        {
            return service.State != CloudState.Ready || page != Page.None;
        }

        // ---------- Refresh ----------

        void Refresh(CloudProgressService service)
        {
            bool gated = IsGated(service);
            gate.SetActive(gated);
            if (gated)
                RefreshGate(service);
            else
                appliedKey = string.Empty;

            RefreshChip(service);
            RefreshPanel(service);
        }

        void RefreshGate(CloudProgressService service)
        {
            switch (service.State)
            {
                case CloudState.Failed:
                    Apply("failed", "Cannot connect to the cloud", service.ConnectionError +
                        "\n\nCode Escape needs an internet connection to load your progress. Gameplay is locked until it loads.",
                        null, 28, 220, ("RETRY", service.RetryConnect));
                    break;
                case CloudState.ChoosingAccount:
                    RefreshAuthPage(service);
                    break;
                case CloudState.Ready:
                    RefreshAccountPages(service);
                    break;
                default:
                    Apply("connecting", "Connecting to Code Escape", StateMessage(service.State), null, 28, 220);
                    break;
            }
        }

        // The screens a student sees before they are signed in.
        void RefreshAuthPage(CloudProgressService service)
        {
            switch (page)
            {
                case Page.SignIn:
                    Apply("signin", "Sign in", "Type the username and password of your Code Escape account.",
                        new[] { new FieldSpec("Username", InputField.ContentType.Standard, 20, pendingUsername), new FieldSpec("Password", InputField.ContentType.Password, 30) },
                        28, 110,
                        ("SIGN IN", () => Run(() => service.SignInAsync(Field(0), Field(1)), null)),
                        ("BACK", () => GoTo(Page.Welcome)));
                    break;
                case Page.Register:
                    Apply("register", "Create your account",
                        "Username: 3-20 letters or numbers (. - @ _ allowed, no spaces). Don't use your real full name.\n" +
                        "Password: 8-30 characters with an upper case letter, a lower case letter, a number and a symbol. " +
                        "Remember it: the game cannot reset it.",
                        new[] { new FieldSpec("Username", InputField.ContentType.Standard, 20), new FieldSpec("Password", InputField.ContentType.Password, 30), new FieldSpec("Password again", InputField.ContentType.Password, 30) },
                        24, 130,
                        ("CREATE ACCOUNT", () => Run(() => service.RegisterAsync(Field(0), Field(1), Field(2)), null, afterError: () =>
                        {
                            // Keep the username, so SIGN IN (e.g. "That username is taken") starts with it filled in.
                            pendingUsername = Field(0);
                        })),
                        ("BACK", () => GoTo(Page.Welcome)));
                    break;
                default:
                    Apply("welcome", "Welcome to Code Escape",
                        "Sign in to keep your progress and continue on any computer.\nNew here? Create an account with a username and a password.\n\n" +
                        (service.HasGuestProgress
                            ? "<color=#6ee89a>This computer has progress from before accounts existed. CREATE ACCOUNT keeps it in your new account.</color>\n\n"
                            : string.Empty) +
                        "<color=#ffd24a>Under 13? Ask a parent or teacher before creating an account.</color>" +
                        (string.IsNullOrEmpty(service.AccountNotice) ? string.Empty : "\n\n" + service.AccountNotice),
                        null, 28, 230,
                        ("SIGN IN", () => GoTo(Page.SignIn)),
                        ("CREATE ACCOUNT", () => GoTo(Page.Register)));
                    break;
            }
        }

        // The account menu and what opens from it, for a signed-in student.
        void RefreshAccountPages(CloudProgressService service)
        {
            switch (page)
            {
                case Page.DeleteConfirm:
                    Apply("delete", "Delete your account?",
                        "<color=#ff7b7b>This permanently deletes your account, your progress and your score. It cannot be undone.</color>",
                        null, 28, 200,
                        ("YES, DELETE EVERYTHING", () => Run(() => service.DeleteAccountAsync(), () => page = Page.Welcome)),
                        ("CANCEL", () => GoTo(Page.Account)));
                    break;
                default:
                    ApplyAccountMenu(service);
                    break;
            }
        }

        void ApplyAccountMenu(CloudProgressService service)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<b>" + (string.IsNullOrEmpty(service.DisplayName) ? "Player" : Escape(service.DisplayName)) + "</b>");
            sb.AppendLine("Code Escape account (username and password)");
            sb.Append("\nYour progress is saved to your account, so you can sign in on any computer. On a shared computer, sign out when you finish.");
            if (!string.IsNullOrEmpty(service.AccountNotice))
                sb.Append("\n\n<color=#6ee89a>" + Escape(service.AccountNotice) + "</color>");

            Apply("account", "Your account", sb.ToString(), null, 26, 230,
                ("SIGN OUT", () => Run(() => service.SignOutAsync(), null)),
                ("DELETE MY ACCOUNT", () => GoTo(Page.DeleteConfirm)),
                ("BACK", () =>
                {
                    service.ClearNotice();
                    GoTo(Page.None);
                }));
        }

        // Names are typed by students: turn the rich-text brackets into plain characters.
        static string Escape(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : text.Replace("<", "‹").Replace(">", "›");
        }

        static string StateMessage(CloudState state)
        {
            switch (state)
            {
                case CloudState.Starting: return "Starting...";
                case CloudState.SigningIn: return "Signing in...";
                case CloudState.Loading: return "Loading your progress...";
                default: return string.Empty;
            }
        }

        // ---------- Gate building blocks ----------

        void GoTo(Page next, string message = null)
        {
            page = next;
            appliedKey = string.Empty;
            SetMessage(message ?? string.Empty, false);
            dirty = true;
        }

        string Field(int index)
        {
            return gateFields[index] != null ? gateFields[index].text.Trim() : string.Empty;
        }

        void SetMessage(string text, bool bad)
        {
            gateMessage.text = text ?? string.Empty;
            gateMessage.color = bad ? Bad : Good;
        }

        // Runs an account action, shows its error (or runs onSuccess). Buttons do nothing while one is running.
        async void Run(Func<Task<string>> action, Action onSuccess, Action afterError = null)
        {
            if (busy)
                return;
            busy = true;
            SetMessage("Please wait...", false);
            string error;
            try
            {
                error = await action();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                error = "Something went wrong. Try again.";
            }
            busy = false;
            if (error != null)
            {
                SetMessage(error, true);
                afterError?.Invoke();
            }
            else
            {
                SetMessage(string.Empty, false);
                onSuccess?.Invoke();
            }
            dirty = true;
        }

        // Shows a screen. Typed text and the message survive refreshes: the fields and buttons are only rebuilt when the
        // screen changes (a new key); otherwise just the text is updated.
        void Apply(string key, string title, string detail, FieldSpec[] fields, int detailSize, float detailHeight,
            params (string label, Action action)[] buttons)
        {
            gateTitle.text = title.ToUpperInvariant();
            gateDetail.text = detail;
            if (key == appliedKey)
                return;
            appliedKey = key;

            gateDetail.fontSize = detailSize;
            gateDetailRect.sizeDelta = new Vector2(1360, detailHeight);
            gateDetailRect.anchoredPosition = new Vector2(0, 115f - detailHeight * 0.5f);

            int count = fields != null ? fields.Length : 0;
            for (int i = 0; i < GateFieldCount; i++)
            {
                bool used = i < count;
                gateFields[i].gameObject.SetActive(used);
                gateLabels[i].gameObject.SetActive(used);
                if (!used)
                    continue;
                float y = 115f - detailHeight - 40f - i * 66f;
                ((RectTransform)gateFields[i].transform).anchoredPosition = new Vector2(70, y);
                ((RectTransform)gateLabels[i].transform).anchoredPosition = new Vector2(-545, y);
                gateLabels[i].text = fields[i].Label;
                gateFields[i].contentType = fields[i].Type;
                gateFields[i].characterLimit = fields[i].Limit;
                gateFields[i].text = fields[i].Value ?? string.Empty;
                if (gateFields[i].placeholder is Text placeholder)
                    placeholder.text = string.Empty;
            }

            for (int i = 0; i < GateButtonCount; i++)
            {
                bool used = i < buttons.Length;
                gateButtons[i].gameObject.SetActive(used);
                gateActions[i] = used ? buttons[i].action : null;
                if (!used)
                    continue;
                Text label = gateButtons[i].GetComponentInChildren<Text>();
                label.text = buttons[i].label;
                label.fontSize = buttons.Length >= 3 ? 20 : 24;
                // Deleting is the one destructive action: red like the main menu's EXIT.
                label.color = buttons[i].label.Contains("DELETE") ? Bad : Color.white;
                gateButtons[i].GetComponent<RectTransform>().anchoredPosition = new Vector2((i - (buttons.Length - 1) * 0.5f) * 370f, -310f);
            }
            // Focus: the first field, or the first button.
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(count > 0 ? gateFields[0].gameObject : gateButtons[0].gameObject);
        }

        // While the gate is up, keyboard/gamepad focus must not reach the menu behind it.
        void KeepFocusOnGate()
        {
            EventSystem es = EventSystem.current;
            if (es == null)
                return;
            GameObject current = es.currentSelectedGameObject;
            if (current != null && current.activeInHierarchy && current.transform.IsChildOf(gate.transform))
                return;
            GameObject target = null;
            foreach (InputField field in gateFields)
            {
                if (field.gameObject.activeInHierarchy)
                {
                    target = field.gameObject;
                    break;
                }
            }
            if (target == null)
            {
                foreach (Button button in gateButtons)
                {
                    if (button.gameObject.activeInHierarchy)
                    {
                        target = button.gameObject;
                        break;
                    }
                }
            }
            es.SetSelectedGameObject(target);
        }

        // Enter inside a text box presses the first (main) button of the screen.
        void SubmitOnEnter()
        {
            Keyboard keyboard = Keyboard.current;
            EventSystem es = EventSystem.current;
            if (keyboard == null || es == null || busy)
                return;
            if (!(keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
                return;
            GameObject current = es.currentSelectedGameObject;
            if (current != null && current.GetComponent<InputField>() != null && gateButtons[0].gameObject.activeInHierarchy)
                gateButtons[0].onClick.Invoke();
        }

        // ---------- Account button, chip, panel ----------

        void RefreshChip(CloudProgressService service)
        {
            string message = null;
            bool showRetry = false;
            switch (service.SaveStatus)
            {
                case CloudSaveStatus.Unsaved:
                    message = "<color=#ffd24a>Unsaved changes</color> - not in the cloud yet";
                    break;
                case CloudSaveStatus.Saving:
                    message = "<color=#9ad7ff>Saving to cloud...</color>";
                    break;
                case CloudSaveStatus.Failed:
                    message = "<color=#ff7b7b>Cloud save failed</color>\n" + Shorten(service.SaveError, 110);
                    showRetry = true;
                    break;
                case CloudSaveStatus.Synced:
                    if (Time.unscaledTime <= savedShownUntil)
                    {
                        message = "<color=#6ee89a>Saved to cloud</color> " + LocalTime(service.LastConfirmedSaveUtc);
                        if (!string.IsNullOrEmpty(service.LastUnlockedAchievement))
                            message += "\nAchievement: " + service.LastUnlockedAchievement;
                    }
                    break;
            }
            chip.SetActive(message != null && service.State == CloudState.Ready && !chipHiddenForChallenge);
            chipText.text = message ?? string.Empty;
            chipRetry.gameObject.SetActive(showRetry);
            chipText.rectTransform.offsetMax = new Vector2(showRetry ? -150f : -14f, -6f);
            PlaceChip(SceneManager.GetActiveScene().name);
        }

        void RefreshPanel(CloudProgressService service)
        {
            string scene = SceneManager.GetActiveScene().name;
            bool visible = service.State == CloudState.Ready && service.Record != null
                           && (scene == SceneNames.MainMenu || scene == SceneNames.LevelMap);
            panel.SetActive(visible);
            if (visible)
                PlacePanel();
            panelBody.SetActive(visible && !panelCollapsed);
            panelToggleLabel.text = panelCollapsed ? "CLOUD PROGRESS  +" : "CLOUD PROGRESS  -";
            if (!visible || panelCollapsed)
                return;

            PlayerProgressData r = service.Record;
            LevelManager levels = LevelManager.Instance;
            var sb = new StringBuilder();
            switch (service.SaveStatus)
            {
                case CloudSaveStatus.Synced:
                    sb.AppendLine("<color=#6ee89a>All changes saved</color> " + LocalTime(service.LastConfirmedSaveUtc));
                    break;
                case CloudSaveStatus.Failed:
                    sb.AppendLine("<color=#ff7b7b>Latest changes NOT saved</color>");
                    break;
                default:
                    sb.AppendLine("<color=#ffd24a>Latest changes not saved yet</color>");
                    break;
            }
            sb.AppendLine($"<color=#8fa8c0>Player ID</color>  {ShortId(r.player_id)}");
            sb.AppendLine($"<color=#8fa8c0>Current level</color>  {LevelLabel(levels, r.current_level)}");
            sb.AppendLine($"<color=#8fa8c0>Highest unlocked</color>  {LevelLabel(levels, r.highest_unlocked_level)}");
            int total = levels != null ? levels.LevelCount : 0;
            sb.AppendLine($"<color=#8fa8c0>Completed</color>  {r.completed_levels.Count}{(total > 0 ? " / " + total : string.Empty)}");
            foreach (string id in r.completed_levels)
                sb.AppendLine("   - " + LevelLabel(levels, id));
            sb.AppendLine($"<color=#8fa8c0>Total score</color>  {r.total_score}");
            sb.Append("<color=#8fa8c0>Achievements</color>  ");
            if (r.achievements.Count == 0)
                sb.Append("none yet");
            foreach (string id in r.achievements)
                sb.Append("\n   - " + Achievements.NameOf(id));
            sb.AppendLine();
            sb.Append("<color=#8fa8c0>Last cloud update</color>  " + FormatStamp(r.last_played));
            panelText.text = sb.ToString();
        }

        static string LevelLabel(LevelManager levels, string levelId)
        {
            if (string.IsNullOrEmpty(levelId))
                return "-";
            LevelInfo info = levels != null ? levels.GetInfoById(levelId) : null;
            return info != null ? $"Level {levels.IndexOf(info) + 1} - {info.displayName}" : levelId;
        }

        static string ShortId(string id)
        {
            return string.IsNullOrEmpty(id) ? "-" : (id.Length > 10 ? id.Substring(0, 10) + "..." : id);
        }

        static string LocalTime(DateTime? utc)
        {
            return utc.HasValue ? "(" + utc.Value.ToLocalTime().ToString("HH:mm:ss") + ")" : string.Empty;
        }

        static string FormatStamp(string iso)
        {
            return DateTime.TryParse(iso, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime t)
                ? t.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "-";
        }

        static string Shorten(string text, int max)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            return text.Length <= max ? text : text.Substring(0, max) + "...";
        }

        // ---------- Building the overlay ----------

        void BuildCanvas()
        {
            var canvasGo = new GameObject("CloudCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            RectTransform root = canvasGo.GetComponent<RectTransform>();

            // Gate: full screen, swallows clicks.
            gate = NewImage("Gate", root, new Color(0.02f, 0.05f, 0.1f, 0.97f));
            gate.GetComponent<Image>().sprite = null; // a plain full-screen dim, no pill corner
            Stretch(gate.GetComponent<RectTransform>());
            // A solid card behind the text and buttons, so the menu behind the gate never shows through them.
            GameObject card = NewImage("Card", gate.GetComponent<RectTransform>(), MenuTheme.PanelSolid);
            Place(card.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(1500, 580), new Vector2(0, -85));
            Outline(card, 0.28f);
            gateTitle = NewText("Title", gate.transform, 40, TextAnchor.MiddleCenter, Color.white);
            Place(gateTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1400, 90), new Vector2(0, 150));
            gateDetail = NewText("Detail", gate.transform, 28, TextAnchor.UpperCenter, new Color(0.8f, 0.88f, 0.96f));
            gateDetailRect = gateDetail.rectTransform;
            Place(gateDetailRect, new Vector2(0.5f, 0.5f), new Vector2(1360, 230), new Vector2(0, 0));
            gateMessage = NewText("Message", gate.transform, 26, TextAnchor.MiddleCenter, Bad);
            Place(gateMessage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1400, 44), new Vector2(0, -238));

            for (int i = 0; i < GateFieldCount; i++)
            {
                gateLabels[i] = NewText("FieldLabel" + i, gate.transform, 26, TextAnchor.MiddleRight, new Color(0.7f, 0.8f, 0.9f));
                // Right-aligned, ending just left of the input box (which spans x -310 to 450).
                Place(gateLabels[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(420, 52), Vector2.zero);
                gateFields[i] = NewInput("Field" + i, gate.transform);
                gateLabels[i].gameObject.SetActive(false);
                gateFields[i].gameObject.SetActive(false);
            }
            for (int i = 0; i < GateButtonCount; i++)
            {
                int index = i;
                gateButtons[i] = NewButton("Button" + i, gate.transform, string.Empty, new Vector2(350, 80), Vector2.zero, 28, () =>
                {
                    if (!busy)
                        gateActions[index]?.Invoke();
                    dirty = true;
                });
                gateButtons[i].gameObject.SetActive(false);
            }

            // Status chip: top right on the menus, bottom right (above the controls bar) in the levels.
            chip = NewImage("StatusChip", root, Panel);
            chip.GetComponent<RectTransform>().sizeDelta = new Vector2(480, 84);
            Outline(chip, 0.3f);
            chipText = NewText("Text", chip.transform, 20, TextAnchor.MiddleLeft, Color.white);
            Stretch(chipText.rectTransform, 14, 6, 14, 6);
            chipText.resizeTextForBestFit = true;
            chipText.resizeTextMinSize = 13;
            chipText.resizeTextMaxSize = 20;
            chipText.verticalOverflow = VerticalWrapMode.Truncate;
            chipRetry = NewButton("Retry", chip.transform, "RETRY", new Vector2(120, 48), new Vector2(0, 0), 22, () =>
            {
                if (CloudProgressService.Instance != null)
                    CloudProgressService.Instance.RetrySave();
            });
            var rr = chipRetry.GetComponent<RectTransform>();
            rr.anchorMin = rr.anchorMax = new Vector2(1f, 0.5f);
            rr.anchoredPosition = new Vector2(-72, 0);
            chip.SetActive(false);

            // Progression panel (bottom right, see PlacePanel).
            panel = NewImage("ProgressPanel", root, Panel);
            Place(panel.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(620, 600), new Vector2(340, 320));
            var rt = panel.GetComponent<RectTransform>();
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(24, 24);
            Outline(panel, 0.3f);
            Button toggle = NewButton("Toggle", panel.transform, "CLOUD PROGRESS  -", new Vector2(620, 54), Vector2.zero, 24, () =>
            {
                panelCollapsed = !panelCollapsed;
                SizePanel();
                dirty = true;
            });
            var tr = toggle.GetComponent<RectTransform>();
            tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 1f);
            tr.pivot = new Vector2(0.5f, 1f);
            tr.anchoredPosition = Vector2.zero;
            panelToggleLabel = toggle.GetComponentInChildren<Text>();
            // Quieter than the other buttons: a dark translucent bar with cyan text.
            panelToggleLabel.color = Accent;
            panelToggleLabel.fontSize = 18;
            panelBody = new GameObject("Body", typeof(RectTransform));
            panelBody.transform.SetParent(panel.transform, false);
            Stretch(panelBody.GetComponent<RectTransform>(), 16, 12, 16, 62);
            panelText = NewText("Text", panelBody.transform, 22, TextAnchor.UpperLeft, Color.white);
            Stretch(panelText.rectTransform, 0, 0, 0, 0);
            panelText.verticalOverflow = VerticalWrapMode.Truncate;
            panel.SetActive(false);
            SizePanel();

            // The gate (also used for the account menu) covers everything else on this canvas.
            gate.transform.SetAsLastSibling();
        }

        static bool IsMenuScene(string scene)
        {
            return scene == SceneNames.MainMenu || scene == SceneNames.LevelMap || scene == SceneNames.LanguageSelect
                || scene == SceneNames.Intro || scene == SceneNames.Ending || scene == SceneNames.Results;
        }

        // Menus have free space top right (on the main menu, below its row of icon buttons). In the levels the top
        // corners hold the HUD and achievement toasts, so the chip sits bottom right, above the controls bar.
        void PlaceChip(string scene)
        {
            bool menu = IsMenuScene(scene);
            var rt = chip.GetComponent<RectTransform>();
            Vector2 corner = menu ? new Vector2(1f, 1f) : new Vector2(1f, 0f);
            rt.anchorMin = rt.anchorMax = rt.pivot = corner;
            rt.anchoredPosition = !menu ? new Vector2(-24, 96) : scene == SceneNames.MainMenu ? new Vector2(-24, -170) : new Vector2(-24, -24);
        }

        void SizePanel()
        {
            var rt = panel.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(620, panelCollapsed ? 54 : 600);
        }

        // Bottom right above the footer: clear of the main menu column and of the level map's Back button.
        void PlacePanel()
        {
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-24, 64);
        }

        GameObject NewImage(string name, RectTransform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            if (MenuTheme.PanelSprite != null)
            {
                image.sprite = MenuTheme.PanelSprite;
                image.type = Image.Type.Sliced;
            }
            return go;
        }

        // The thin white border of the main menu's pills, over the whole of target.
        static Image Outline(GameObject target, float alpha)
        {
            var go = new GameObject("Outline", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(target.transform, false);
            var image = go.GetComponent<Image>();
            image.sprite = MenuTheme.Outline;
            image.type = Image.Type.Sliced;
            image.color = new Color(1f, 1f, 1f, alpha);
            image.raycastTarget = false;
            Stretch(go.GetComponent<RectTransform>());
            return image;
        }

        Text NewText(string name, Transform parent, int size, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.raycastTarget = false;
            return text;
        }

        InputField NewInput(string name, Transform parent)
        {
            GameObject go = NewImage(name, parent.GetComponent<RectTransform>(), new Color(0.06f, 0.12f, 0.2f, 1f));
            Outline(go, 0.4f);
            Place(go.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(760, 52), Vector2.zero);
            Text text = NewText("Text", go.transform, 28, TextAnchor.MiddleLeft, Color.white);
            Stretch(text.rectTransform, 14, 4, 14, 4);
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            Text placeholder = NewText("Placeholder", go.transform, 28, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.35f));
            Stretch(placeholder.rectTransform, 14, 4, 14, 4);
            var input = go.AddComponent<InputField>();
            input.textComponent = text;
            input.placeholder = placeholder;
            input.targetGraphic = go.GetComponent<Image>();
            input.lineType = InputField.LineType.SingleLine;
            return input;
        }

        // A "ghost" button like the main menu's: see-through navy, a thin outline that lights up in Ice while
        // highlighted or selected.
        Button NewButton(string name, Transform parent, string label, Vector2 size, Vector2 position, int fontSize, Action onClick)
        {
            GameObject go = NewImage(name, parent.GetComponent<RectTransform>(), Panel);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
            Image outline = Outline(go, 1f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = outline;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0.4f);
            colors.highlightedColor = Accent;
            colors.selectedColor = Accent;
            colors.pressedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.15f);
            button.colors = colors;
            button.onClick.AddListener(() => onClick());
            Text text = NewText("Label", go.transform, fontSize, TextAnchor.MiddleCenter, Color.white);
            Stretch(text.rectTransform);
            text.text = label;
            return button;
        }

        static void Stretch(RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 position)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
        }
    }
}
