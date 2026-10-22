# Shards of Eternity

A turn-based tactical dungeon crawler set in Aethra, a world broken when the Veil shattered. You lead a squad of four heroes through rifts that rebuild themselves every time you enter. The game mixes Demeo's board, Diablo's loot and RAID's squad building, and it plays like chess.

Target platforms: PC and mobile first, VR later. Engine: Unity.

## What's here

| Path | What it is |
| --- | --- |
| `prototype/index.html` | Playable browser prototype of one combat room. Open it in any browser. |
| `.gitignore` | Unity ignore rules, ready for the Unity project. |

## The combat prototype

Open `prototype/index.html` in a browser. It has no build step and no dependencies.

- Pick 4 of the 6 human classes: Barbarian, Paladin, Rogue, Sorcerer, Hunter and Shaman.
- Turn order follows Attack Speed, and the timeline shows the next 10 turns.
- Each class has its own movement rule, such as the Barbarian's 12-tile charge or the Sorcerer's teleport.
- Heroes can't move through anyone. Enemies can pass through each other but not through heroes.
- Damage is shown before you attack. Crits are the only luck.
- Once the room wakes, rift pressure brings reinforcements and shifts the walls, with a warning one round ahead.

## Core rules (current draft)

- Heroes are a race plus a class. Humans come first; angels and demons follow as races with their own classes.
- On a turn, a unit moves up to its Move stat and takes one action, in either order.
- Gear uses 13 Diablo-style slots and adds stats such as Attack Speed, crit chance and crit damage.
