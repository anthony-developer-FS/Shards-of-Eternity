"""
Builds every model and writes it to Assets/ShardsOfEternity/Resources/Models as a .bytes file the game loads.

    python -m venv venv && venv/bin/pip install bpy numpy
    venv/bin/python Tools/ArtGen/build_models.py            # everything
    venv/bin/python Tools/ArtGen/build_models.py hero_rogue # just the names given

Output is deterministic, so re-running only changes files whose code changed. Meta files get GUIDs derived from the
file name, so they stay stable too.
"""
import hashlib
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import characters  # noqa: E402
import environment  # noqa: E402
import kit  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "ShardsOfEternity", "Resources", "Models")

META = """fileFormatVersion: 2
guid: {guid}
TextScriptImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def guid(name):
    return hashlib.md5(("shards-of-eternity/" + name).encode()).hexdigest()


def all_builders():
    out = {}
    for k, fn in characters.HEROES.items():
        out["hero_" + k] = (fn, dict(ao_dist=0.12, samples=20))
    for k, fn in characters.ENEMIES.items():
        out["enemy_" + k] = (fn, dict(ao_dist=0.12, samples=20))
    for k, fn in environment.build_list().items():
        big = k.startswith(("float_rock", "spire", "rift_tear"))
        out[k] = (fn, dict(ao_dist=0.6 if big else 0.2, samples=12, height_grad=0.0 if big else 0.15,
                           floor_z=None if big or k.startswith("rock_") else -0.13))
    return out


def main(names):
    os.makedirs(OUT, exist_ok=True)
    folder_meta = OUT + ".meta"
    if not os.path.exists(folder_meta):
        with open(folder_meta, "w", newline="\n") as f:
            f.write(FOLDER_META.format(guid=guid("Resources/Models")))
    builders = all_builders()
    for name, (fn, opts) in builders.items():
        if names and name not in names:
            continue
        t = time.time()
        model = fn()
        path = os.path.join(OUT, name + ".bytes")
        verts, tris, _ = kit.write_mesh(path, model, **opts)
        with open(path + ".meta", "w", newline="\n") as f:
            f.write(META.format(guid=guid("Models/" + name)))
        print(f"{name:28s} {verts:6d} verts {tris:6d} tris  {time.time() - t:.1f}s")


if __name__ == "__main__":
    main(set(sys.argv[1:]))
