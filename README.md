# Shards of Eternity

A turn-based tactical dungeon crawler set in Aethra, a world broken when the Veil shattered. You lead a squad of four heroes through rifts that rebuild themselves every time you enter. The game mixes Demeo's board, Diablo's loot and RAID's squad building, and it plays like chess.

Target platforms: PC and mobile first, VR later. Engine: Unity.

## What's here

| Path | What it is |
| --- | --- |
| `Assets/`, `Packages/`, `ProjectSettings/` | The Unity 6 project: the combat prototype ported to Unity. See [docs/UNITY_SETUP.md](docs/UNITY_SETUP.md) to open and play it. |
| `Assets/ShardsOfEternity/Scripts/Core` | Game rules in plain C#, with unit tests. |
| `prototype/index.html` | Playable browser prototype of one combat room. Open it in any browser. |
| `.gitignore`, `.gitattributes` | Unity ignore rules and line-ending and merge settings. |

## The Unity prototype

Open the repository folder in Unity Hub with Unity 6 LTS and press Play. The first open creates the prototype scene for you. Full steps, controls and troubleshooting are in [docs/UNITY_SETUP.md](docs/UNITY_SETUP.md).

It has the same rules as the browser prototype, plus a procedural room generator with two themes (Ashen Crypt and Hellforge).

## The combat prototype

Open `prototype/index.html` in a browser. It has no build step and no dependencies.

- Pick 4 of the 6 human classes: Barbarian, Paladin, Rogue, Sorcerer, Hunter and Shaman.
- Turn order follows Attack Speed, and the timeline shows the next 10 turns.
- Each class has its own movement rule, such as the Barbarian's 12-tile charge or the Shaman's swap.
- Each hero has 3 abilities and an ultimate. The ultimate charges as the hero fights.
- Heroes can't move through anyone. Enemies can pass through each other but not through heroes.
- Damage is shown before you attack. Crits are the only luck.
- Once the room wakes, rift pressure brings reinforcements and shifts the walls, with a warning one round ahead.

## Core rules (current draft)

- Heroes are a race plus a class. Humans come first; angels and demons follow as races with their own classes.
- On a turn, a unit moves up to its Move stat and takes one action, in either order.
- Gear uses 13 Diablo-style slots and adds stats such as Attack Speed, crit chance and crit damage.
