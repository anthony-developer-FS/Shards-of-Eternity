"""
Procedural modeling kit for Shards of Eternity.

Every model in the game is built from code here: lathed profiles, ellipsoids, tapered tubes, extruded outlines and
beveled boxes, colored per part, then given baked ambient occlusion and written to a small binary mesh file that the
game loads at runtime (see Assets/ShardsOfEternity/Scripts/Game/ModelLibrary.cs). No imported or third-party art.

Coordinates while modeling are Blender's: Z is up, the model faces -Y (toward the viewer), 1 unit = 1 tile.
The exporter converts to Unity's left-handed Y-up space.
"""
import math
import random
import struct

import bpy  # noqa: F401  (must load before bmesh and mathutils)
import bmesh
import numpy as np
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree


def hexcol(h):
    return ((h >> 16 & 255) / 255.0, (h >> 8 & 255) / 255.0, (h & 255) / 255.0)


def mix(a, b, t):
    return tuple(a[i] * (1 - t) + b[i] * t for i in range(3))


def shade(c, k):
    return tuple(min(1.0, max(0.0, x * k)) for x in c)


class Part:
    __slots__ = ("verts", "faces", "color", "glow", "smooth", "ao")

    def __init__(self, verts, faces, color, glow=0.0, smooth=True, ao=True):
        self.verts = np.asarray(verts, dtype=np.float64).reshape(-1, 3)
        self.faces = faces
        self.color = color
        self.glow = glow
        self.smooth = smooth
        self.ao = ao


