using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using PlayerData = Unity.Services.CloudSave.Models.Data.Player;
using Unity.Services.Core;
using CodeEscape.Game;
using CodeEscape.Programming;
using CodeEscape.Rewards;
using CodeEscape.SaveSystem;

namespace CodeEscape.Cloud
{
    public enum CloudState
    {
        Starting,
        SigningIn,
        Loading,
        /// <summary>Player signed in and the progression loaded. Gameplay may start.</summary>
        Ready,
        Failed,
        /// <summary>Nobody is signed in on this computer: the player signs in or creates an account.</summary>
        ChoosingAccount
    }

    public enum CloudSaveStatus
    {
        /// <summary>Everything the game knows is confirmed stored in Cloud Save.</summary>
        Synced,
        /// <summary>There are changes that have not been sent (or not confirmed) yet.</summary>
        Unsaved,
        Saving,
        Failed
    }

    /// <summary>
    /// Stores and retrieves accounts and progression with Unity Gaming Services: Unity Authentication (username and
    /// password) and Cloud Save. On start it signs the student last signed in on this computer in again (Unity keeps a
    /// session token, never the password); otherwise the student creates an account or signs in. Then it loads the
    /// saved progression and restores it into the existing save system. Afterwards it saves automatically, in the
    /// background, when levels are cleared or unlocked, points are earned, achievements unlock and the main menu is
    /// reached. The existing local save (PlayerPrefs, SaveManager) is still what the game runs on; the cloud copy is only
    /// reported as saved once Cloud Save has confirmed it. Passwords are never stored or logged.
    /// See Documentation/CloudSave.md.
    /// </summary>
    public class CloudProgressService : MonoBehaviour
    {
        public const string KeyProgression = "code_escape_progression";
        public const string KeySnapshot = "code_escape_snapshot";

        const string PrefUnsynced = "CodeEscape.Cloud.Unsynced";
        const string PrefPlayerId = "CodeEscape.Cloud.PlayerId";
        const float SaveDebounceSeconds = 3f;
        const int TimeoutMilliseconds = 20000;

        public static CloudProgressService Instance { get; private set; }

        /// <summary>Raised whenever the connection state, save status or confirmed record changes.</summary>
        public static event Action Changed;

        public CloudState State { get; private set; } = CloudState.Starting;
        public string ConnectionError { get; private set; } = string.Empty;
        public CloudSaveStatus SaveStatus { get; private set; } = CloudSaveStatus.Synced;
        public string SaveError { get; private set; } = string.Empty;
        public DateTime? LastConfirmedSaveUtc { get; private set; }
        /// <summary>The progression last loaded from, or confirmed saved to, Cloud Save. Null until loaded.</summary>
        public PlayerProgressData Record { get; private set; }
        /// <summary>True when the last load replaced the local game state with the cloud one.</summary>
        public bool RestoredFromCloud { get; private set; }
        /// <summary>Achievements unlocked by the latest save, for a short on-screen message.</summary>
        public string LastUnlockedAchievement { get; private set; } = string.Empty;
        /// <summary>The signed-in student's username, or empty.</summary>
        public string DisplayName { get; private set; } = string.Empty;
        /// <summary>
        /// True while this computer is signed in as a player from before accounts existed (signed in automatically,
        /// without a username). Creating an account then keeps that player's progress.
        /// </summary>
        public bool HasGuestProgress { get; private set; }
        /// <summary>A note about the last account action that the account menu should show (e.g. "Your account was deleted.").</summary>
        public string AccountNotice { get; private set; } = string.Empty;

        public void ClearNotice()
        {
            AccountNotice = string.Empty;
        }

        bool connecting;
        bool applying;
        bool saving;
        bool pending;
        float nextFlushTime;
        string confirmedFingerprint = string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // Domain reload is disabled in this project, so statics must be cleared manually.
            Instance = null;
            Changed = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (Instance != null)
                return;
            var go = new GameObject("CloudProgress");
            DontDestroyOnLoad(go);
            go.AddComponent<CloudProgressService>();
            go.AddComponent<ProgressionUI>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void Start()
        {
            _ = ConnectAsync();
        }

