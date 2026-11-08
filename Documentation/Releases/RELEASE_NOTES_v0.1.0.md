# Code Escape, release notes

**Project:** Code Escape (Group IT22)
**Version:** 0.1.0
**Release date:** 8 November 2026
**Release type:** Testing build (first controlled project build)
**Source version:** branch `release-v0.1.0`, commit `f8b27a8`, tag `v0.1.0`
**Built with:** Unity 6000.5.10f1, Windows 64-bit standalone, built in batch mode from a clean checkout of the commit above

## Included in this version
- Educational Python puzzle game: the player (Alex) walks through six locations and opens locked doors and hazards by solving beginner Python challenges.
- Level map with six levels (Home, Maple Street, Warehouse 7, Riverside Tech Facility, Old Station, Sentinel Control Room); cleared levels can be replayed.
- 32 Python challenges checked by running the player's code in a sandboxed interpreter (no file, network or import access).
- Learning cards before each challenge, feedback on wrong answers, a hint ladder (free hint after 3 wrong answers, show-answer after 5).
- Scoring (+100 per solved challenge, -25 for the first hint on a question), achievements, level stars and a results screen.
- Story systems: cutscenes, dialogue, objectives, clue journal, and the prologue comic in the intro.
- Student accounts in the game (username and password) with progress saved to Unity Cloud Save and a local save.
- One shared look for the menus, HUD and dialogs.

## Fixed
- No defect fixes in this version.
- Internal change, no change in behavior: record building and snapshot restoring moved out of `CloudProgressService` into `ProgressRecordBuilder` and `ProgressSnapshotApplier`.

## Known issues
- An internet connection and an account are needed to play, because progress is loaded from and saved to Unity Cloud Save before gameplay starts.
- There is no password reset in the game; the project owner sets a new password in the Unity Dashboard.
- The build is not code-signed, so Windows SmartScreen may warn the first time it runs (choose More info, then Run anyway).
- Pull Request #1 (`refactor/save-progress`) has not been reviewed or merged yet, so this release was built from a release branch that carries those commits.
- Not yet tested on a second computer by another member.

## Not yet included
- PHP and SQL challenges (the content stays in the project for future versions).
- Builds for platforms other than Windows.

## Installation / running instructions
1. Download `CodeEscape-v0.1.0.zip` and extract it to a normal folder (for example `Documents\CodeEscape-v0.1.0`). Do not run it from inside the zip.
2. Run `CodeEscape.exe`.
3. Create an account on the welcome screen: a username of 3 to 20 characters (letters, numbers and `. - @ _`) and a password of 8 to 30 characters with an upper case letter, a lower case letter, a number and a symbol. Or sign in if you already have one.
4. In the main menu choose Select Level, then Play. Walk to a door and press E to open its challenge.
5. Controls: A / D or the arrow keys to walk, W / Up / Space to jump (side view), WASD or arrows in top-down areas.
6. To check the version, read `VERSION.txt` in the extracted folder (it names the version, the source commit and the tag).

## Source traceability
Released build, then version 0.1.0, then tag `v0.1.0`, then commit `f8b27a8` on branch `release-v0.1.0` in https://github.com/jjcmall/Code-Escape-IT22