class Model:
    """A list of colored parts. Parts may overlap; they are never booleaned."""

    def __init__(self, name):
        self.name = name
        self.parts = []
        # Points of interest written into the file, in modeling space (e.g. where the health bar sits).
        self.anchors = {}

    # ---------- adding geometry ----------

    def add_bm(self, bm, color, glow=0.0, smooth=True, matrix=None, mirror_x=False, ao=True, jitter=0.0, seed=0):
        if matrix is not None:
            bmesh.ops.transform(bm, matrix=matrix, verts=bm.verts)
        verts = [v.co.copy() for v in bm.verts]
        if jitter > 0:
            rng = random.Random(seed)
            verts = [v + Vector((rng.uniform(-jitter, jitter), rng.uniform(-jitter, jitter), rng.uniform(-jitter, jitter)))
                     for v in verts]
        idx = {v: i for i, v in enumerate(bm.verts)}
        faces = [[idx[v] for v in f.verts] for f in bm.faces]
        bm.free()
        p = Part([tuple(v) for v in verts], faces, color, glow, smooth, ao)
        self.parts.append(p)
        if mirror_x:
            mv = p.verts.copy()
            mv[:, 0] *= -1
            self.parts.append(Part(mv, [list(reversed(f)) for f in faces], color, glow, smooth, ao))
        return p

    def mirror_last(self):
        p = self.parts[-1]
        mv = p.verts.copy()
        mv[:, 0] *= -1
        self.parts.append(Part(mv, [list(reversed(f)) for f in p.faces], p.color, p.glow, p.smooth, p.ao))

    def merge(self, other, matrix=None, mirror_x=False):
        for p in other.parts:
            v = p.verts
            if matrix is not None:
                m = np.array(matrix)
                v = (np.c_[v, np.ones(len(v))] @ m.T)[:, :3]
            self.parts.append(Part(v, p.faces, p.color, p.glow, p.smooth, p.ao))
            if mirror_x:
                mv = v.copy()
                mv[:, 0] *= -1
                self.parts.append(Part(mv, [list(reversed(f)) for f in p.faces], p.color, p.glow, p.smooth, p.ao))

    # ---------- primitives ----------

    def sphere(self, center, radii, color, seg=12, rings=8, rot=None, **kw):
        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=1.0)
        r = radii if isinstance(radii, (tuple, list)) else (radii, radii, radii)
        m = Matrix.Translation(center) @ (rot or Matrix.Identity(4)) @ Matrix.Diagonal((r[0], r[1], r[2], 1))
        return self.add_bm(bm, color, matrix=m, **kw)

    def ico(self, center, radii, color, subdiv=1, rot=None, **kw):
        bm = bmesh.new()
        bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=1.0)
        r = radii if isinstance(radii, (tuple, list)) else (radii, radii, radii)
        m = Matrix.Translation(center) @ (rot or Matrix.Identity(4)) @ Matrix.Diagonal((r[0], r[1], r[2], 1))
        kw.setdefault("smooth", False)
        return self.add_bm(bm, color, matrix=m, **kw)

    def box(self, center, size, color, bevel=0.0, rot=None, seg=1, **kw):
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0)
        bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
        if bevel > 0:
            bmesh.ops.bevel(bm, geom=list(bm.edges) + list(bm.verts), offset=bevel, segments=seg, affect="EDGES",
                            profile=0.5)
        m = Matrix.Translation(center) @ (rot or Matrix.Identity(4))
        kw.setdefault("smooth", False)
        return self.add_bm(bm, color, matrix=m, **kw)

    def cyl(self, center, r1, r2, depth, color, seg=12, rot=None, cap=True, **kw):
        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=cap, cap_tris=False, segments=seg, radius1=r1, radius2=r2, depth=depth)
        m = Matrix.Translation(center) @ (rot or Matrix.Identity(4))
        return self.add_bm(bm, color, matrix=m, **kw)

    def lathe(self, profile, color, seg=16, center=(0, 0, 0), scale=(1, 1, 1), rot=None, cap_top=True,
              cap_bottom=True, phase=0.0, arc=None, **kw):
        """Revolve a (radius, z) profile around Z. Radii may be (rx, ry) for oval sections.
        arc=(a0, a1) in degrees sweeps only part of the way round (an open hood), measured from -Y (the front)."""
        verts, faces = [], []
        n = len(profile)
        if arc is not None:
            a0, a1 = (math.radians(arc[0] - 90), math.radians(arc[1] - 90))
            ring = seg + 1
        else:
            ring = seg
        for j in range(ring):
            a = (a0 + (a1 - a0) * j / seg) if arc is not None else 2 * math.pi * j / seg + phase
            ca, sa = math.cos(a), math.sin(a)
            for (r, z) in profile:
                rx, ry = (r if isinstance(r, tuple) else (r, r))
                verts.append((rx * ca * scale[0], ry * sa * scale[1], z * scale[2]))
        for j in range(seg):
            jn = (j + 1) % ring
            for i in range(n - 1):
                a, b = j * n + i, jn * n + i
                faces.append([a, b, b + 1, a + 1])
        if arc is not None:
            # Open shells are drawn from both sides.
            back = [list(reversed(f)) for f in faces]
            off = len(verts)
            verts = verts + [(x * 0.93, y * 0.93, z) for (x, y, z) in verts]
            faces = faces + [[i + off for i in f] for f in back]
            return self._add_raw(verts, faces, color, center, rot, **kw)
        if cap_bottom and profile[0][0] != 0:
            faces.append([j * n for j in reversed(range(seg))])
        if cap_top and profile[-1][0] != 0:
            faces.append([j * n + n - 1 for j in range(seg)])
        return self._add_raw(verts, faces, color, center, rot, **kw)

    def tube(self, points, radii, color, seg=8, cap=True, flat=None, **kw):
        """A tube along a polyline with a radius per point (tapers to a point when a radius is 0).
        flat=(sx, sy) squashes the cross-section, for blades, tails and fingers."""
        pts = [Vector(p) for p in points]
        n = len(pts)
        verts, faces = [], []
        tangents = []
        for i in range(n):
            if i == 0:
                t = pts[1] - pts[0]
            elif i == n - 1:
                t = pts[-1] - pts[-2]
            else:
                t = (pts[i + 1] - pts[i - 1])
            tangents.append(t.normalized())
        up = Vector((0, 0, 1)) if abs(tangents[0].z) < 0.9 else Vector((0, 1, 0))
        normal = tangents[0].cross(up).normalized()
        frames = []
        for i in range(n):
            if i > 0:
                normal = normal - tangents[i] * normal.dot(tangents[i])
                if normal.length < 1e-6:
                    normal = tangents[i].orthogonal()
                normal.normalize()
            binormal = tangents[i].cross(normal).normalized()
            frames.append((normal, binormal))
        sx, sy = flat or (1, 1)
        for i in range(n):
            nm, bn = frames[i]
            r = radii[i] if isinstance(radii, (list, tuple)) else radii
            for j in range(seg):
                a = 2 * math.pi * j / seg
                off = nm * (math.cos(a) * r * sx) + bn * (math.sin(a) * r * sy)
                verts.append(tuple(pts[i] + off))
        for i in range(n - 1):
            for j in range(seg):
                jn = (j + 1) % seg
                faces.append([i * seg + j, i * seg + jn, (i + 1) * seg + jn, (i + 1) * seg + j])
        if cap:
            faces.append([j for j in reversed(range(seg))])
            faces.append([(n - 1) * seg + j for j in range(seg)])
        return self._add_raw(verts, faces, color, (0, 0, 0), None, **kw)

    def extrude(self, outline, depth, color, center=(0, 0, 0), rot=None, bevel=0.0, **kw):
        """An outline in the XZ plane, extruded along Y by depth (centered). Good for blades, wings, banners."""
        bm = bmesh.new()
        vs = [bm.verts.new((x, -depth / 2, z)) for (x, z) in outline]
        f = bm.faces.new(vs)
        bmesh.ops.triangulate(bm, faces=[f])
        ret = bmesh.ops.extrude_face_region(bm, geom=list(bm.faces))
        moved = [g for g in ret["geom"] if isinstance(g, bmesh.types.BMVert)]
        bmesh.ops.translate(bm, vec=Vector((0, depth, 0)), verts=moved)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        if bevel > 0:
            bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=1, affect="EDGES")
        m = Matrix.Translation(center) @ (rot or Matrix.Identity(4))
        kw.setdefault("smooth", False)
        return self.add_bm(bm, color, matrix=m, **kw)

    def _add_raw(self, verts, faces, color, center, rot, mirror_x=False, glow=0.0, smooth=True, ao=True, jitter=0.0,
                 seed=0):
        bm = bmesh.new()
        bv = [bm.verts.new(v) for v in verts]
        for f in faces:
            try:
                bm.faces.new([bv[i] for i in f])
            except ValueError:
                pass
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
        m = Matrix.Translation(center) @ (rot or Matrix.Identity(4))
        return self.add_bm(bm, color, glow=glow, smooth=smooth, matrix=m, mirror_x=mirror_x, ao=ao, jitter=jitter,
                           seed=seed)

    # ---------- finishing ----------

    def bounds(self):
        allv = np.vstack([p.verts for p in self.parts])
        return allv.min(0), allv.max(0)


