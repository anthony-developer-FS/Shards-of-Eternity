# Opening the Unity prototype

The Unity project lives at the root of this repository (`Assets/`, `Packages/`, `ProjectSettings/`). The browser prototype in `prototype/` sits beside it and Unity ignores it.

## 1. Install the tools (once)

1. Install [Unity Hub](https://unity.com/download).
2. In Unity Hub, go to **Installs > Install Editor** and install **Unity 6.6** (`6000.6.7f1`, the version the project is on). Add these modules when asked:
   - **Microsoft Visual Studio Community** (Windows) or use JetBrains Rider / VS Code, for editing scripts.
   - **Android Build Support** and/or **iOS Build Support**, only when you want to test on a phone.
3. Install [GitHub Desktop](https://desktop.github.com/) if you don't use git from the command line.

## 2. Get the code

1. In GitHub Desktop, choose **File > Clone repository** and pick `anthony-developer-FS/Shards-of-Eternity`.
2. Switch to the branch with the Unity project (or `main`, once the pull request is merged).

## 3. Open it in Unity

1. In Unity Hub, go to **Projects > Add > Add project from disk** and select the repository folder (the one that contains `Assets`).
2. If Hub says the project was made with a different editor version, install `6000.6.7f1` or confirm the upgrade.
3. The first open takes a few minutes while Unity imports packages and builds its `Library` cache.
4. When the editor finishes loading, it creates and opens `Assets/ShardsOfEternity/Scenes/Prototype.unity` on its own. If it doesn't, use the menu **Shards of Eternity > Open Prototype Scene**.
5. Press **Play**.

## 4. Play the combat prototype

A run is a descent through several floors: a **dungeon** has 3 or 4, a **raid** has 12. Each floor is a generated room. When every enemy is dead the rift door turns green; walk any hero onto it and the next floor loads. Health, deaths and ultimate charge carry over between floors. The last floor holds the **Rift Lord**, a boss who hits every hero next to him, and killing him wins the run.

Once a room wakes, **reinforcements** arrive every 2 minutes of real time. The countdown is at the top right; **Pause** (or P) stops it. Rift pressure still rises with the turn timeline, but now only shifts the walls.

**Loot.** Slain enemies can drop gear onto their tile, marked by a beam of light in the item's rarity color: white for Common, blue for Magic, yellow for Rare and orange for Legendary. Better items get taller beams, and Rare and Legendary ones light up the floor around them. Walk a hero over the tile to pick the items up. Anything still on the floor when you take the exit is left behind. The boss always drops two Rare or better items. Deeper floors drop better rarities and bigger stats.

**Gear.** Heroes have 13 slots: head, shoulders, amulet, chest, wrists, hands, waist, legs, feet, two rings, main hand and off hand. Gear adds Damage, Armor, Health, Attack Speed, crit chance and crit damage, and Legendary boots add Move. After each floor you make **camp**: pick a hero, compare stash items against what they wear (green is better, red is worse), equip, then descend.

**Action shards** are an experimental option on the squad screen. Each hero gets 2 shards per turn and every move, attack or ability costs 1, so you can move twice, attack twice, or do one of each. With **Bank unspent shards** on, shards you don't spend carry over, up to 4.

| Action | Mouse and keyboard | Touch |
| --- | --- | --- |
| Preview | Hover a tile or enemy | Tap once |
| Move | Click a blue tile (orange is a Barbarian charge, teal is a Shaman swap) | Tap |
| Attack | Click a red enemy | Tap twice |
| Ability | Press 1 to 4 or click its button, then click a purple tile | Tap the button, then tap a target twice |
| End turn | E | End turn button |
| Pause | P | Pause button |
| Cancel | Esc | Tap the ability again |

The side panel shows the current hero, abilities, the exact outcome before you commit, rift pressure and the log. The bar at the top is the turn timeline. **New run** starts over; **Change squad / rules** goes back to the squad screen, where you pick 4 of the 6 classes, dungeon or raid, and the action shard options.

You can also select the **Shards of Eternity** object in the scene and set the squad, run type, a fixed seed, the shard rules or the reinforcement interval from the Inspector.

## 5. Run the tests

Open **Window > General > Test Runner**, choose **EditMode**, and press **Run All**. There are 42 tests covering body blocking, turn order, damage, abilities, enemy turns, room generation, floors and the boss, action shards, loot and gear, and simulated battles and whole runs.

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
