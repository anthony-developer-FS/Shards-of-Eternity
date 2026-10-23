# Art and music generators

Every model and every music track in the game is made from code in this folder. Nothing is downloaded, sampled or
traced from existing work, so the game's art and soundtrack are entirely its own.

| Folder | What it makes | Output |
| --- | --- | --- |
| `ArtGen/` | Heroes, enemies, board pieces for the four rift themes, and the backdrop, as painted tabletop miniatures with baked shadows and glowing parts | `Assets/ShardsOfEternity/Resources/Models/*.bytes` |
| `MusicGen/` | Seven looping tracks: title, one per rift theme, the boss and camp | `Assets/ShardsOfEternity/Resources/Music/*.ogg` |

You only need these tools to change the art or music. The generated files are committed, so the Unity project opens
and plays without them.

## Models

The models are built with Blender's Python module (`bpy`), which runs without the Blender app:

```sh
python3.13 -m venv .venv
.venv/bin/pip install bpy numpy
.venv/bin/python Tools/ArtGen/build_models.py              # rebuild every model (about 20 seconds)
.venv/bin/python Tools/ArtGen/build_models.py hero_rogue   # or just the ones you name
```

- `kit.py` is the modeling kit: lathed profiles, ellipsoids, tapered tubes, extruded outlines, beveled boxes, ambient
  occlusion baked into vertex colors, and the exporter.
- `characters.py` has the 7 heroes and 7 enemies. Each is one function, so changing a helmet or a weapon is a few
  lines.
- `environment.py` has the floor, wall, pillar and light pieces per theme, the shared pieces (rift door, spikes,
  spawn crack, loot shard, active ring) and the backdrop (floating rocks, spires, the rift tear).

`preview_lineup.py` and `preview_board.py` render previews with Cycles, like the ones in `docs/art/` (set
`SOE_PREVIEW_OUT` to choose the folder). They show the models, not the game's lighting.

Models face -Y in Blender (toward the camera in the game), stand on z = 0 and use 1 unit per tile. A part can glow:
its glow amount is stored in the vertex color's alpha, and the `Shards/Miniature` shader turns it into emission.
The game reads the `.bytes` files with `ModelLibrary`. If a model is missing, the board falls back to the old
colored primitives.

## Music

The tracks are composed note by note in `compose.py` and rendered by a small synthesizer in `synth.py` (formant
choirs, detuned-saw strings and brass, organ, bells, plucked harp, flute, drums, wind and a convolution hall). It
needs Python 3 with numpy, and ffmpeg with libvorbis:

```sh
python3 Tools/MusicGen/compose.py          # all seven tracks (a few minutes)
python3 Tools/MusicGen/compose.py boss     # or just one
```

| Track | Plays | Feel |
| --- | --- | --- |
| `title` | Title, squad and Armory screens | A D minor hymn: choir above, low brass below, bells between |
| `crypt` | Ashen Crypt | Drone, distant choir, a music box and bells |
| `hellforge` | Hellforge | War drums, anvils and low brass in E Phrygian |
| `cathedral` | Shadow Cathedral | Organ, choir and bells in C minor |
| `wastes` | Howling Wastes | Wind, frame drums, a throat-sung drone and a flute |
| `boss` | Any floor with the Rift Lord | Fast taiko, racing strings, brass and choir |
| `camp` | Camp between floors | Warm pads, harp, flute and a crackling fire |

Each track's reverb tail is folded back onto its start, so it loops without a seam. `MusicPlayer` crossfades between
them, and the volume is in **Settings > Music**.