def rot(x=0, y=0, z=0):
    return (Matrix.Rotation(math.radians(z), 4, "Z") @ Matrix.Rotation(math.radians(y), 4, "Y") @
            Matrix.Rotation(math.radians(x), 4, "X"))


def look_rot(direction, roll=0):
    """Rotation that points local +Z along direction."""
    d = Vector(direction).normalized()
    q = Vector((0, 0, 1)).rotation_difference(d)
    return q.to_matrix().to_4x4() @ Matrix.Rotation(math.radians(roll), 4, "Z")


# ---------------------------------------------------------------- baking and export

def _triangulate(model):
    """Flatten parts into triangle soup with per-corner normals and colors (before AO)."""
    P, N, C, G, AOmask = [], [], [], [], []
    for p in model.parts:
        v = p.verts
        tri_idx = []
        for f in p.faces:
            for k in range(1, len(f) - 1):
                tri_idx.append((f[0], f[k], f[k + 1]))
        if not tri_idx:
            continue
        tri_idx = np.array(tri_idx)
        a, b, c = v[tri_idx[:, 0]], v[tri_idx[:, 1]], v[tri_idx[:, 2]]
        fn = np.cross(b - a, c - a)
        area = np.linalg.norm(fn, axis=1)
        keep = area > 1e-12
        tri_idx, fn, area = tri_idx[keep], fn[keep], area[keep]
        fn = fn / area[:, None]
        if p.smooth:
            vn = np.zeros_like(v)
            for k in range(3):
                np.add.at(vn, tri_idx[:, k], fn * area[:, None])
            # Weld normals of coincident verts within the part (seams of lathes and spheres).
            keyed = {}
            for i, pos in enumerate(np.round(v, 5)):
                keyed.setdefault(tuple(pos), []).append(i)
            for ids in keyed.values():
                if len(ids) > 1:
                    s = vn[ids].sum(0)
                    vn[ids] = s
            vn /= np.maximum(np.linalg.norm(vn, axis=1)[:, None], 1e-12)
            corner_n = vn[tri_idx]
        else:
            corner_n = np.repeat(fn[:, None, :], 3, axis=1)
        P.append(v[tri_idx])
        N.append(corner_n)
        C.append(np.tile(np.array(p.color, dtype=np.float64), (len(tri_idx), 3, 1)))
        G.append(np.full((len(tri_idx), 3), p.glow))
        AOmask.append(np.full((len(tri_idx), 3), 1.0 if p.ao else 0.0))
    return (np.concatenate(P), np.concatenate(N), np.concatenate(C), np.concatenate(G), np.concatenate(AOmask))


