# Code Escape

Code Escape is a 2D PC educational game. The player (Alex) moves through six locations, and each locked obstacle or hazard opens a beginner programming question (Python, PHP or SQL). Solving questions unlocks the way forward, earns points and clears levels on a level map.

> Capstone project. Course and institution details: **[to be completed by the team]**

## Group members
Group number: **IT22**

- Jean Joseph C. Mallari
- Mark Daniel M. Haguisan
- Louis Angelo D. Junio
- Alynus Job C. Tombado

## Tools and technologies
- Unity 6000.5.10f1 (C#, Universal Render Pipeline, Input System, uGUI)
- Unity Gaming Services: Authentication (anonymous sign-in) and Cloud Save, via `com.unity.services.cloudsave` 3.4.1
- Unity PlayerPrefs for the local save
- Git and GitHub (GitHub Desktop)
## Features
- **Three languages:** Python, PHP and SQL. One is chosen per playthrough, with its own level-map progress.
- **Six levels:** Home, Maple Street, Warehouse 7, Riverside Tech Facility, Old Station and the Sentinel Control Room, played from a level map. Cleared levels can be replayed.
- **Questions:** 48 predefined questions (10 obstacle questions per language, plus 18 hazard-challenge questions). Types include multiple choice, predict the output, fill the missing code, fix the incorrect code and choose the correct code. Answers are compared with stored solutions; player code is never executed.
- **Hints and scoring:** +100 points per solved challenge, -25 for the first hint on a question, with a rank on the results screen.
- **Story systems:** cutscenes, dialogue, objectives, collectible clues with a journal, settings, and an SDG 4 information panel.
- **Progress saving:** a local save (PlayerPrefs) plus a **Unity Cloud Save** copy that loads on start. See [Documentation/CloudSave.md](Documentation/CloudSave.md).

## Controls
| Mode | Keys |
|---|---|
| Side view | A / D or Left / Right to walk, W / Up / Space to jump |
| Top down | WASD or arrow keys to move in eight directions |

Menus can be used with the mouse or keyboard (Esc closes panels).

## Requirements
- **Unity 6000.5.10f1** (install through Unity Hub)
- Windows PC
- An **internet connection** to play, because progression is loaded from and saved to Unity Cloud Save before gameplay starts
- A Unity account with this project linked to a Unity Dashboard project (Cloud Save enabled): see the setup section in [Documentation/CloudSave.md](Documentation/CloudSave.md)

## Run the project
1. Clone the repository and add the folder in Unity Hub (use the exact editor version above).
2. Open the project. The first import can take a while.
3. Open `Assets/Scenes/00_MainMenu.unity` and press **Play**.
4. Wait for the "Connecting to Unity Cloud Save" screen to finish, then choose **Play Adventure**.

To build, use File > Build Profiles (all 12 scenes in `Assets/Scenes` are already in Build Settings).

## Project structure
| Path | Contents |
|---|---|
| `Assets/Scenes` | Main menu, language select, level map, intro, six level scenes, ending, results |
| `Assets/Scripts/Programming` | Challenge manager, question data and database, language selection |
| `Assets/Scripts/Obstacles` | Programming obstacles, catch hazards and their movement |
| `Assets/Scripts/Player` | Player controller and cameras |
| `Assets/Scripts/Game` | Game state, level and level-map progress, scene transitions, results, pause |
| `Assets/Scripts/SaveSystem` | Local save (SaveManager, ProgressSaveService, save data classes) |
| `Assets/Scripts/Cloud` | Unity Cloud Save integration and its on-screen status UI |
| `Assets/Scripts/UI`, `Story`, `Clues`, `Dialogue`, `Cutscenes`, `Audio` | Menus and HUD, story flow, clues, dialogue, cutscenes, audio |
| `Assets/ScriptableObjects/Questions` | The question assets (Python, PHP, SQL, hazards) |
| `Documentation` | Cloud Save notes and the capstone technical alignment report |

## Database and data
- **Storage used:** Unity Cloud Save (player data, key-value) through Unity Authentication anonymous sign-in, plus a local save in PlayerPrefs. No credentials are stored in the project; the Unity project is linked to a Unity Dashboard project.
- **Saved:** current level, highest unlocked level, completed levels, total score, achievements, last played time, and a snapshot used to restore the level map. Saved automatically when a level is cleared or unlocked, when score changes and when the main menu opens.
- **Retrieved and displayed:** loaded when the game starts, then shown on the level map, the Continue panel and the "Cloud progress" panel.
- **Details, field list and testing:** [Documentation/CloudSave.md](Documentation/CloudSave.md)

## Cloud Save setup for a new machine
No passwords, keys or service account files are needed or stored in this repository. After cloning:
1. Open the project in Unity 6000.5.10f1 and sign in with your Unity account.
2. Make sure the project is linked to a Unity Dashboard project (Edit > Project Settings > Services) with Cloud Save enabled. The link is kept in `ProjectSettings`.
3. Press Play. The game signs in anonymously and loads or creates the player's progress.

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

### Tests I ran (Unity 6000.5.10f1, Play Mode in the Editor, this project, real Unity cloud project)
| # | Check | Result |
|---|---|---|
| 1 | UGS initialises and anonymous sign-in works with no login UI | Passed: the first Play session reached Cloud Save and obtained a Player ID |
| 2 | New progression is created when no cloud record exists | Passed: log "creating a new record", then a confirmed save |
| 3 | Existing cloud data loads on the next launch | Passed: second and later sessions logged "Loaded progression from Cloud Save" with the saved values |
| 4 | Clearing a level updates and saves progression | Passed (simulated by calling `LevelProgressManager.RecordLevelCleared("03_Home", "04_Neighborhood")`): record showed `completed_levels=[home]`, `current_level=neighborhood`, `highest_unlocked_level=neighborhood`, achievement `first_level`, status Synced |
| 5 | Repeating the same completion adds no duplicate or extra save | Passed: still one completed level, status stayed Synced, no new save logged |
| 6 | After restart, the level map reflects cloud data | Passed (state read through `LevelProgressManager.GetState`): Home Completed, Neighborhood Available, Warehouse Locked, scene reloaded to the level map |
| 7 | Project compiles with the packages and changes | Passed: no C# compile errors in the Editor log |

My test data was deleted from Cloud Save afterwards, and the local playthrough and selected language were reset (so you may need to pick a language again). The simulated level clear used the Editor's anonymous test player only.


## Known limitations
- Anonymous identity only; no recovery or cross-device sync (see above).
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




