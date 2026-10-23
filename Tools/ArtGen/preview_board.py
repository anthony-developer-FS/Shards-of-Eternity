"""Renders a sample room in one theme from the game camera's angle: preview_board.py crypt|hellforge|cathedral|wastes [samples]."""
import sys, math, random
import os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kit, characters as ch, environment as env
import bpy
OUT = os.environ.get("SOE_PREVIEW_OUT", ".")
theme = sys.argv[1]
samples = int(sys.argv[2]) if len(sys.argv) > 2 else 24
lib = env.build_list()
cache = {}
def get(name, fn=None):
    if name not in cache:
        m = (fn or lib[name])()
        cache[name] = kit.finalize(m, samples=8)
    return cache[name]
kit.reset_scene()
W, H = 10, 8
rng = random.Random(4)
grid = [["." for _ in range(W)] for _ in range(H)]
for x in range(W):
    grid[0][x] = grid[H-1][x] = "#"
for y in range(H):
    grid[y][0] = grid[y][W-1] = "#"
for (x, y) in [(3, 3), (6, 4), (4, 5)]: grid[y][x] = "P"
grid[2][7] = "S"; grid[5][2] = "^"; grid[1][5] = "E"; grid[0][5] = "."; grid[3][1]="."
for (x,y) in [(4,1),(5,6)]: grid[y][x] = "#"
objs = 0
def place(name, x, y, rz=0.0, z=0.0, fn=None):
    # tile (x, y) -> blender (x, y_b) with row 0 far away: Blender +Y is away from a camera at -Y.
    kit.add_preview_object(get(name, fn), name, location=(x, y_from(y), z), rotation_z=rz)
def y_from(y): return (H - 1 - y)
for y in range(H):
    for x in range(W):
        c = grid[y][x]
        h = (x * 7 + y * 13) % 4
        if c == "#":
            place(f"wall_{theme}_{h % 2}", x, y)
            if (x * 3 + y) % 7 == 0: place(f"light_{theme}", x, y, z=1.0 if h % 2 == 0 else 0.82)
        else:
            place(f"floor_{theme}_{h}", x, y, rz=math.radians(90 * ((x + y) % 4)))
            if c == "P": place(f"pillar_{theme}", x, y)
            if c == "^": place("spikes", x, y)
            if c == "E": place("exit_closed", x, y, rz=math.pi)
            if c == "S": place("spawn_crack", x, y)
        place(f"rock_{theme}_{1 if (x in (0, W-1) or y in (0, H-1)) and h % 2 else 0}", x, y, rz=math.radians(90*h))
heroes = [("paladin", 3, 6), ("rogue", 5, 5), ("sorcerer", 2, 6), ("priest", 4, 6)]
foes = {"crypt": ["ghoul", "ghoul", "imp"], "hellforge": ["hellknight", "imp", "imp"], "cathedral": ["cultist", "riftcaller", "ghoul"], "wastes": ["hellhound", "hellhound", "imp"]}[theme]
for (n, x, y) in heroes:
    place("hero_" + n, x, y, fn=ch.HEROES[n])
for n, (x, y) in zip(foes, [(6, 2), (3, 2), (7, 3)]):
    place("enemy_" + n, x, y, rz=math.pi, fn=ch.ENEMIES[n])
place("active_ring", 3, 6)
for i, (bx, by, bz, v) in enumerate([(-3, 4, -1.5, 0), (13, 7, -0.5, 1), (12, -2, -2.5, 2), (-2, -3, -2.0, 1)]):
    kit.add_preview_object(get(f"float_rock_{v}"), "fr", location=(bx, by, bz))
kit.add_preview_object(get("rift_tear"), "tear", location=(5, 22, 7))
kit.add_preview_object(get("spire_dark"), "sp", location=(-6, 18, -3))
kit.add_preview_object(get("spire_light"), "sp2", location=(17, 20, -2))
# lights
sun = {"crypt": (0.6, 0.75, 1.0), "hellforge": (1.0, 0.55, 0.35), "cathedral": (0.7, 0.55, 1.0), "wastes": (1.0, 0.8, 0.55)}[theme]
kit.add_light("SUN", (0, 0, 10), 2.0, sun, rot_euler=(math.radians(50), 0, math.radians(-35)))
kit.add_light("AREA", (4.5, 3.5, -4), 300, (0.7, 0.3, 1.0), size=12, rot_euler=(math.radians(180), 0, 0))
w = bpy.context.scene.world.node_tree.nodes["Background"]
w.inputs[0].default_value = (0.02, 0.015, 0.035, 1)
cx, cy = (W - 1) / 2, (H - 1) / 2
pitch = math.radians(58)
d = 17
kit.add_camera((cx, cy - d * math.cos(pitch), d * math.sin(pitch)), (cx, cy, 0), lens=55)
kit.render(f"{OUT}/board_{theme}.png", 1280, 800, samples=samples)