def bake_ao(P, N, AOmask, samples=20, dist=0.18, strength=0.85, floor_z=None, seed=1):
    """Ambient occlusion per corner, by casting hemisphere rays against the whole model (and an optional ground)."""
    flat = P.reshape(-1, 3)
    tris = [(3 * i, 3 * i + 1, 3 * i + 2) for i in range(len(P))]
    bvh = BVHTree.FromPolygons([Vector(x) for x in flat], tris, epsilon=0.0)
    rng = np.random.default_rng(seed)
    dirs = rng.normal(size=(samples, 3))
    dirs /= np.linalg.norm(dirs, axis=1)[:, None]
    cache = {}
    out = np.ones(flat.shape[0])
    Nf = N.reshape(-1, 3)
    mask = AOmask.reshape(-1)
    for i in range(flat.shape[0]):
        if mask[i] == 0:
            continue
        key = (round(flat[i, 0], 4), round(flat[i, 1], 4), round(flat[i, 2], 4),
               round(Nf[i, 0], 2), round(Nf[i, 1], 2), round(Nf[i, 2], 2))
        if key in cache:
            out[i] = cache[key]
            continue
        n = Nf[i]
        origin = Vector(flat[i] + n * 2e-3)
        hits = 0.0
        total = 0.0
        for d in dirs:
            if d @ n < 0:
                d = -d
            w = float(d @ n)
            total += w
            loc, _, _, h = bvh.ray_cast(origin, Vector(d), dist)
            if loc is not None:
                hits += w * (1 - h / dist) ** 0.5
            elif floor_z is not None and d[2] < 0:
                t = (floor_z - flat[i, 2]) / d[2]
                if 0 < t < dist:
                    hits += w * (1 - t / dist) ** 0.5
        ao = 1 - strength * hits / max(total, 1e-6)
        cache[key] = ao
        out[i] = ao
    return out.reshape(P.shape[0], 3)


def finalize(model, ao=True, ao_dist=0.18, ao_strength=0.85, floor_z=0.0, height_grad=0.25, samples=20):
    P, N, C, G, M = _triangulate(model)
    if ao:
        A = bake_ao(P, N, M, samples=samples, dist=ao_dist, strength=ao_strength, floor_z=floor_z)
    else:
        A = np.ones(P.shape[:2])
    # A soft painted-miniature gradient: a little darker toward the feet.
    zmin, zmax = P[..., 2].min(), P[..., 2].max()
    if height_grad > 0 and zmax > zmin:
        h = (P[..., 2] - zmin) / (zmax - zmin)
        A = A * (1 - height_grad + height_grad * np.clip(h * 1.6, 0, 1))
    glow = G
    # Glowing parts ignore occlusion.
    lit = np.where(glow[..., None] > 0, C, C * A[..., None])
    return P, N, lit, glow


def write_mesh(path, model, **kw):
    """
    Binary format (little endian), version 1:
      'SOEM' u32 version, u32 vertexCount, u32 indexCount, 6 x f32 bounds (min, max),
      u32 anchorCount, then per anchor: u8 nameLength, name bytes, 3 x f32 position,
      vertexCount x (3 f32 position, 3 f32 normal, 4 u8 color RGBA where A is glow), indexCount x u32.
    Positions are converted to Unity space (x, z, y) and triangles rewound for the left-handed frame.
    """
    P, N, Cl, Gl = finalize(model, **kw)
    verts = {}
    vdata = []
    indices = []
    for t in range(P.shape[0]):
        tri = []
        for k in range(3):
            p = P[t, k]
            n = N[t, k]
            c = Cl[t, k]
            g = Gl[t, k]
            rgba = (int(round(min(1, c[0]) * 255)), int(round(min(1, c[1]) * 255)), int(round(min(1, c[2]) * 255)),
                    int(round(min(1, g) * 255)))
            up = (round(p[0], 5), round(p[2], 5), round(p[1], 5))
            un = (round(n[0], 3), round(n[2], 3), round(n[1], 3))
            key = (up, un, rgba)
            i = verts.get(key)
            if i is None:
                i = len(vdata)
                verts[key] = i
                vdata.append(key)
            tri.append(i)
        indices.extend((tri[0], tri[2], tri[1]))
    pos = np.array([v[0] for v in vdata])
    bmin, bmax = pos.min(0), pos.max(0)
    with open(path, "wb") as f:
        f.write(b"SOEM")
        f.write(struct.pack("<III", 1, len(vdata), len(indices)))
        f.write(struct.pack("<6f", *bmin, *bmax))
        f.write(struct.pack("<I", len(model.anchors)))
        for name, a in model.anchors.items():
            nb = name.encode("ascii")
            f.write(struct.pack("<B", len(nb)) + nb)
            f.write(struct.pack("<3f", a[0], a[2], a[1]))
        buf = bytearray()
        for (p, n, c) in vdata:
            buf += struct.pack("<6f4B", *p, *n, *c)
        f.write(buf)
        f.write(np.array(indices, dtype="<u4").tobytes())
    return len(vdata), len(indices) // 3, (P, N, Cl, Gl)


