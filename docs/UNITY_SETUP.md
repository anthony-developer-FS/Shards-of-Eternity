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
5. Press **Play**. The game opens on the title screen: choose **New run**, pick 4 heroes and the run options, then **Enter the rift**.

## 4. Play the combat prototype

A run is a descent through several floors: a **dungeon** has 3 or 4, a **raid** has 12. Each floor is a generated room. When every enemy is dead the rift door turns green; walk any hero onto it and the next floor loads. Health, deaths and ultimate charge carry over between floors. The last floor holds the **Rift Lord**, a boss who hits every hero next to him, and killing him wins the run.

Once a room wakes, **reinforcements** arrive on a real-time timer: the first wave after 10 minutes, the second 5 minutes later, then one every 2 minutes. The timer restarts at 10 minutes on each new floor. For quick testing, turn on **Fast 2-min timer** on the squad screen. The countdown is at the top right; **Pause** (or P) stops it. Rift pressure still rises with the turn timeline, but now only shifts the walls.

**Enemies and themes.** Each run is set in a theme with its own enemy mix: the **Ashen Crypt** (Ghouls), the **Hellforge** (Imps and Hellknights), the **Shadow Cathedral** (Cultists and Rift Callers) and the **Howling Wastes** (Hellhound packs). A dungeon keeps one theme. A raid moves to the next theme every 3 floors. Besides Ghouls, Imps and Hellknights you will meet:

- **Cultist**: attacks from 3 tiles, but heals a badly wounded demon within 3 tiles for 8 instead.
- **Hellhound**: fast and fragile. It moves 6 tiles and bites for +3 after dashing 3 or more.
- **Rift Caller**: every other turn it summons an Imp beside it, up to 2 at a time. Kill it first.

Deeper floors are harder: enemies gain 8% Health per floor and +1 Damage every 3 floors, and every 4 floors adds an enemy to the room.

**Fallen heroes and the Priest.** A hero at 0 HP falls and stays on the board as a flat grey body. Fallen heroes stay down on later floors too. Sera, the **Priest**, can bring them back: **Resurrection** revives a body within 3 tiles at half HP (6 turn cooldown), and her ultimate **Mass Resurrection** revives every fallen hero next to her at a third of their HP and heals everyone else for 10. **Holy Light** heals 10, **Sanctuary** gives nearby heroes +2 Armor, and all her heals cure poison. If every hero falls, the run is lost.

**Loot.** Slain enemies can drop gear onto their tile, marked by a beam of light in the item's rarity color: white for Common, blue for Magic, yellow for Rare and orange for Legendary. Better items get taller beams, and Rare and Legendary ones light up the floor around them. Walk a hero over the tile to pick the items up. Anything still on the floor when you take the exit is left behind. The boss always drops two Rare or better items. Deeper floors drop better rarities and bigger stats.

**Gear.** Heroes have 13 slots: head, shoulders, amulet, chest, wrists, hands, waist, legs, feet, two rings, main hand and off hand. Gear adds Damage, Armor, Health, Attack Speed, crit chance and crit damage, and Legendary boots add Move. After each floor you make **camp**: pick a hero, compare stash items against what they wear (green is better, red is worse), equip, then descend.

**Levels and saving.** Your progress is saved on your computer and kept between sessions. Every hero in the squad earns the run's XP: kills (deeper floors pay more, reinforcements pay half), a bonus for each cleared floor and a big one for sealing the rift. Each level adds 3 Health, every 3rd level adds 1 Damage and every 4th adds 1 Armor, up to level 20. Gear stays with your heroes too:

- **Win** and you keep everything you found. Spare items go to the vault.
- **Lose** (or abandon or restart a run) and heroes keep what they are wearing and the XP they earned, but items left in the stash are lost. Gear a hero brought into the run always comes back.

