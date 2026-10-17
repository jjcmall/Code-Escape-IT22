# Code Escape: Cloud Save progression

Technical notes for the Unity Cloud Save integration. For the project overview see the main [README](../README.md).

- **Unity version:** 6000.5.10f1
- **Cloud packages (added for the cloud save):** `com.unity.services.cloudsave` 3.4.1 (installs `com.unity.services.authentication` 3.7.4 and `com.unity.services.core` 1.18.0 as dependencies). Versions are from `Packages/packages-lock.json`.

## Cloud progression (Unity Cloud Save)

Progression is stored in **Unity Cloud Save** (player-specific data). No other cloud service or database is used. The game opens without any login screen: it signs the player in **anonymously** through Unity Authentication, loads the saved progression, and blocks gameplay until that succeeds. This prototype is **online-only**.

### What it is, and is not
- Cloud Save player data is a per-player **key-value store**. It is not a SQL database: there are no tables, queries or joins.
- Each player only reads and writes their own data (default access class, written by the signed-in player). There is no shared key.
- Progression is written by the game client, so **it is not cheat-proof**. A modified client could send any score or level. A trustworthy score would need server-side logic (for example Cloud Code), which is not part of this prototype.

### Data stored
Two Cloud Save keys per player:

| Key | Content |
|---|---|
| `code_escape_progression` | `PlayerProgressData` JSON (below) |
| `code_escape_snapshot` | The existing local save files as JSON (selected language, the playthrough `ProgressData`, and each language's `LevelProgressData`), so the level map, checkpoints and Continue work unchanged after a restore |

`PlayerProgressData` (`Assets/Scripts/Cloud/PlayerProgressData.cs`):

| Field | Meaning |
|---|---|
| `player_id` | Authenticated Unity Player ID |
| `current_level` | Level ID of the level currently reached |
| `highest_unlocked_level` | Level ID of the furthest level unlocked in any language |
| `completed_levels` | Unique level IDs cleared (no duplicates); mirrors the current save |
| `total_score` | Lifetime score: +100 per distinct solved challenge, -25 per distinct hinted question |
| `achievements` | Unlocked achievement IDs (see below) |
| `last_played` | UTC time of the latest progression update (ISO 8601) |
| `scored_challenges`, `penalised_hints` | Bookkeeping that makes the score idempotent (each entry counted once) |

Achievements are simple rules added for this prototype. They are derived from progression: `first_challenge`, `first_level`, `five_challenges`, `halfway` (half the levels cleared in one language), `case_closed` (all levels cleared in one language). Change them in `CloudProgressService.UnlockAchievements`.

### How loading works (on every start)
1. `CloudProgressService` (created automatically after the first scene loads) initialises Unity Gaming Services.
2. It signs in anonymously (`AuthenticationService.SignInAnonymouslyAsync`) if not already signed in.
3. It loads both keys. If **neither** exists, this player has no progression yet, so a new record is created from the current game state and saved; **only after Cloud Save confirms it does the game unlock**. If only one key exists, or the data cannot be read, the load fails and **nothing is overwritten**.
4. If a record exists, it is written back through the existing save system (`ProgressSaveService`, `SaveManager`, `LevelProgressManager.ReloadFromSave`, `LanguageManager.RestoreLanguage`), so the level map and Continue show the cloud data.
5. If anything fails (no internet, services not linked, timeout after 20 s), a full-screen message with a **Retry** button is shown and gameplay stays locked. Progress is never silently reset.

One exception, to avoid losing work: if this same Player ID had changes that could not be confirmed last time (the game quit or the network failed), those local changes are newer than the cloud copy and are sent again instead of being overwritten.

### How saving works
Saves run asynchronously and never block gameplay. Triggers: a level cleared (immediately), a level unlocked, score changes and solved challenges (batched, 3 s debounce), the main menu loading, and the app being paused. Before any request the new record is compared with the last confirmed one; unchanged state sends nothing. Repeated events cannot create duplicate completions or score, because completions are a set and score is computed from distinct solved/hinted IDs.

The status chip (top right) shows **Unsaved changes**, **Saving...**, **Saved to cloud** (only after confirmation) or **Cloud save failed** with a **Retry** button. The **Cloud progress** panel (bottom left on the main menu and level map, collapsible) shows the data last loaded from, or confirmed by, Cloud Save: Player ID (shortened), current level, highest unlocked level, completed levels, total score, achievements and last update. The existing level map also reflects the restored unlocks and completions.

### Persistence and identity: what to expect
- **Game restart on the same device:** the anonymous session is cached by Unity Authentication, so the same Player ID signs in again and the progression loads.
- **Different device, cleared local data, or a new Player ID:** anonymous authentication is **not a recoverable account**. A new device, cleared Unity Authentication cache (for example clearing PlayerPrefs / app data, or the editor's session token) or a new profile creates a **new** Player ID with **no progression**, and the old data stays in the cloud but is unreachable by the game. Do not rely on anonymous sign-in to move progress between devices. Real account linking (for example Unity Player Accounts or a platform identity) would be a separate, future feature and is intentionally not implemented here.
- A progress reset in the game clears levels and the playthrough in the cloud copy, but `total_score` and `achievements` are lifetime totals and are kept.

## Unity Dashboard setup
Follow the official documentation for current menu names (they change between Unity versions); this is the sequence:
1. Open the Unity Dashboard (cloud.unity.com), sign in, and create or select a project (inside your organization).
2. In the Editor, open **Edit > Project Settings > Services**, sign in, and link this project to that Dashboard project. This writes the cloud project ID into `ProjectSettings/ProjectSettings.asset`. (This repository's settings already contain a `cloudProjectId`, so a link existed before this change; check it is the project you intend.)
3. Install the packages in Package Manager (Unity Registry): **Cloud Save** (`com.unity.services.cloudsave`); Authentication and Core come with it. They are already listed in `Packages/manifest.json`.
4. In the Dashboard, make sure **Cloud Save** is enabled for the project and environment, and that Authentication allows anonymous sign-in (it is the default identity provider). 
5. Choose the environment in **Project Settings > Services > Environments** (the default `production` is used if you set nothing). The game calls `UnityServices.InitializeAsync()` with no options, so it uses that environment.
6. To check data: after playing, open Dashboard > Cloud Save > Player Data and look up the Player ID shown in the game's cloud panel. The two keys above should appear.

## Testing

### Tests performed (Unity 6000.5.10f1, Play Mode in the Editor)
| # | Check | Result |
|---|---|---|
| 1 | UGS initialises and anonymous sign-in works with no login UI | Passed: the first Play session reached Cloud Save and obtained a Player ID |
| 2 | New progression is created when no cloud record exists | Passed: log "creating a new record", then a confirmed save |
| 3 | Existing cloud data loads on the next launch | Passed: second and later sessions logged "Loaded progression from Cloud Save" with the saved values |
| 4 | Clearing a level updates and saves progression | Passed (simulated by calling `LevelProgressManager.RecordLevelCleared("03_Home", "04_Neighborhood")`): record showed `completed_levels=[home]`, `current_level=neighborhood`, `highest_unlocked_level=neighborhood`, achievement `first_level`, status Synced |
| 5 | Repeating the same completion adds no duplicate or extra save | Passed: still one completed level, status stayed Synced, no new save logged |
| 6 | After restart, the level map reflects cloud data | Passed (state read through `LevelProgressManager.GetState`): Home Completed, Neighborhood Available, Warehouse Locked, scene reloaded to the level map |
| 7 | Project compiles with the packages and changes | Passed: no C# compile errors in the Editor log |


## Known limitations
- Anonymous identity only; no recovery or cross-device sync (see above).
- Not cheat-proof; client-authoritative.
- Online-only. Unsaved changes are kept in the existing local save (PlayerPrefs) and retried, but they are never reported as saved.
- Total score is computed from distinct solved and hinted IDs, so replaying a level does not add points, and a hint already penalised once is not penalised again on a replay.
- Achievements are simple derived rules added for this prototype.

## Files

Created:
- `Assets/Scripts/Cloud/PlayerProgressData.cs`: record and snapshot data classes
- `Assets/Scripts/Cloud/CloudProgressService.cs`: initialisation, anonymous sign-in, load, restore, save, retry, achievements
- `Assets/Scripts/Cloud/ProgressionUI.cs`: online gate, save status chip, progression panel (built in code)
- `README.md`

Modified:
- `Packages/manifest.json`: added `com.unity.services.cloudsave` 3.4.1 (`packages-lock.json` updated by Unity)
- `Assets/Scripts/Programming/LanguageManager.cs`: added `RestoreLanguage` (restores the language without starting a new playthrough)
- `Assets/Scripts/Game/LevelProgressManager.cs`: added `ReloadFromSave` (re-reads level progress after a restore)

No scene, prefab or gameplay script was changed.

## Documentation references
- Unity Cloud Save: https://docs.unity.com/ugs/en-us/manual/cloud-save/manual
- Unity Authentication (anonymous sign-in): https://docs.unity.com/ugs/en-us/manual/authentication/manual/overview
- Unity Gaming Services overview and Dashboard: https://docs.unity.com/ugs/en-us/manual/overview/manual/unity-gaming-services-home

(Links are given as starting points; Unity reorganises its documentation, so search the title if one has moved.)