        void OnEnable()
        {
            LevelProgressManager.LevelCleared += OnLevelCleared;
            LevelManager.LevelUnlocked += OnLevelUnlocked;
            ProgrammingChallengeManager.ScoreChanged += OnScoreChanged;
            ProgrammingChallengeManager.ChallengeCompleted += OnChallengeCompleted;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            LevelProgressManager.LevelCleared -= OnLevelCleared;
            LevelManager.LevelUnlocked -= OnLevelUnlocked;
            ProgrammingChallengeManager.ScoreChanged -= OnScoreChanged;
            ProgrammingChallengeManager.ChallengeCompleted -= OnChallengeCompleted;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            if (pending && !saving && !applying && State == CloudState.Ready && Time.unscaledTime >= nextFlushTime)
                _ = FlushAsync();
        }

        void OnApplicationPause(bool paused)
        {
            // A last chance to send changes; not guaranteed to finish, so the status stays Unsaved until confirmed.
            if (paused)
                RequestSave(true);
        }

        // ---------- Game events ----------

        void OnLevelCleared(ProgrammingLanguage language, LevelInfo cleared, LevelInfo next) => RequestSave(true);
        void OnLevelUnlocked(LevelInfo level) => RequestSave(false);
        void OnScoreChanged(int score, int hints) => RequestSave(false);
        void OnChallengeCompleted(string obstacleId, QuestionData question) => RequestSave(false);

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Returning to the main menu: send anything not yet confirmed (a no-op when nothing changed).
            if (scene.name == SceneNames.MainMenu)
                RequestSave(true);
        }

        // ---------- Connect and load ----------

        /// <summary>Starts (or retries) the initialise, sign-in and load sequence.</summary>
        public void RetryConnect()
        {
            if (State == CloudState.Failed)
                _ = ConnectAsync();
        }

        /// <summary>Retries a failed save.</summary>
        public void RetrySave()
        {
            if (State != CloudState.Ready || saving)
                return;
            pending = true;
            nextFlushTime = 0f;
        }

        Task ConnectAsync()
        {
            return RunConnectStepAsync(async () =>
            {
                if (Application.internetReachability == NetworkReachability.NotReachable)
                    throw new InvalidOperationException("No internet connection. Code Escape needs to be online to load and save progress.");

                SetState(CloudState.Starting);
                await InitializeServicesAsync();

                SetState(CloudState.SigningIn);
                IAuthenticationService auth = AuthenticationService.Instance;
                if (!auth.IsSignedIn)
                {
                    // Only a saved session is resumed: with no session token nobody is signed in here, and no new
                    // player is ever created without an account.
                    if (!auth.SessionTokenExists)
                    {
                        SetState(CloudState.ChoosingAccount);
                        return;
                    }
                    try
                    {
                        // With a session token this signs the same player in again (any sign-in method).
                        await WithTimeout(auth.SignInAnonymouslyAsync(), "Signing in");
                    }
                    catch (AuthenticationException e) when (e.ErrorCode == AuthenticationErrorCodes.InvalidSessionToken)
                    {
                        auth.ClearSessionToken();
                        SetState(CloudState.ChoosingAccount);
                        return;
                    }
                }

                await RefreshAccountAsync();
                if (HasGuestProgress)
                {
                    // Signed in as this computer's player from before accounts existed: the student chooses
                    // CREATE ACCOUNT (keeps that progress) or SIGN IN.
                    SetState(CloudState.ChoosingAccount);
                    return;
                }
                await LoadAsync();
            });
        }

