"""Renders a lineup of miniatures with Cycles: preview_lineup.py heroes|enemies [name,name,...] [samples]."""
import sys, time, math
import os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kit, characters as ch
import bpy
OUT = os.environ.get("SOE_PREVIEW_OUT", ".")
which = sys.argv[1]
names = sys.argv[2].split(",") if len(sys.argv) > 2 else None
group = dict(ch.HEROES, **ch.ENEMIES)
if names is None:
    names = list(ch.HEROES if which == "heroes" else ch.ENEMIES)
kit.reset_scene()
bpy.context.scene.world.node_tree.nodes["Background"].inputs[0].default_value = (0.03, 0.025, 0.045, 1)
cols = min(4, len(names))
sp = 0.85
for i, name in enumerate(names):
    m = group[name]()
    b = kit.finalize(m, samples=12)
    r, c = divmod(i, cols)
    kit.add_preview_object(b, name, location=(c * sp, r * sp * 1.1, 0))
rows = (len(names) + cols - 1) // cols
cx = (cols - 1) * sp / 2; cy = (rows - 1) * sp * 1.1 / 2
kit.add_light("SUN", (0, 0, 5), 2.2, (1.0, 0.92, 0.85), rot_euler=(math.radians(40), 0, math.radians(-30)))
kit.add_light("AREA", (cx - 2, -3, 2), 140, (0.55, 0.6, 1.0), size=4)
kit.add_light("AREA", (cx + 2, 3, 2.5), 200, (0.8, 0.3, 1.0), size=3)
bpy.ops.mesh.primitive_plane_add(size=30, location=(cx, cy, 0))
pl = bpy.context.active_object
mat = bpy.data.materials.new("floor"); mat.use_nodes = True
mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.05, 0.045, 0.06, 1)
pl.data.materials.append(mat)
d = 2.4 + cols * 0.55 + rows * 0.3
kit.add_camera((cx, cy - d, d * 0.62), (cx, cy, 0.4), lens=50)
kit.render(f"{OUT}/{which}.png", 1400, 300 + 380 * rows, samples=int(sys.argv[3]) if len(sys.argv) > 3 else 32)