# ---------------------------------------------------------------- preview rendering (Cycles, CPU)

def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 48
    scene.cycles.use_denoising = True
    scene.render.film_transparent = False
    world = bpy.data.worlds.new("World")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.02, 0.02, 0.03, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 1.0
    return scene


_preview_mat = None


def preview_material():
    global _preview_mat
    if _preview_mat is not None and _preview_mat.name in bpy.data.materials:
        return _preview_mat
    mat = bpy.data.materials.new("Mini")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    attr = nt.nodes.new("ShaderNodeAttribute")
    attr.attribute_name = "Col"
    attr.attribute_type = "GEOMETRY"
    nt.links.new(attr.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(attr.outputs["Color"], bsdf.inputs["Emission Color"])
    glow = nt.nodes.new("ShaderNodeAttribute")
    glow.attribute_name = "Glow"
    math_node = nt.nodes.new("ShaderNodeMath")
    math_node.operation = "MULTIPLY"
    math_node.inputs[1].default_value = 4.0
    nt.links.new(glow.outputs["Fac"], math_node.inputs[0])
    nt.links.new(math_node.outputs[0], bsdf.inputs["Emission Strength"])
    bsdf.inputs["Roughness"].default_value = 0.6
    _preview_mat = mat
    return mat


def add_preview_object(baked, name, location=(0, 0, 0), rotation_z=0.0):
    P, N, Cl, Gl = baked
    me = bpy.data.meshes.new(name)
    flat = P.reshape(-1, 3)
    me.from_pydata([tuple(v) for v in flat], [], [(3 * i, 3 * i + 1, 3 * i + 2) for i in range(P.shape[0])])
    col = me.color_attributes.new("Col", "FLOAT_COLOR", "CORNER")
    cl = Cl.reshape(-1, 3)
    data = np.c_[cl, np.ones(len(cl))].astype(np.float32).ravel()
    # Corner order matches vertex order since each triangle owns its 3 verts.
    col.data.foreach_set("color", data)
    gl = me.attributes.new("Glow", "FLOAT", "CORNER")
    gl.data.foreach_set("value", Gl.reshape(-1).astype(np.float32))
    me.normals_split_custom_set([tuple(n) for n in N.reshape(-1, 3)])
    me.validate()
    ob = bpy.data.objects.new(name, me)
    ob.location = location
    ob.rotation_euler = (0, 0, rotation_z)
    ob.data.materials.append(preview_material())
    bpy.context.scene.collection.objects.link(ob)
    return ob


def add_light(kind, loc, energy, color=(1, 1, 1), size=1.0, rot_euler=None):
    ld = bpy.data.lights.new(kind + "L", kind)
    ld.energy = energy
    ld.color = color
    if kind == "AREA":
        ld.size = size
    elif kind == "POINT":
        ld.shadow_soft_size = size
    ob = bpy.data.objects.new(kind, ld)
    ob.location = loc
    if rot_euler:
        ob.rotation_euler = rot_euler
    bpy.context.scene.collection.objects.link(ob)
    return ob


def add_camera(loc, target, lens=50, ortho=None):
    cd = bpy.data.cameras.new("Cam")
    cd.lens = lens
    if ortho:
        cd.type = "ORTHO"
        cd.ortho_scale = ortho
    ob = bpy.data.objects.new("Cam", cd)
    ob.location = loc
    d = Vector(target) - Vector(loc)
    ob.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.collection.objects.link(ob)
    bpy.context.scene.camera = ob
    return ob


def render(path, w=1280, h=720, samples=48):
    scene = bpy.context.scene
    scene.render.resolution_x = w
    scene.render.resolution_y = h
    scene.cycles.samples = samples
    scene.render.filepath = path
    scene.render.image_settings.file_format = "PNG"
    bpy.ops.render.render(write_still=True)
