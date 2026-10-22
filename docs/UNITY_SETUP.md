# Opening the Unity prototype

The Unity project lives at the root of this repository (`Assets/`, `Packages/`, `ProjectSettings/`). The browser prototype in `prototype/` sits beside it and Unity ignores it.

## 1. Install the tools (once)

1. Install [Unity Hub](https://unity.com/download).
2. In Unity Hub, go to **Installs > Install Editor** and install **Unity 6 LTS** (any `6000.0.x` version). Add these modules when asked:
   - **Microsoft Visual Studio Community** (Windows) or use JetBrains Rider / VS Code, for editing scripts.
   - **Android Build Support** and/or **iOS Build Support**, only when you want to test on a phone.
3. Install [GitHub Desktop](https://desktop.github.com/) if you don't use git from the command line.

## 2. Get the code

1. In GitHub Desktop, choose **File > Clone repository** and pick `anthony-developer-FS/Shards-of-Eternity`.
2. Switch to the branch with the Unity project (or `main`, once the pull request is merged).

## 3. Open it in Unity

1. In Unity Hub, go to **Projects > Add > Add project from disk** and select the repository folder (the one that contains `Assets`).
2. If Hub says the project was made with a different editor version, pick your installed Unity 6 version and confirm. That is expected; the project only pins `6000.0.23f1` as a starting point.
3. The first open takes a few minutes while Unity imports packages and builds its `Library` cache.
4. When the editor finishes loading, it creates and opens `Assets/ShardsOfEternity/Scenes/Prototype.unity` on its own. If it doesn't, use the menu **Shards of Eternity > Open Prototype Scene**.
5. Press **Play**.

## 4. Play the combat prototype

It plays the same room and rules as the browser prototype, in 3D with placeholder shapes.

| Action | Mouse and keyboard | Touch |
| --- | --- | --- |
| Preview | Hover a tile or enemy | Tap once |
| Move | Click a blue tile (orange is a Barbarian charge, teal is a Shaman swap) | Tap |
| Attack | Click a red enemy | Tap twice |
| Ability | Press 1 to 4 or click its button, then click a purple tile | Tap the button, then tap a target twice |
| End turn | E | End turn button |
| Cancel | Esc | Tap the ability again |

The side panel shows the current hero, abilities, the exact outcome before you commit, rift pressure and the log. The bar at the top is the turn timeline. **New random room** builds a procedurally generated room (Ashen Crypt or Hellforge theme); **Change squad** lets you pick a different 4 of the 6 classes.

You can also select the **Shards of Eternity** object in the scene and set the squad, a fixed seed, or start in a generated room from the Inspector.

## 5. Run the tests

Open **Window > General > Test Runner**, choose **EditMode**, and press **Run All**. There are 24 tests covering body blocking, turn order, damage, abilities, enemy turns, room generation and 30 full simulated battles.

## 6. Commit the files Unity generates

The first time Unity opens the project it writes files that belong in git. After your first open, commit these:

- `ProjectSettings/` (all of it)
- `Packages/packages-lock.json`
- `Assets/ShardsOfEternity/Scenes/` and its `.meta` files

Never commit `Library/`, `Temp/`, `Logs/` or `UserSettings/`. The `.gitignore` already excludes them.

## Troubleshooting

- **"You are trying to read Input using the UnityEngine.Input class, but you have switched active Input handling to Input System package"**: go to **Edit > Project Settings > Player > Other Settings > Active Input Handling** and choose **Both**. The prototype uses the classic input API for now.
- **Everything is pink**: the project has been switched to URP without converting materials. The prototype creates its materials in code and picks the URP shader when URP is active, so re-entering Play mode usually fixes it. If not, check that a URP asset is assigned in **Project Settings > Graphics**.
- **Testing on a phone**: the quickest route is the **Unity Remote 5** app with **Edit > Project Settings > Editor > Device** set to your phone. For a real build, switch the platform in **File > Build Profiles** to Android or iOS.

## How the code is organized

| Folder | What it is |
| --- | --- |
| `Assets/ShardsOfEternity/Scripts/Core` | The game rules in plain C# with no Unity dependency: board, pathing, body blocking, turn timeline, damage, all 24 abilities, enemy AI, rift pressure and the room generator. |
| `Assets/ShardsOfEternity/Scripts/Game` | The Unity side: `GameBootstrap` builds the scene, `BoardView` draws and animates, `BattleController` runs the turn loop and input, `BattleHud` is a temporary on-screen UI. |
| `Assets/ShardsOfEternity/Editor` | Creates the prototype scene and the menu item. |
| `Assets/ShardsOfEternity/Tests/EditMode` | Unit tests for the rules. |

The rules resolve instantly and report what happened as a list of events (moved, damaged, knocked back, and so on). The view plays those events back as animations. That split keeps the rules testable and lets the same rules drive a PC, mobile or VR presentation later.