        static async Task InitializeServicesAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Initialized)
                return;
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                await WithTimeout(UnityServices.InitializeAsync(), "Initialising Unity Gaming Services");
                return;
            }
            // A previous attempt timed out and is still initialising.
            float until = Time.realtimeSinceStartup + TimeoutMilliseconds / 1000f;
            while (UnityServices.State == ServicesInitializationState.Initializing && Time.realtimeSinceStartup < until)
                await Task.Delay(200);
            if (UnityServices.State != ServicesInitializationState.Initialized)
                throw new TimeoutException("Initialising Unity Gaming Services timed out.");
        }

        // Runs one connect sequence at a time; any failure shows the gate's error with Retry.
        async Task RunConnectStepAsync(Func<Task> steps)
        {
            if (connecting)
                return;
            connecting = true;
            ConnectionError = string.Empty;
            try
            {
                await steps();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CloudProgress] Connect failed: {e}");
                ConnectionError = Describe(e);
                SetState(CloudState.Failed);
            }
            finally
            {
                connecting = false;
            }
        }

        // Reads the username of the signed-in player (empty for a player from before accounts existed).
        async Task RefreshAccountAsync()
        {
            PlayerInfo info = await WithTimeout(AuthenticationService.Instance.GetPlayerInfoAsync(), "Reading your account");
            DisplayName = info != null && !string.IsNullOrEmpty(info.Username) ? info.Username : string.Empty;
            HasGuestProgress = DisplayName.Length == 0;
            Changed?.Invoke();
        }

        async Task LoadAsync()
        {
            SetState(CloudState.Loading);
            await SyncPlayerNameAsync();
            Dictionary<string, Unity.Services.CloudSave.Models.Item> items = await WithTimeout(
                CloudSaveService.Instance.Data.Player.LoadAsync(
                    new HashSet<string> { KeyProgression, KeySnapshot },
                    new PlayerData.LoadOptions(new PlayerData.DefaultReadAccessClassOptions())),
                "Loading progress");
            await HandleLoadedAsync(items);
        }

        // Sets the Unity player name to the username, so the Unity Dashboard's player list (Name column) shows who each
        // player is. Unity adds "#" and a number to every player name. Best effort: a failure never blocks play.
        async Task SyncPlayerNameAsync()
        {
            if (DisplayName.Length == 0)
                return;
            try
            {
                IAuthenticationService auth = AuthenticationService.Instance;
                string current = await WithTimeout(auth.GetPlayerNameAsync(autoGenerate: false), "Reading your player name");
                if (current == null || !current.StartsWith(DisplayName + "#", StringComparison.Ordinal))
                    await WithTimeout(auth.UpdatePlayerNameAsync(DisplayName), "Setting your player name");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CloudProgress] Could not set the player name: {e.Message}");
            }
        }

        // ---------- Accounts ----------
        // Every account action returns null when it worked, or a message to show the student. Passwords are used for
        // the request only: never stored, logged or kept in a field. There is no password reset in the game: the
        // developers can set a new password in the Unity Dashboard (Player Management).

        async Task<string> Guard(Func<Task> action, bool signingUp)
        {
            try
            {
                await InitializeServicesAsync();
                await action();
                return null;
            }
            catch (RequestFailedException e)
            {
                Debug.LogWarning($"[CloudProgress] Account action failed ({e.ErrorCode}): {e.Message}");
                return Friendly(e, signingUp);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CloudProgress] Account action failed: {e}");
                return Describe(e);
            }
        }

        // Unity's rules: 3-20 characters, letters, digits and . - @ _
        static string CheckUsername(string username)
        {
            string u = username ?? string.Empty;
            if (u.Length < 3 || u.Length > 20)
                return "Use 3 to 20 characters for the username.";
            foreach (char c in u)
            {
                bool allowed = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '.' || c == '-' || c == '@' || c == '_';
                if (!allowed)
                    return "The username can only use letters, numbers and . - @ _ (no spaces).";
            }
            return null;
        }

        // Unity's rules: 8-30 characters with an upper case letter, a lower case letter, a number and a symbol.
        static string CheckPassword(string password)
        {
            string p = password ?? string.Empty;
            if (p.Length < 8 || p.Length > 30)
                return "Use 8 to 30 characters for the password.";
            bool upper = false, lower = false, digit = false, symbol = false;
            foreach (char c in p)
            {
                if (char.IsUpper(c)) upper = true;
                else if (char.IsLower(c)) lower = true;
                else if (char.IsDigit(c)) digit = true;
                else symbol = true;
            }
            return upper && lower && digit && symbol ? null
                : "The password needs an upper case letter, a lower case letter, a number and a symbol (like ! or #).";
        }

        /// <summary>CREATE ACCOUNT: the student is signed in straight away.</summary>
        public async Task<string> RegisterAsync(string username, string password, string confirmPassword)
        {
            string problem = CheckUsername(username) ?? CheckPassword(password)
                             ?? (password != confirmPassword ? "The two passwords are different. Type the same password twice." : null);
            if (problem != null)
                return problem;
            string error = await Guard(async () =>
            {
                IAuthenticationService auth = AuthenticationService.Instance;
                // A player from before accounts existed keeps their progress: the username is added to that player.
                if (auth.IsSignedIn && HasGuestProgress)
                    await WithTimeout(auth.AddUsernamePasswordAsync(username, password), "Creating your account");
                else
                    await WithTimeout(auth.SignUpWithUsernamePasswordAsync(username, password), "Creating your account");
            }, signingUp: true);
            if (error != null)
                return error;
            return SignedIn(username);
        }

        public async Task<string> SignInAsync(string username, string password)
        {
            string problem = string.IsNullOrEmpty(username) ? "Type your username." : string.IsNullOrEmpty(password) ? "Type your password." : null;
            if (problem != null)
                return problem;
            string error = await Guard(async () =>
            {
                IAuthenticationService auth = AuthenticationService.Instance;
                // Leaves the automatic player from before accounts existed (its progress stays in its own save).
                if (auth.IsSignedIn)
                    auth.SignOut(true);
                await WithTimeout(auth.SignInWithUsernamePasswordAsync(username, password), "Signing in");
            }, signingUp: false);
            if (error != null)
                return error;
            return SignedIn(username);
        }

        string SignedIn(string username)
        {
            DisplayName = username;
            HasGuestProgress = false;
            AccountNotice = string.Empty;
            _ = RunConnectStepAsync(LoadAsync);
            return null;
        }

        /// <summary>
        /// SIGN OUT: only once everything is confirmed saved, so nothing is lost. The local copy of the player's
        /// progress is then cleared, so the next student on this computer starts from their own.
        /// </summary>
        public async Task<string> SignOutAsync()
        {
            if (State != CloudState.Ready || connecting || saving)
                return "Please wait a moment and try again.";
            connecting = true;
            try
            {
                await SaveAsync(forceInitial: false);
                if (SaveStatus != CloudSaveStatus.Synced)
                    return "Your latest progress isn't saved yet, so you are still signed in. Check the connection and try again.";
                FinishLeaving(deleteAccount: false);
                return null;
            }
            finally
            {
                connecting = false;
            }
        }

        /// <summary>DELETE MY ACCOUNT: removes the saved progress and then the account itself (privacy).</summary>
        public async Task<string> DeleteAccountAsync()
        {
            if (State != CloudState.Ready || connecting)
                return "Please wait a moment and try again.";
            connecting = true;
            try
            {
                string error = await Guard(async () =>
                {
                    await WithTimeout(CloudSaveService.Instance.Data.Player.DeleteAllAsync(), "Deleting your progress");
                    await WithTimeout(AuthenticationService.Instance.DeleteAccountAsync(), "Deleting your account");
                }, signingUp: false);
                if (error != null)
                    return error;
                FinishLeaving(deleteAccount: true);
                return null;
            }
            finally
            {
                connecting = false;
            }
        }

        void FinishLeaving(bool deleteAccount)
        {
            // Not Ready first, so resetting the local game cannot queue a save for nobody.
            SetState(CloudState.ChoosingAccount);
            IAuthenticationService auth = AuthenticationService.Instance;
            if (auth.IsSignedIn)
                auth.SignOut(true);
            else
                auth.ClearSessionToken();
            ResetLocalForNewPlayer();
            Record = null;
            DisplayName = string.Empty;
            HasGuestProgress = false;
            AccountNotice = deleteAccount ? "Your account and its saved progress were deleted." : string.Empty;
            confirmedFingerprint = string.Empty;
            pending = false;
            SetSaveStatus(CloudSaveStatus.Synced, string.Empty);
            Debug.Log(deleteAccount ? "[CloudProgress] Account deleted." : "[CloudProgress] Signed out.");
            SceneManager.LoadScene(SceneNames.MainMenu);
        }

        // The local save belongs to one player; another player starts from their own cloud copy (or from nothing).
        void ResetLocalForNewPlayer()
        {
            applying = true;
            try
            {
                if (ProgressSaveService.Instance != null)
                    ProgressSaveService.Instance.ResetProgress();
                else
                    SaveManager.DeleteAllLevelProgress();
                if (LevelProgressManager.Instance != null)
                    LevelProgressManager.Instance.ReloadFromSave();
                if (AchievementManager.Instance != null)
                    AchievementManager.Instance.ClearForNewPlayer();
            }
            finally
            {
                applying = false;
            }
            PlayerPrefs.DeleteKey(PrefUnsynced);
            PlayerPrefs.DeleteKey(PrefPlayerId);
            PlayerPrefs.Save();
        }

        async Task HandleLoadedAsync(Dictionary<string, Unity.Services.CloudSave.Models.Item> items)
        {
            string playerId = AuthenticationService.Instance.PlayerId;

            // The local save belongs to another player (an account switch on this computer): never carry it over.
            string localOwner = PlayerPrefs.GetString(PrefPlayerId, string.Empty);
            if (localOwner.Length > 0 && localOwner != playerId)
            {
                Debug.Log("[CloudProgress] A different player signed in - clearing the previous player's local progress.");
                ResetLocalForNewPlayer();
            }

            bool hasProgression = items.TryGetValue(KeyProgression, out var progressionItem);
            bool hasSnapshot = items.TryGetValue(KeySnapshot, out var snapshotItem);
            if (hasProgression != hasSnapshot)
                throw new InvalidOperationException("The saved progression in Cloud Save is incomplete. It was left untouched; ask the developer to check it.");

            if (!hasProgression)
            {
                // First sign-in for this account: no record exists, so create one from the current game state.
                Debug.Log("[CloudProgress] No progression in Cloud Save for this player - creating a new record.");
                Record = new PlayerProgressData { player_id = playerId };
                RestoredFromCloud = false;
                confirmedFingerprint = string.Empty;
                SetState(CloudState.Loading);
                await SaveAsync(forceInitial: true);
                if (SaveStatus != CloudSaveStatus.Synced)
                    throw new InvalidOperationException("Could not create your progression in Cloud Save: " + SaveError);
                PlayerPrefs.SetString(PrefPlayerId, playerId);
                SetState(CloudState.Ready);
                return;
            }

            PlayerProgressData record;
            ProgressSnapshot snapshot;
            try
            {
                record = JsonUtility.FromJson<PlayerProgressData>(progressionItem.Value.GetAs<string>());
                snapshot = JsonUtility.FromJson<ProgressSnapshot>(snapshotItem.Value.GetAs<string>());
            }
            catch (Exception e)
            {
                throw new InvalidOperationException("The saved progression could not be read and was left untouched: " + e.Message);
            }
            if (record == null || snapshot == null)
                throw new InvalidOperationException("The saved progression is empty or unreadable and was left untouched.");
            record.Sanitize();
            if (record.version > PlayerProgressData.CurrentVersion)
                throw new InvalidOperationException("The saved progression was written by a newer version of Code Escape. Update the game.");

            // Changes this same player identity made but could not confirm last time (quit / network error) are
            // newer than the cloud copy, so keep them and send them again instead of overwriting them.
            bool localIsNewer = PlayerPrefs.GetInt(PrefUnsynced, 0) == 1 && PlayerPrefs.GetString(PrefPlayerId, string.Empty) == playerId;

            Record = record;
            if (localIsNewer)
            {
                RestoredFromCloud = false;
                confirmedFingerprint = Fingerprint(record, snapshot);
                SetState(CloudState.Ready);
                pending = true;
                nextFlushTime = 0f;
                SetSaveStatus(CloudSaveStatus.Unsaved, string.Empty);
                return;
            }

            ApplyToGame(snapshot);
            RestoredFromCloud = true;
            Debug.Log($"[CloudProgress] Loaded progression from Cloud Save (score {record.total_score}, {record.completed_levels.Count} levels cleared, " +
                      $"highest unlocked '{record.highest_unlocked_level}', last played {record.last_played}).");
            confirmedFingerprint = Fingerprint(record, snapshot);
            PlayerPrefs.SetString(PrefPlayerId, playerId);
            PlayerPrefs.SetInt(PrefUnsynced, 0);
            SetSaveStatus(CloudSaveStatus.Synced, string.Empty);
            SetState(CloudState.Ready);

            // Menus read the save when they open; any other scene already built itself from the old state.
            if (SceneManager.GetActiveScene().name != SceneNames.MainMenu)
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // Replaces the local game state with the cloud one, through the existing save system.
        void ApplyToGame(ProgressSnapshot snapshot)
        {
            applying = true;
            try
            {
                ProgressSnapshotApplier.Apply(snapshot, Record);
            }
            finally
            {
                applying = false;
            }
        }

        // ---------- Save ----------

        /// <summary>Queues a save. Updates are batched: a burst of events becomes one request.</summary>
        public void RequestSave(bool immediate)
        {
            if (State != CloudState.Ready || applying)
                return;
            pending = true;
            float due = Time.unscaledTime + (immediate ? 0f : SaveDebounceSeconds);
            nextFlushTime = immediate ? due : Mathf.Max(nextFlushTime, due);
            if (SaveStatus == CloudSaveStatus.Synced)
            {
                // Only claim "unsaved" if the game state really differs from what the cloud confirmed.
                BuildRecord(out PlayerProgressData record, out ProgressSnapshot snapshot);
                if (Fingerprint(record, snapshot) == confirmedFingerprint)
                {
                    pending = false;
                    return;
                }
                MarkUnsynced();
                SetSaveStatus(CloudSaveStatus.Unsaved, string.Empty);
            }
        }

        async Task FlushAsync()
        {
            pending = false;
            await SaveAsync(forceInitial: false);
        }

        async Task SaveAsync(bool forceInitial)
        {
            if (saving)
                return;
            saving = true;
            try
            {
                BuildRecord(out PlayerProgressData record, out ProgressSnapshot snapshot);
                string fingerprint = Fingerprint(record, snapshot);
                if (!forceInitial && fingerprint == confirmedFingerprint)
                {
                    SetSaveStatus(CloudSaveStatus.Synced, string.Empty);
                    return;
                }

                record.last_played = DateTime.UtcNow.ToString("o");
                string recordJson = JsonUtility.ToJson(record);
                string snapshotJson = JsonUtility.ToJson(snapshot);

                MarkUnsynced();
                SetSaveStatus(CloudSaveStatus.Saving, string.Empty);
                await WithTimeout(CloudSaveService.Instance.Data.Player.SaveAsync(
                    new Dictionary<string, object>
                    {
                        { KeyProgression, recordJson },
                        { KeySnapshot, snapshotJson }
                    },
                    new PlayerData.SaveOptions(new PlayerData.DefaultWriteAccessClassOptions())), "Saving progress");

                // Only now has Cloud Save confirmed the data.
                Record = record;
                confirmedFingerprint = fingerprint;
                LastConfirmedSaveUtc = DateTime.UtcNow;
                PlayerPrefs.SetInt(PrefUnsynced, 0);
                PlayerPrefs.SetString(PrefPlayerId, record.player_id);
                SetSaveStatus(CloudSaveStatus.Synced, string.Empty);
                Debug.Log($"[CloudProgress] Saved to Cloud Save (score {record.total_score}, {record.completed_levels.Count} levels cleared).");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CloudProgress] Save failed: {e}");
                SetSaveStatus(CloudSaveStatus.Failed, Describe(e));
            }
            finally
            {
                saving = false;
            }
        }

        void MarkUnsynced()
        {
            PlayerPrefs.SetInt(PrefUnsynced, 1);
            PlayerPrefs.Save();
        }

        // ---------- Building the record ----------

        // Reads the current game state into a record and snapshot (the rules live in ProgressRecordBuilder).
        void BuildRecord(out PlayerProgressData record, out ProgressSnapshot snapshot)
        {
            ProgressRecordBuilder.Build(Record, AuthenticationService.Instance.PlayerId, out record, out snapshot, out string newestAchievement);
            LastUnlockedAchievement = newestAchievement;
        }

        // Identifies the saved content; last_played is excluded so an unchanged game state is never re-sent.
        static string Fingerprint(PlayerProgressData record, ProgressSnapshot snapshot)
        {
            PlayerProgressData copy = record.Clone();
            copy.last_played = string.Empty;
            return JsonUtility.ToJson(copy) + "|" + JsonUtility.ToJson(snapshot);
        }

        // ---------- Helpers ----------

        void SetState(CloudState state)
        {
            State = state;
            Changed?.Invoke();
        }

        void SetSaveStatus(CloudSaveStatus status, string error)
        {
            SaveStatus = status;
            SaveError = error ?? string.Empty;
            Changed?.Invoke();
        }

        // A failed account request as a message a student can act on. The server's reason is also matched by text,
        // because Unity reports some of them (wrong password, taken username) only in the message.
        static string Friendly(RequestFailedException e, bool signingUp)
        {
            string message = e.Message ?? string.Empty;
            int code = e.ErrorCode;
            if (Contains(message, "WRONG_USERNAME_PASSWORD") || (!signingUp && (code == AuthenticationErrorCodes.InvalidParameters || code == CommonErrorCodes.InvalidToken)))
                return "Wrong username or password.";
            if (Contains(message, "USERNAME_ALREADY") || Contains(message, "ENTITY_EXISTS") || code == AuthenticationErrorCodes.AccountAlreadyLinked || code == CommonErrorCodes.Conflict)
                return signingUp ? "That username is taken. Choose another one, or SIGN IN if it is yours." : "That account is already in use.";
            if (code == AuthenticationErrorCodes.InvalidParameters)
                return "Unity did not accept that username or password. Check the rules above and try again.";
            if (code == CommonErrorCodes.TooManyRequests)
                return "Too many tries. Wait a minute and try again.";
            if (code == CommonErrorCodes.TransportError || code == CommonErrorCodes.Timeout || code == CommonErrorCodes.ServiceUnavailable)
                return "Cannot reach the server. Check your internet connection and try again.";
            if (code == AuthenticationErrorCodes.BannedUser)
                return "This account is blocked. Ask your teacher or the developers.";
            if (code == CommonErrorCodes.Forbidden || code == CommonErrorCodes.ProjectPolicyAccessDenied)
                return "Username and password sign-in is not switched on for this game. Ask the developers to enable it in the Unity Dashboard.";
            return "Something went wrong (code " + code + "). Try again.";
        }

        static bool Contains(string text, string part)
        {
            return text.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static string Describe(Exception e)
        {
            if (e is InvalidOperationException || e is TimeoutException)
                return e.Message;
            if (Application.internetReachability == NetworkReachability.NotReachable)
                return "No internet connection. Check your network and retry.";
            if (e is RequestFailedException failed)
                return $"Unity Gaming Services error {failed.ErrorCode}: {failed.Message}";
            return $"{e.GetType().Name}: {e.Message}";
        }

        static async Task WithTimeout(Task task, string what)
        {
            Task finished = await Task.WhenAny(task, Task.Delay(TimeoutMilliseconds));
            if (finished != task)
                throw new TimeoutException($"{what} timed out. Check your connection and retry.");
            await task;
        }

        static async Task<T> WithTimeout<T>(Task<T> task, string what)
        {
            Task finished = await Task.WhenAny(task, Task.Delay(TimeoutMilliseconds));
            if (finished != task)
                throw new TimeoutException($"{what} timed out. Check your connection and retry.");
            return await task;
        }
    }

    /// <summary>The achievements this prototype awards, derived from progression (not from gameplay triggers).</summary>
    public static class Achievements
    {
        public const string FirstChallenge = "first_challenge";
        public const string FirstLevel = "first_level";
        public const string FiveChallenges = "five_challenges";
        public const string Halfway = "halfway";
        public const string CaseClosed = "case_closed";

        public static string NameOf(string id)
        {
            ProgrammingChallengeManager challenges = ProgrammingChallengeManager.Instance;
            return AchievementCatalog.NameOf(id, challenges != null ? challenges.Database : null);
        }
    }
}