Between runs, open the **Armory** from the title screen to see each hero's level and stats, swap gear from the vault, and discard what you don't need (click Discard twice). **Settings > Reset progress** on the title screen wipes every level, item and stat. The save is `profile.json` in Unity's persistent data folder (on a Mac, `~/Library/Application Support/DefaultCompany/Shards-of-Eternity`). The PlayMode tests use a temporary folder, so they never touch your save.

**Action shards** are an experimental option on the squad screen. Each hero gets 2 shards per turn and every move, attack or ability costs 1, so you can move twice, attack twice, or do one of each. With **Bank unspent shards** on, shards you don't spend carry over, up to 4.

| Action | Mouse and keyboard | Touch |
| --- | --- | --- |
| Preview | Hover a tile or enemy | Tap once |
| Move | Click a blue tile (orange is a Barbarian charge, teal is a Shaman swap) | Tap |
| Attack | Click a red enemy | Tap twice |
| Ability | Press 1 to 4 or click its button, then click a purple tile | Tap the button, then tap a target twice |
| End turn | E | End turn button |
| Pause menu | P or Esc (Esc cancels an aimed ability first) | Pause or Menu button |
| Rotate camera | Right drag, or Z / X | Two-finger twist |
| Pan camera | Middle drag, or WASD / arrow keys | Two-finger drag |
| Zoom | Scroll wheel | Pinch |
| Re-center camera | F | |

The bar at the top is the turn order, with the floor counter, the reinforcement timer and the Pause button. The side panel (below the board on a portrait phone) shows the current unit, its ability cards with cooldowns, the exact outcome before you commit, rift pressure and the log. Health bars and status tags float over every unit. Hover an ability card to read what it does.

The **pause menu** has Resume, Settings, Restart run and Abandon run. **Settings** sets the interface size, turns the health bars over units on or off, and lists the controls. Settings are remembered between sessions. On the squad screen you pick 4 of the 7 classes, a dungeon or a raid, and the experimental rules.

You can also select the **Shards of Eternity** object in the scene and set the squad, run type, a fixed seed, the shard rules or the reinforcement schedule from the Inspector. Tick **Skip Title** to go straight into a run with those settings.

## 5. Run the tests

Open **Window > General > Test Runner**, choose **EditMode**, and press **Run All**. There are 71 tests covering levels, XP and saving, body blocking, the Priest and revives, enemy behaviors, themes and floor scaling, turn order, damage, abilities, enemy turns, room generation, floors and the boss, action shards, loot and gear, and simulated battles and whole runs.

The **PlayMode** tab has 2 more tests that play through every screen (title, Armory, squad, battle, pause, settings, camp, end of run) in landscape and portrait. They save a screenshot of each to `Logs/Screenshots` in the project folder.

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
| `Assets/ShardsOfEternity/Scripts/Core` | The game rules in plain C# with no Unity dependency: board, pathing, body blocking, turn timeline, damage, all 28 abilities, enemy AI, rift pressure, the room generator, the themes, and the profile with levels, XP and the save format. |
| `Assets/ShardsOfEternity/Scripts/Game` | The Unity side: `GameBootstrap` builds the scene, `BoardView` draws and animates, `BattleController` runs the turn loop and input, `CameraRig` moves the camera, `ProfileStore` reads and writes the save, and `BattleHud` is the UI (built with UI Toolkit in code). |
| `Assets/ShardsOfEternity/Resources/UI` | `Shards.uss`, the style sheet for the whole UI: colors, panels, buttons, cards. Edit it to restyle the game. |
| `Assets/ShardsOfEternity/Editor` | Creates the prototype scene and the menu item. |
| `Assets/ShardsOfEternity/Tests/EditMode` | Unit tests for the rules. |
| `Assets/ShardsOfEternity/Tests/PlayMode` | Plays through every screen in landscape and portrait and saves screenshots to `Logs/Screenshots`. |

The rules resolve instantly and report what happened as a list of events (moved, damaged, knocked back, and so on). The view plays those events back as animations. That split keeps the rules testable and lets the same rules drive a PC, mobile or VR presentation later.
