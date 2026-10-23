"""
Heroes and enemies as painted tabletop miniatures: chunky heroic proportions on a round base, facing -Y.
Each builder returns a kit.Model. Heights are in tiles; a hero stands about 0.85 tall.
"""
import math

from kit import Model, hexcol, mix, rot, shade, look_rot

# Shared paints
STONE = hexcol(0x2A2830)
STONE_D = hexcol(0x1A1920)
GOLD = hexcol(0xD9A93E)
GOLD_D = hexcol(0x9C7424)
STEEL = hexcol(0xA7ADB8)
STEEL_D = hexcol(0x5E636E)
IRON = hexcol(0x3A3C44)
LEATHER = hexcol(0x6B4630)
LEATHER_D = hexcol(0x45291B)
SKIN = hexcol(0xE0AC88)
SKIN_D = hexcol(0xB97D5C)
HAIR_DARK = hexcol(0x2B1E17)
WHITE = hexcol(0xEDE8DC)
BONE = hexcol(0xD8CDB0)
RIFT = hexcol(0xB46CFF)
HELLFIRE = hexcol(0xFF5A1F)
EMBER = hexcol(0xFFB040)
HOLY = hexcol(0xFFE7A0)
BLACK = hexcol(0x141218)


def base(m, radius=0.3, hero=True, rim=None):
    """The miniature's base: a beveled stone disc with a painted rim and a little rubble on top."""
    rim = rim or (GOLD if hero else hexcol(0x8A2020))
    m.lathe([(radius, 0.0), (radius + 0.012, 0.012), (radius + 0.012, 0.038), (radius, 0.05), (radius - 0.02, 0.055),
             (0, 0.056)], STONE, seg=28, smooth=False)
    m.lathe([(radius + 0.014, 0.016), (radius + 0.018, 0.024), (radius + 0.014, 0.032)], rim, seg=28, cap_top=False,
            cap_bottom=False, smooth=True)
    for i, (a, d, s) in enumerate([(30, 0.7, 0.03), (150, 0.75, 0.025), (250, 0.6, 0.035), (320, 0.8, 0.02)]):
        r = radius * d
        m.ico((r * math.cos(math.radians(a)), r * math.sin(math.radians(a)), 0.06), (s, s * 0.9, s * 0.6),
              STONE_D if i % 2 else shade(STONE, 1.3), subdiv=0, jitter=0.004, seed=i)


def eyes(m, head, r, color, glow=1.0, spread=0.38, size=0.16, height=0.1):
    x, y, z = head
    for s in (-1, 1):
        m.sphere((x + s * r * spread, y - r * 0.86, z + r * height), (r * size, r * size * 0.6, r * size * 0.75),
                 color, seg=8, rings=5, glow=glow, ao=False)


def limb(m, pts, radii, color, seg=8, **kw):
    m.tube(pts, radii, color, seg=seg, **kw)


def humanoid(m, *, hip=0.36, skin=SKIN, legs=LEATHER, boots=LEATHER_D, torso=STEEL, belt=LEATHER_D,
             arms=None, hands=None, head_r=0.085, head_z=0.70, chest_w=0.15, waist_w=0.11, stance=0.075,
             left_hand=(-0.2, -0.07, 0.38), right_hand=(0.2, -0.07, 0.38), elbow_out=0.06, robe=None,
             torso_top=0.6, shoulder_z=0.56, shoulder_x=0.16, neck=True, face=True):
    """A stocky miniature body. Hands are given in model space; elbows are bent outward automatically.
    robe=(color, hem_radius) replaces legs with a long robe."""
    arms = arms or torso
    hands = hands or skin
    if robe is None:
        for s in (-1, 1):
            limb(m, [(s * 0.06, 0.0, hip), (s * (stance + 0.01), -0.01, 0.2), (s * stance, 0.0, 0.1)],
                 [0.05, 0.043, 0.04], legs)
            m.sphere((s * stance, -0.03, 0.085), (0.05, 0.075, 0.04), boots, seg=10, rings=6)
            m.cyl((s * stance, 0.0, 0.13), 0.048, 0.052, 0.07, boots, seg=10)
    else:
        color, hem = robe
        m.lathe([(hem, 0.058), (hem * 0.97, 0.08), ((hem * 0.8, hem * 0.75), 0.2), ((waist_w * 1.2, waist_w), hip),
                 ((waist_w, waist_w * 0.85), hip + 0.05)], color, seg=18, cap_top=False)
    # pelvis and torso
    m.lathe([((waist_w * 1.15, waist_w * 0.9), hip - 0.04), ((waist_w * 1.1, waist_w * 0.85), hip + 0.02),
             ((waist_w, waist_w * 0.8), hip + 0.08), ((chest_w, chest_w * 0.72), shoulder_z - 0.06),
             ((chest_w * 0.95, chest_w * 0.7), shoulder_z), ((chest_w * 0.55, chest_w * 0.45), torso_top), (0, torso_top + 0.01)],
            torso, seg=16)
    m.lathe([((waist_w * 1.2, waist_w * 0.95), hip - 0.005), ((waist_w * 1.22, waist_w * 0.97), hip + 0.03),
             ((waist_w * 1.17, waist_w * 0.93), hip + 0.035)], belt, seg=16, cap_top=False, cap_bottom=False)
    m.box((0, -waist_w * 0.95, hip + 0.015), (0.045, 0.02, 0.04), GOLD, bevel=0.004)
    # arms
    for s, hand in ((-1, left_hand), (1, right_hand)):
        sh = (s * shoulder_x, 0.0, shoulder_z)
        mid = ((sh[0] + hand[0]) / 2 + s * elbow_out, (sh[1] + hand[1]) / 2 + 0.03, (sh[2] + hand[2]) / 2)
        limb(m, [sh, mid, hand], [0.042, 0.036, 0.03], arms)
        m.sphere(hand, 0.034, hands, seg=8, rings=6)
    if neck:
        m.cyl((0, 0, torso_top + 0.02), 0.035, 0.032, 0.06, skin, seg=8)
    head = (0, -0.005, head_z)
    m.sphere(head, (head_r, head_r * 0.95, head_r * 1.05), skin, seg=14, rings=10)
    if face:
        m.sphere((0, -head_r * 0.98, head_z - head_r * 0.1), (head_r * 0.14, head_r * 0.14, head_r * 0.2),
                 shade(skin, 0.92), seg=6, rings=4)
    m.anchors["head"] = (0, 0, head_z + head_r)
    return head


def cape(m, color, top_z=0.58, bottom_z=0.1, width=0.17, flare=0.26, back=0.08, sway=0.0):
    """A cloak hanging down the back, as a curved shell."""
    rows, cols = 6, 9
    verts, faces = [], []
    for i in range(rows):
        t = i / (rows - 1)
        z = top_z + (bottom_z - top_z) * t
        w = width + (flare - width) * t
        for j in range(cols):
            u = j / (cols - 1) * 2 - 1
            a = u * math.pi * 0.55
            x = math.sin(a) * w + sway * t * t
            y = back * 0.6 + math.cos(a) * w * 0.55 + t * 0.05 + 0.012 * math.sin(j * 1.7 + i)
            verts.append((x, y, z))
    for i in range(rows - 1):
        for j in range(cols - 1):
            a = i * cols + j
            faces.append([a, a + 1, a + cols + 1, a + cols])
    # Give the cloth some thickness so it reads from both sides.
    n = len(verts)
    inner = [(x * 0.96, y - 0.012, z) for (x, y, z) in verts]
    verts += inner
    faces += [[n + f[0], n + f[3], n + f[2], n + f[1]] for f in faces[:]]
    m._add_raw(verts, faces, color, (0, 0, 0), None, smooth=True)


def pauldron(m, s, color, trim, z=0.56, x=0.17, r=0.075, spikes=0, spike_color=None):
    m.lathe([(r, -0.01), (r * 0.95, 0.03), (r * 0.7, 0.06), (r * 0.3, 0.075), (0, 0.078)], color, seg=12,
            center=(s * x, 0.0, z), rot=rot(y=s * 20), cap_bottom=False)
    m.lathe([(r * 1.04, -0.02), (r * 1.06, -0.005), (r * 1.0, 0.0)], trim, seg=12, center=(s * x, 0.0, z),
            rot=rot(y=s * 20), cap_top=False, cap_bottom=False)
    for k in range(spikes):
        a = math.radians(-30 + 60 * k / max(spikes - 1, 1))
        d = (s * math.cos(a) * 0.5, math.sin(a) * 0.5, 0.85)
        p0 = (s * x + d[0] * 0.07, d[1] * 0.07, z + 0.05)
        m.tube([p0, (p0[0] + d[0] * 0.09, p0[1] + d[1] * 0.09, p0[2] + d[2] * 0.09)], [0.016, 0.0],
               spike_color or BONE, seg=6)


def sword(m, grip, direction, length=0.38, blade=STEEL, hilt=GOLD, width=0.028, glow=0.0, guard=0.07):
    g = grip
    d = direction
    m.tube([(g[0] - d[0] * 0.05, g[1] - d[1] * 0.05, g[2] - d[2] * 0.05), (g[0] + d[0] * 0.04, g[1] + d[1] * 0.04, g[2] + d[2] * 0.04)],
           [0.014, 0.014], LEATHER_D, seg=6)
    m.sphere((g[0] - d[0] * 0.06, g[1] - d[1] * 0.06, g[2] - d[2] * 0.06), 0.018, hilt, seg=6, rings=4)
    gc = (g[0] + d[0] * 0.05, g[1] + d[1] * 0.05, g[2] + d[2] * 0.05)
    m.box(gc, (guard, 0.022, 0.018), hilt, bevel=0.005, rot=look_rot(d) @ rot(x=90))
    tip = (g[0] + d[0] * (0.05 + length), g[1] + d[1] * (0.05 + length), g[2] + d[2] * (0.05 + length))
    m.tube([gc, ((gc[0] + tip[0]) / 2, (gc[1] + tip[1]) / 2, (gc[2] + tip[2]) / 2), tip], [width, width * 0.9, 0.0], blade,
           seg=4, flat=(1.0, 0.25), smooth=False, glow=glow)


def shield(m, center, color, rim, emblem, emblem_glow=0.0, size=0.15, tilt=(10, 0, -25)):
    outline = [(0, size * 1.25), (size * 0.7, size * 1.05), (size * 0.85, size * 0.6), (size * 0.75, -size * 0.1),
               (size * 0.4, -size * 0.75), (0, -size * 1.15), (-size * 0.4, -size * 0.75), (-size * 0.75, -size * 0.1),
               (-size * 0.85, size * 0.6), (-size * 0.7, size * 1.05)]
    r = rot(*tilt)
    m.extrude([(x * 1.08, z * 1.06) for (x, z) in outline], 0.022, rim, center=center, rot=r)
    m.extrude(outline, 0.03, color, center=center, rot=r)
    # A sun emblem: a disc with rays, standing proud of the face.
    from mathutils import Vector
    off = r @ Vector((0, -0.02, 0.02))
    c = (center[0] + off.x, center[1] + off.y, center[2] + off.z)
    rays = []
    for k in range(16):
        a = 2 * math.pi * k / 16
        rr = size * (0.45 if k % 2 == 0 else 0.25)
        rays.append((math.cos(a) * rr, math.sin(a) * rr))
    m.extrude(rays, 0.012, emblem, center=c, rot=r, glow=emblem_glow)


def staff(m, bottom, top, color=LEATHER, r=0.016):
    m.tube([bottom, ((bottom[0] + top[0]) / 2 + 0.005, (bottom[1] + top[1]) / 2, (bottom[2] + top[2]) / 2), top],
           [r, r * 1.05, r * 0.9], color, seg=6)


def horn(m, root, direction, curl, length, r, color, seg=7, steps=6):
    pts, radii = [], []
    x, y, z = root
    dx, dy, dz = direction
    for i in range(steps):
        t = i / (steps - 1)
        pts.append((x, y, z))
        radii.append(r * (1 - t) ** 0.8)
        x += dx * length / steps
        y += dy * length / steps
        z += dz * length / steps
        dx, dy, dz = dx + curl[0], dy + curl[1], dz + curl[2]
    m.tube(pts, radii, color, seg=seg)


def wing(m, root, s, color, membrane, span=0.42, height=0.3, glow=0.0, feathered=False, back=0.1):
    """A bat wing (bones and membrane) or a feathered angel wing, swept up and back from the shoulder blade."""
    x0, y0, z0 = root
    if feathered:
        for k in range(7):
            t = k / 6
            ang = math.radians(15 + 70 * t)
            L = span * (0.55 + 0.45 * math.sin(math.pi * (0.3 + 0.7 * t)))
            tip = (x0 + s * math.cos(ang) * L * 0.9, y0 + back + t * 0.04, z0 + math.sin(ang) * height * 1.2 - 0.1 * t)
            m.tube([(x0 + s * 0.02, y0 + back * 0.4, z0 - 0.02 * t), ((x0 + tip[0]) / 2, (y0 + tip[1]) / 2 + 0.02, (z0 + tip[2]) / 2 + 0.03), tip],
                   [0.03, 0.028, 0.0], mix(color, membrane, t * 0.6), seg=6, flat=(1.0, 0.3), glow=glow * (1 - t * 0.5))
        return
    arm_end = (x0 + s * span * 0.55, y0 + back, z0 + height)
    m.tube([(x0, y0 + 0.02, z0), (x0 + s * span * 0.25, y0 + back * 0.6, z0 + height * 0.7), arm_end], [0.016, 0.014, 0.01],
           color, seg=6)
    fingers = [(span, height * 0.6), (span * 0.9, height * 0.05), (span * 0.6, -height * 0.4)]
    pts = [(x0, y0 + 0.03, z0 - 0.08)]
    for fx, fz in reversed(fingers):
        tip = (x0 + s * fx, y0 + back + 0.03, z0 + fz)
        m.tube([arm_end, tip], [0.01, 0.0], color, seg=5)
        pts.append(tip)
    pts.append(arm_end)
    # Membrane between the finger bones, scalloped.
    verts = [arm_end]
    faces = []
    outline = [pts[0]]
    for i in range(1, len(pts) - 1):
        a, b = outline[-1], pts[i]
        mid = ((a[0] + b[0]) / 2 - s * 0.03 * 0, (a[1] + b[1]) / 2, (a[2] + b[2]) / 2 + 0.03)
        mid = (mid[0] * 0.92 + arm_end[0] * 0.08, mid[1], mid[2] * 0.92 + arm_end[2] * 0.08)
        outline += [mid, b]
    for p in outline:
        verts.append(p)
    for i in range(1, len(verts) - 1):
        faces.append([0, i, i + 1] if s > 0 else [0, i + 1, i])
    n = len(verts)
    verts2 = [(v[0], v[1] + 0.006, v[2]) for v in verts]
    faces2 = [[n + f[0], n + f[2], n + f[1]] for f in faces]
    m._add_raw(verts + verts2, faces + faces2, membrane, (0, 0, 0), None, smooth=False, glow=glow)


def halo(m, center, r, color=HOLY, glow=1.0, thick=0.008):
    pts = [(center[0] + math.cos(2 * math.pi * k / 20) * r, center[1] + 0.02, center[2] + math.sin(2 * math.pi * k / 20) * r * 0.35)
           for k in range(21)]
    m.tube(pts, [thick] * 21, color, seg=5, glow=glow, ao=False, cap=False)


# =========================================================================== heroes


def barbarian():
    m = Model("hero_barbarian")
    base(m, 0.32)
    skin = hexcol(0xD99A72)
    fur = hexcol(0x7A5A3E)
    red = hexcol(0x8E2424)
    head = humanoid(m, skin=skin, torso=skin, arms=skin, legs=hexcol(0x4A3226), boots=hexcol(0x3B2A20), stance=0.095,
                    chest_w=0.17, waist_w=0.12, shoulder_x=0.18, head_r=0.083, head_z=0.69,
                    left_hand=(-0.13, -0.12, 0.5), right_hand=(0.15, -0.12, 0.47), elbow_out=0.08)
    # war paint, belt sash, kilt of fur
    m.lathe([((0.135, 0.115), 0.31), ((0.15, 0.13), 0.24), ((0.16, 0.14), 0.18)], fur, seg=14, cap_top=False,
            cap_bottom=False, jitter=0.006)
    m.box((0.0, -0.115, 0.27), (0.06, 0.02, 0.12), red, bevel=0.005)
    # chest scar strap
    m.tube([(-0.15, -0.06, 0.55), (0, -0.115, 0.45), (0.12, -0.08, 0.35)], [0.012] * 3, LEATHER_D, seg=6)
    # fur mantle
    m.lathe([((0.2, 0.15), 0.52), ((0.19, 0.15), 0.58), ((0.12, 0.1), 0.62), ((0.05, 0.05), 0.63)], fur, seg=16,
            cap_bottom=True, jitter=0.012, seed=3)
    # hair and beard
    m.sphere((head[0], head[1] + 0.025, head[2] + 0.02), (0.09, 0.085, 0.09), hexcol(0xA0522D), seg=12, rings=8,
             jitter=0.006)
    m.tube([(0, -0.07, 0.66), (0, -0.085, 0.6), (0, -0.07, 0.55)], [0.05, 0.04, 0.0], hexcol(0xA0522D), seg=8,
           flat=(1.0, 0.6))
    m.box((0, -0.084, 0.705), (0.12, 0.014, 0.022), hexcol(0x2E3C6E), bevel=0.003)  # warpaint band
    eyes(m, head, 0.083, WHITE, glow=0.2)
    # horned helm band
    m.lathe([(0.092, 0.72), (0.094, 0.745), (0.088, 0.76)], IRON, seg=14, center=(0, 0, 0), cap_top=False,
            cap_bottom=False)
    for s in (-1, 1):
        horn(m, (s * 0.08, 0.0, 0.75), (s * 0.7, -0.2, 0.35), (0, 0, 0.25), 0.16, 0.024, BONE)
    # great axe held two-handed across the body
    a, b = (-0.2, -0.13, 0.62), (0.26, -0.11, 0.24)
    staff(m, b, (a[0] - 0.05, a[1], a[2] + 0.06), LEATHER, 0.017)
    blade = [(0, 0.0), (0.06, 0.07), (0.14, 0.11), (0.17, 0.0), (0.14, -0.11), (0.06, -0.07)]
    for s in (-1, 1):
        m.extrude([(s * x, z) for (x, z) in blade], 0.014, STEEL, center=(-0.24, -0.13, 0.68), rot=rot(x=0, y=-30 + 0, z=0))
    m.extrude([(-0.03, 0.03), (0.03, 0.03), (0.03, -0.03), (-0.03, -0.03)], 0.03, IRON, center=(-0.24, -0.13, 0.68),
              rot=rot(y=-30))
    return m


def paladin():
    m = Model("hero_paladin")
    base(m, 0.32)
    plate, plate_d = hexcol(0xD7DCE6), hexcol(0x8D95A6)
    tabard = WHITE
    head = humanoid(m, torso=plate, arms=plate_d, legs=plate_d, boots=plate, belt=GOLD_D, chest_w=0.165, waist_w=0.115,
                    shoulder_x=0.17, head_r=0.08, head_z=0.7, left_hand=(-0.19, -0.1, 0.42),
                    right_hand=(0.17, -0.12, 0.5), elbow_out=0.05)
    # tabard front panel with a gold sun
    m.extrude([(-0.07, 0.0), (0.07, 0.0), (0.06, -0.24), (0, -0.28), (-0.06, -0.24)], 0.012, tabard,
              center=(0, -0.118, 0.5), rot=rot(x=-6))
    m.extrude([(math.cos(a) * 0.025, math.sin(a) * 0.025) for a in [2 * math.pi * k / 10 for k in range(10)]], 0.008,
              GOLD, center=(0, -0.128, 0.43), rot=rot(x=-6), glow=0.25)
    cape(m, hexcol(0x2D4C9C), top_z=0.6, bottom_z=0.1, width=0.16, flare=0.24, back=0.07)
    for s in (-1, 1):
        pauldron(m, s, plate, GOLD, z=0.56, x=0.18, r=0.085)
    # great helm with a crest and glowing visor slit
    m.lathe([(0.088, 0.64), (0.092, 0.69), (0.088, 0.75), (0.07, 0.785), (0.03, 0.8), (0, 0.802)], plate, seg=16,
            center=(0, -0.005, 0), cap_bottom=False)
    m.box((0, -0.088, 0.71), (0.11, 0.02, 0.016), HOLY, glow=0.9)
    m.box((0, -0.09, 0.69), (0.016, 0.02, 0.06), GOLD_D, bevel=0.003)
    m.extrude([(0, 0), (0.0, 0.11), (0.05, 0.13), (0.11, 0.08), (0.09, 0.02)], 0.016, hexcol(0x2D4C9C),
              center=(0, 0.02, 0.79), rot=rot(z=90))
    shield(m, (-0.23, -0.12, 0.42), WHITE, GOLD, GOLD, emblem_glow=0.6, size=0.15, tilt=(8, 0, -22))
    # holy mace raised
    staff(m, (0.17, -0.12, 0.44), (0.19, -0.16, 0.7), LEATHER_D, 0.013)
    m.ico((0.195, -0.165, 0.73), 0.045, GOLD, subdiv=1)
    for k in range(6):
        a = 2 * math.pi * k / 6
        m.box((0.195 + math.cos(a) * 0.045, -0.165 + math.sin(a) * 0.045, 0.73), (0.02, 0.02, 0.05), GOLD_D,
              rot=rot(z=math.degrees(a)))
    m.anchors["head"] = (0, 0, 0.84)
    return m


def rogue():
    m = Model("hero_rogue")
    base(m, 0.3)
    cloth, cloth_d = hexcol(0x3A2B52), hexcol(0x231A33)
    head = humanoid(m, torso=cloth, arms=cloth_d, legs=cloth_d, boots=LEATHER_D, belt=LEATHER, chest_w=0.135,
                    waist_w=0.1, shoulder_x=0.15, head_r=0.078, head_z=0.67, stance=0.09,
                    left_hand=(-0.2, -0.12, 0.36), right_hand=(0.19, -0.14, 0.45), elbow_out=0.07)
    cape(m, hexcol(0x5B3F8C), top_z=0.6, bottom_z=0.16, width=0.15, flare=0.24, back=0.06, sway=0.05)
    # hood: an open cone over the head, with a dark face hollow and glowing eyes
    m.lathe([(0.105, 0.6), (0.11, 0.66), (0.1, 0.72), (0.075, 0.77), (0.03, 0.81), (0.0, 0.83)], hexcol(0x5B3F8C),
            seg=16, center=(0, 0.01, 0), scale=(1, 1.05, 1), arc=(42, 318))
    m.sphere((0, -0.06, 0.675), (0.065, 0.04, 0.06), BLACK, seg=10, rings=6, ao=False)
    eyes(m, (0, 0.01, 0.675), 0.075, hexcol(0xE6B3FF), glow=1.0, spread=0.35, size=0.13)
    # face mask
    m.box((0, -0.085, 0.645), (0.09, 0.02, 0.035), cloth_d, bevel=0.006)
    # bandolier and pouches
    m.tube([(-0.13, -0.08, 0.56), (0, -0.115, 0.46), (0.11, -0.09, 0.36)], [0.011] * 3, LEATHER, seg=6)
    for x in (-0.07, 0.07):
        m.box((x, -0.12, 0.34), (0.04, 0.03, 0.04), LEATHER, bevel=0.006)
    # twin daggers, one held low reversed, one forward
    sword(m, (-0.2, -0.12, 0.36), (0.1, -0.35, -0.93), length=0.15, width=0.02, guard=0.04, blade=hexcol(0xC7C9E0))
    sword(m, (0.19, -0.14, 0.45), (0.15, -0.7, 0.7), length=0.17, width=0.02, guard=0.04, blade=hexcol(0xC7C9E0),
          glow=0.15)
    m.anchors["head"] = (0, 0, 0.84)
    return m


def sorcerer():
    m = Model("hero_sorcerer")
    base(m, 0.3)
    robe, robe_d = hexcol(0x1F3F7A), hexcol(0x152A52)
    head = humanoid(m, torso=robe, arms=robe, belt=GOLD_D, robe=(robe_d, 0.2), chest_w=0.13, waist_w=0.1,
                    shoulder_x=0.145, head_r=0.08, head_z=0.68, left_hand=(-0.17, -0.12, 0.47),
                    right_hand=(0.2, -0.05, 0.4), elbow_out=0.06)
    # robe front trim and runes
    m.extrude([(-0.03, 0.0), (0.03, 0.0), (0.06, -0.3), (-0.06, -0.3)], 0.01, GOLD, center=(0, -0.14, 0.36),
              rot=rot(x=-16))
    for k, z in enumerate((0.3, 0.22, 0.14)):
        m.box((0, -0.16 - 0.025 * k, z), (0.02, 0.01, 0.02), hexcol(0x7FD8FF), glow=0.9, rot=rot(y=45))
    # high collar
    m.lathe([((0.1, 0.08), 0.57), ((0.13, 0.1), 0.66), ((0.14, 0.11), 0.7)], robe, seg=14, center=(0, 0.02, 0),
            cap_top=False, cap_bottom=False)
    # white hair and beard
    m.sphere((0, 0.02, 0.705), (0.085, 0.08, 0.075), hexcol(0xE6E6EE), seg=12, rings=8, jitter=0.004)
    m.tube([(0, -0.068, 0.655), (0, -0.085, 0.6), (0, -0.075, 0.535)], [0.05, 0.038, 0.0], hexcol(0xE6E6EE), seg=8,
           flat=(1, 0.55))
    eyes(m, head, 0.08, hexcol(0x7FD8FF), glow=1.0)
    # pointed hat with a bent tip
    m.lathe([(0.15, 0.735), (0.15, 0.745), (0.09, 0.755), (0.075, 0.8), (0.05, 0.86), (0.0, 0.92)], robe, seg=16)
    m.tube([(0, 0, 0.9), (0.02, 0.03, 0.96), (0.06, 0.07, 0.98)], [0.025, 0.014, 0.0], robe, seg=8)
    m.lathe([(0.078, 0.77), (0.08, 0.79), (0.074, 0.8)], GOLD, seg=16, cap_top=False, cap_bottom=False)
    # staff with a floating arcane crystal in a claw
    staff(m, (-0.17, -0.12, 0.08), (-0.17, -0.12, 0.88), hexcol(0x3B2A20), 0.014)
    for k in range(3):
        a = 2 * math.pi * k / 3
        m.tube([(-0.17, -0.12, 0.86), (-0.17 + math.cos(a) * 0.04, -0.12 + math.sin(a) * 0.04, 0.92),
                (-0.17 + math.cos(a) * 0.02, -0.12 + math.sin(a) * 0.02, 0.97)], [0.008, 0.006, 0.0], GOLD, seg=5)
    m.ico((-0.17, -0.12, 0.93), (0.035, 0.035, 0.05), hexcol(0x7FD8FF), glow=1.0, ao=False)
    # spell in the open hand
    m.ico((0.21, -0.08, 0.47), 0.04, hexcol(0x9FE4FF), subdiv=1, glow=1.0, ao=False)
    m.anchors["head"] = (0, 0, 0.95)
    return m


def hunter():
    m = Model("hero_hunter")
    base(m, 0.3)
    green, green_d = hexcol(0x3E6B3A), hexcol(0x2A4728)
    head = humanoid(m, torso=LEATHER, arms=green_d, legs=hexcol(0x4F3A2A), boots=LEATHER_D, belt=LEATHER_D,
                    chest_w=0.14, waist_w=0.1, shoulder_x=0.15, head_r=0.078, head_z=0.68, stance=0.09,
                    left_hand=(-0.12, -0.2, 0.52), right_hand=(0.08, -0.05, 0.53), elbow_out=0.05)
    cape(m, green, top_z=0.6, bottom_z=0.2, width=0.15, flare=0.22, back=0.06)
    # hood down on the shoulders, hair, face
    m.lathe([((0.12, 0.1), 0.56), ((0.13, 0.11), 0.6), ((0.1, 0.1), 0.63)], green, seg=14, center=(0, 0.02, 0),
            jitter=0.004)
    m.sphere((0, 0.02, 0.705), (0.084, 0.08, 0.07), hexcol(0x6E4A2A), seg=12, rings=8)
    m.tube([(0, 0.07, 0.7), (0, 0.11, 0.62), (0, 0.1, 0.55)], [0.025, 0.02, 0.01], hexcol(0x6E4A2A), seg=6)
    eyes(m, head, 0.078, hexcol(0x2A2A2A), glow=0.0)
    # leather jerkin straps and quiver with fletched arrows
    m.tube([(0.13, -0.08, 0.57), (0, -0.11, 0.46), (-0.11, -0.08, 0.36)], [0.011] * 3, LEATHER_D, seg=6)
    m.cyl((0.08, 0.1, 0.5), 0.04, 0.045, 0.24, LEATHER_D, seg=10, rot=rot(y=-20))
    for k in range(4):
        x = 0.1 + 0.012 * (k - 1.5)
        m.box((x + 0.02, 0.1 + 0.006 * k, 0.64 + 0.01 * (k % 2)), (0.006, 0.025, 0.05), hexcol(0xC43B2E), rot=rot(y=-20))
    # longbow held out front, drawn
    bow = []
    for k in range(9):
        t = k / 8
        a = math.radians(-70 + 140 * t)
        bow.append((-0.12 + math.sin(-a) * 0.02 * 0 - (1 - math.cos(a)) * 0.08, -0.2 - (1 - math.cos(a)) * 0.0,
                    0.52 + math.sin(a) * 0.26))
    bow = [(x, y - 0.0, z) for (x, y, z) in bow]
    m.tube(bow, [0.008, 0.013, 0.015, 0.016, 0.02, 0.016, 0.015, 0.013, 0.008], hexcol(0x5A3A22), seg=6)
    m.tube([bow[0], (0.08, -0.05, 0.53), bow[-1]], [0.003] * 3, WHITE, seg=4, smooth=False)
    m.tube([(0.08, -0.05, 0.53), (-0.13, -0.24, 0.52)], [0.005, 0.005], hexcol(0x8A6A4A), seg=4)
    m.tube([(-0.13, -0.24, 0.52), (-0.16, -0.27, 0.52)], [0.012, 0.0], STEEL, seg=4)
    m.anchors["head"] = (0, 0, 0.82)
    return m


def shaman():
    m = Model("hero_shaman")
    base(m, 0.3)
    teal, teal_d = hexcol(0x2E8C86), hexcol(0x1D5A57)
    head = humanoid(m, skin=hexcol(0xB07A57), torso=teal, arms=hexcol(0xB07A57), legs=LEATHER, boots=LEATHER_D,
                    robe=(teal_d, 0.19), belt=BONE, chest_w=0.14, waist_w=0.105, shoulder_x=0.15, head_r=0.08,
                    head_z=0.68, left_hand=(-0.18, -0.1, 0.5), right_hand=(0.19, -0.1, 0.4), elbow_out=0.06)
    # fur mantle with bone beads
    m.lathe([((0.19, 0.14), 0.52), ((0.18, 0.14), 0.58), ((0.1, 0.08), 0.62)], hexcol(0x8A7A6A), seg=16,
            jitter=0.01, seed=5)
    for k in range(7):
        a = math.radians(200 + 20 * k)
        m.sphere((math.cos(a) * 0.15, math.sin(a) * 0.11 - 0.0, 0.5), 0.016, BONE, seg=6, rings=4)
    # braids
    m.sphere((0, 0.02, 0.71), (0.085, 0.08, 0.072), hexcol(0x1E1A1A), seg=12, rings=8)
    for s in (-1, 1):
        m.tube([(s * 0.07, -0.02, 0.66), (s * 0.08, -0.04, 0.58), (s * 0.075, -0.06, 0.5)], [0.018, 0.016, 0.01],
               hexcol(0x1E1A1A), seg=6)
    # bone mask over the upper face and feathered headdress
    m.lathe([((0.085, 0.06), 0.66), ((0.088, 0.065), 0.7), ((0.08, 0.06), 0.74)], BONE, seg=14, center=(0, -0.02, 0),
            phase=0.0, cap_top=False, cap_bottom=False)
    eyes(m, head, 0.08, hexcol(0x7FFFE0), glow=1.0)
    for k in range(5):
        a = math.radians(-50 + 25 * k)
        root = (math.sin(a) * 0.07, 0.03 + math.cos(a) * 0.02, 0.75)
        m.tube([root, (root[0] * 1.6, root[1] + 0.04, root[2] + 0.1), (root[0] * 2.2, root[1] + 0.08, root[2] + 0.16)],
               [0.012, 0.016, 0.0], hexcol(0xE8E0D0) if k % 2 else hexcol(0x2E8C86), seg=5, flat=(1, 0.35))
    # totem staff topped with a ram skull and spirit flame
    staff(m, (-0.18, -0.1, 0.08), (-0.18, -0.1, 0.82), hexcol(0x5A3A22), 0.016)
    m.sphere((-0.18, -0.11, 0.84), (0.035, 0.04, 0.035), BONE, seg=8, rings=6)
    for s in (-1, 1):
        horn(m, (-0.18 + s * 0.025, -0.1, 0.86), (s * 0.5, 0.3, 0.2), (0, -0.25, -0.35), 0.09, 0.012, BONE, steps=7)
    m.ico((-0.18, -0.11, 0.92), (0.03, 0.03, 0.05), hexcol(0x7FFFE0), glow=1.0, ao=False)
    m.ico((0.2, -0.1, 0.42), 0.032, hexcol(0x7FFFE0), glow=1.0, ao=False)
    m.anchors["head"] = (0, 0, 0.93)
    return m


def priest():
    m = Model("hero_priest")
    base(m, 0.3)
    robe, robe_d = hexcol(0xF1EDE4), hexcol(0xCFC6B6)
    head = humanoid(m, torso=robe, arms=robe, robe=(robe_d, 0.2), belt=GOLD, chest_w=0.13, waist_w=0.1,
                    shoulder_x=0.145, head_r=0.08, head_z=0.68, left_hand=(-0.13, -0.15, 0.47),
                    right_hand=(0.19, -0.08, 0.45), elbow_out=0.06)
    m.extrude([(-0.035, 0.0), (0.035, 0.0), (0.05, -0.34), (-0.05, -0.34)], 0.01, GOLD, center=(0, -0.13, 0.4),
              rot=rot(x=-14))
    # small angel wings behind the shoulders
    for s in (-1, 1):
        wing(m, (s * 0.06, 0.07, 0.56), s, hexcol(0xFFF6E0), hexcol(0xFFE3A0), span=0.3, height=0.22, feathered=True,
             glow=0.15)
    # long golden hair
    m.sphere((0, 0.02, 0.7), (0.088, 0.085, 0.08), hexcol(0xE8C36A), seg=12, rings=8)
    m.lathe([((0.085, 0.07), 0.5), ((0.09, 0.08), 0.6), ((0.088, 0.08), 0.68)], hexcol(0xE8C36A), seg=14,
            center=(0, 0.035, 0), cap_top=False, phase=0)
    eyes(m, head, 0.08, hexcol(0x6AA0FF), glow=0.4)
    halo(m, (0, 0.04, 0.82), 0.075)
    # holy book and a staff of light
    m.box((-0.13, -0.18, 0.47), (0.08, 0.025, 0.1), hexcol(0x8C2E2E), bevel=0.006, rot=rot(x=-20))
    m.box((-0.13, -0.195, 0.47), (0.03, 0.006, 0.05), GOLD, rot=rot(x=-20), glow=0.4)
    staff(m, (0.19, -0.08, 0.1), (0.19, -0.08, 0.8), GOLD_D, 0.012)
    m.lathe([(0.0, 0.78), (0.03, 0.8), (0.045, 0.84), (0.0, 0.86)], GOLD, seg=10, center=(0.19, -0.08, 0))
    m.ico((0.19, -0.08, 0.89), (0.03, 0.03, 0.04), HOLY, glow=1.0, ao=False)
    for k in range(4):
        a = 2 * math.pi * k / 4 + 0.4
        m.tube([(0.19, -0.08, 0.89), (0.19 + math.cos(a) * 0.06, -0.08 + math.sin(a) * 0.06, 0.89 + 0.02)], [0.006, 0.0],
               HOLY, seg=4, glow=1.0, ao=False)
    m.anchors["head"] = (0, 0, 0.92)
    return m


# =========================================================================== enemies


def ghoul():
    m = Model("enemy_ghoul")
    base(m, 0.3, hero=False)
    skin, skin_d = hexcol(0x6F8A4E), hexcol(0x46592F)
    rag = hexcol(0x3A3028)
    # hunched: legs bent, torso tilted forward
    for s in (-1, 1):
        limb(m, [(s * 0.06, 0.04, 0.3), (s * 0.1, -0.06, 0.18), (s * 0.09, 0.02, 0.07)], [0.04, 0.032, 0.028], skin_d)
        m.sphere((s * 0.09, -0.02, 0.07), (0.035, 0.07, 0.025), skin_d, seg=8, rings=5)
        for k in (-1, 0, 1):
            m.tube([(s * 0.09 + k * 0.018, -0.07, 0.065), (s * 0.09 + k * 0.022, -0.105, 0.06)], [0.008, 0.0], BONE, seg=4)
    m.lathe([((0.1, 0.08), 0.26), ((0.11, 0.09), 0.32)], rag, seg=12, cap_top=False, jitter=0.008)
    m.lathe([((0.09, 0.07), 0.28), ((0.1, 0.08), 0.36), ((0.14, 0.11), 0.46), ((0.12, 0.1), 0.52), ((0.05, 0.05), 0.55), (0, 0.555)],
            skin, seg=14, rot=rot(x=-28), center=(0, -0.0, 0.0))
    # ribs showing
    for k in range(4):
        z = 0.38 + k * 0.035
        m.tube([(-0.09, -0.06 - k * 0.022, z), (0, -0.12 - k * 0.022, z + 0.01), (0.09, -0.06 - k * 0.022, z)],
               [0.008] * 3, BONE, seg=4)
    # spine ridge
    for k in range(5):
        m.sphere((0, 0.08 + k * 0.01, 0.4 + k * 0.035), 0.02, BONE, seg=6, rings=4)
    head = (0, -0.16, 0.53)
    m.sphere(head, (0.075, 0.08, 0.07), skin, seg=12, rings=8)
    m.sphere((0, -0.2, 0.495), (0.055, 0.05, 0.03), skin_d, seg=10, rings=6)  # jaw
    for k in range(5):
        m.tube([(-0.03 + k * 0.015, -0.235, 0.51), (-0.03 + k * 0.015, -0.24, 0.49)], [0.006, 0.0], BONE, seg=4)
    eyes(m, head, 0.075, hexcol(0x9CFF7A), glow=1.0, spread=0.42, size=0.2)
    m.anchors["head"] = (0, 0, 0.62)
    # long arms hanging with claws
    for s in (-1, 1):
        sh = (s * 0.14, -0.06, 0.48)
        hand = (s * 0.2, -0.2, 0.2)
        limb(m, [sh, (s * 0.21, -0.08, 0.34), hand], [0.035, 0.028, 0.024], skin)
        m.sphere(hand, 0.03, skin_d, seg=8, rings=6)
        for k in range(3):
            m.tube([hand, (hand[0] + s * 0.01 * k, hand[1] - 0.05, hand[2] - 0.06 + 0.02 * k)], [0.009, 0.0], BONE, seg=4)
    # hanging rags
    m.extrude([(-0.08, 0), (0.08, 0), (0.06, -0.12), (0.03, -0.08), (0.0, -0.14), (-0.04, -0.09), (-0.07, -0.13)],
              0.01, rag, center=(0, -0.09, 0.3), rot=rot(x=-10))
    return m


def imp():
    m = Model("enemy_imp")
    base(m, 0.26, hero=False)
    red, red_d = hexcol(0xD2502A), hexcol(0x8E2E18)
    # squat legs with hooves
    for s in (-1, 1):
        limb(m, [(s * 0.05, 0.0, 0.2), (s * 0.08, -0.04, 0.13), (s * 0.07, 0.02, 0.07)], [0.035, 0.028, 0.022], red_d)
        m.cyl((s * 0.07, 0.01, 0.07), 0.025, 0.03, 0.03, BLACK, seg=8)
    # pot belly body
    m.sphere((0, 0.0, 0.27), (0.1, 0.09, 0.1), red, seg=14, rings=10)
    m.sphere((0, -0.04, 0.24), (0.075, 0.06, 0.07), hexcol(0xE8845A), seg=12, rings=8)
    # big head with horns and a grin
    head = (0, -0.02, 0.42)
    m.sphere(head, (0.095, 0.085, 0.085), red, seg=14, rings=10)
    for s in (-1, 1):
        horn(m, (s * 0.06, 0.0, 0.48), (s * 0.4, 0.2, 0.6), (s * 0.05, 0.08, -0.05), 0.12, 0.022, hexcol(0x2A1A14))
        # pointed ears
        m.tube([(s * 0.085, 0.0, 0.43), (s * 0.15, 0.03, 0.47)], [0.02, 0.0], red, seg=5, flat=(1, 0.4))
    eyes(m, head, 0.09, hexcol(0xFFE34A), glow=1.0, spread=0.4, size=0.22, height=0.15)
    m.extrude([(-0.05, 0), (0.05, 0), (0.035, -0.02), (0.0, -0.028), (-0.035, -0.02)], 0.01, BLACK,
              center=(0, -0.1, 0.4), rot=rot(x=0))
    for k in (-1, 1):
        m.tube([(k * 0.025, -0.103, 0.4), (k * 0.025, -0.105, 0.385)], [0.006, 0.0], WHITE, seg=4)
    m.anchors["head"] = (0, 0, 0.56)
    # wings and tail
    for s in (-1, 1):
        wing(m, (s * 0.05, 0.07, 0.33), s, red_d, hexcol(0x5A1E14), span=0.25, height=0.18, back=0.06)
    m.tube([(0, 0.08, 0.22), (0, 0.18, 0.15), (0.05, 0.25, 0.2), (0.08, 0.26, 0.27)], [0.018, 0.014, 0.01, 0.004], red_d,
           seg=6)
    m.extrude([(0, 0.03), (0.025, -0.02), (-0.025, -0.02)], 0.008, BLACK, center=(0.08, 0.26, 0.28), rot=rot(z=90))
    # arms, one hurling a fireball
    for s, hand in ((-1, (-0.13, -0.08, 0.27)), (1, (0.14, -0.06, 0.42))):
        limb(m, [(s * 0.08, 0.0, 0.33), (s * 0.13, -0.02, 0.32), hand], [0.025, 0.02, 0.018], red)
        m.sphere(hand, 0.022, red_d, seg=6, rings=4)
    m.ico((0.15, -0.08, 0.48), 0.05, EMBER, subdiv=1, glow=1.0, ao=False)
    m.ico((0.15, -0.08, 0.5), (0.035, 0.035, 0.06), HELLFIRE, subdiv=1, glow=1.0, ao=False)
    return m


def hellknight():
    m = Model("enemy_hellknight")
    base(m, 0.34, hero=False)
    armor, armor_d = hexcol(0x2C2A30), hexcol(0x1A181D)
    lava = hexcol(0xFF4A12)
    head = humanoid(m, torso=armor, arms=armor_d, legs=armor_d, boots=armor, belt=hexcol(0x5A1A12), skin=armor,
                    chest_w=0.19, waist_w=0.13, shoulder_x=0.2, head_r=0.085, head_z=0.8, stance=0.1, hip=0.4,
                    shoulder_z=0.64, torso_top=0.69, left_hand=(-0.22, -0.1, 0.42), right_hand=(0.22, -0.14, 0.52),
                    elbow_out=0.07, face=False)
    # glowing seams in the breastplate
    for k in range(3):
        z = 0.5 + 0.05 * k
        m.tube([(-0.1, -0.13, z), (0, -0.15, z - 0.02), (0.1, -0.13, z)], [0.007] * 3, lava, seg=4, glow=1.0, ao=False)
    m.box((0, -0.15, 0.6), (0.05, 0.02, 0.05), lava, glow=1.0, rot=rot(y=45))
    for s in (-1, 1):
        pauldron(m, s, armor, hexcol(0x6A1A12), z=0.64, x=0.21, r=0.1, spikes=3, spike_color=hexcol(0x3A2A24))
    # tasset plates
    for s in (-1, 0, 1):
        m.box((s * 0.075, -0.12, 0.34), (0.065, 0.02, 0.1), armor, bevel=0.008, rot=rot(x=-8))
    # helm: a closed bucket with a horn crown and a burning visor
    m.lathe([(0.095, 0.72), (0.1, 0.8), (0.09, 0.87), (0.05, 0.9), (0, 0.905)], armor, seg=16, cap_bottom=False)
    m.box((0, -0.096, 0.8), (0.12, 0.02, 0.018), lava, glow=1.0, ao=False)
    for s in (-1, 1):
        horn(m, (s * 0.08, 0.0, 0.86), (s * 0.6, 0.1, 0.5), (s * 0.02, 0.05, 0.05), 0.2, 0.03, hexcol(0x3A2A24))
    # tattered cape
    cape(m, hexcol(0x5A1A12), top_z=0.68, bottom_z=0.12, width=0.18, flare=0.27, back=0.08)
    # huge cleaver resting down to the right
    sword(m, (0.22, -0.14, 0.52), (0.05, -0.25, -0.97), length=0.42, width=0.07, guard=0.12, blade=hexcol(0x3E3A40),
          hilt=hexcol(0x6A1A12))
    m.tube([(0.24, -0.2, 0.42), (0.27, -0.25, 0.2), (0.27, -0.26, 0.08)], [0.006] * 3, lava, seg=4, glow=1.0, ao=False)
    m.anchors["head"] = (0, 0, 1.0)
    return m


def cultist():
    m = Model("enemy_cultist")
    base(m, 0.3, hero=False)
    robe, robe_d = hexcol(0x2A1A3A), hexcol(0x1A0F26)
    head = humanoid(m, torso=robe, arms=robe, robe=(robe_d, 0.2), belt=hexcol(0x8C1E2A), chest_w=0.13, waist_w=0.1,
                    shoulder_x=0.145, head_r=0.078, head_z=0.67, skin=hexcol(0xC9B8A8),
                    left_hand=(-0.08, -0.16, 0.45), right_hand=(0.08, -0.16, 0.45), elbow_out=0.05, face=False)
    # deep pointed hood
    m.lathe([(0.11, 0.58), (0.115, 0.66), (0.1, 0.74), (0.07, 0.8), (0.03, 0.86), (0, 0.9)], robe, seg=16,
            center=(0, 0.015, 0), arc=(40, 320))
    m.sphere((0, -0.06, 0.67), (0.07, 0.04, 0.065), BLACK, seg=10, rings=6, ao=False)
    # bone-white mask with a slit
    m.lathe([((0.06, 0.04), 0.63), ((0.065, 0.045), 0.69), ((0.055, 0.04), 0.73)], WHITE, seg=12,
            center=(0, -0.055, 0), cap_top=False, cap_bottom=False)
    m.box((0, -0.098, 0.685), (0.06, 0.01, 0.01), hexcol(0xFF3A5A), glow=1.0, ao=False)
    # a sigil on the chest and a candle-lit grimoire held out
    m.extrude([(math.cos(a) * 0.035, math.sin(a) * 0.035) for a in [2 * math.pi * k / 5 + math.pi / 2 for k in (0, 2, 4, 1, 3)]],
              0.006, hexcol(0xFF3A5A), center=(0, -0.13, 0.5), glow=0.8, ao=False)
    m.box((0, -0.19, 0.46), (0.12, 0.08, 0.02), hexcol(0x3A2418), bevel=0.005, rot=rot(x=-10))
    m.cyl((0.03, -0.2, 0.5), 0.01, 0.01, 0.05, WHITE, seg=6)
    m.ico((0.03, -0.2, 0.535), (0.01, 0.01, 0.02), hexcol(0xFF6A8A), glow=1.0, ao=False)
    # sash
    m.extrude([(-0.03, 0), (0.03, 0), (0.025, -0.25), (-0.025, -0.25)], 0.008, hexcol(0x8C1E2A), center=(0.05, -0.12, 0.35),
              rot=rot(x=-14, z=-8))
    m.anchors["head"] = (0, 0, 0.9)
    return m


def hellhound():
    m = Model("enemy_hellhound")
    base(m, 0.32, hero=False)
    hide, hide_d = hexcol(0x3A2620), hexcol(0x24160F)
    fire = hexcol(0xFF6A1F)
    # body along -Y (head forward)
    m.tube([(0, 0.17, 0.3), (0, 0.06, 0.33), (0, -0.06, 0.35), (0, -0.13, 0.36)], [0.07, 0.085, 0.095, 0.08], hide,
           seg=12, flat=(0.85, 1.0))
    # ember cracks along the flanks
    for s in (-1, 1):
        for k in range(3):
            y = 0.1 - k * 0.08
            m.tube([(s * 0.075, y + 0.03, 0.36), (s * 0.085, y, 0.33), (s * 0.08, y - 0.02, 0.3)], [0.006] * 3, fire, seg=4,
                   glow=1.0, ao=False)
    # legs, front pair straight, back pair crouched
    for s in (-1, 1):
        limb(m, [(s * 0.06, -0.1, 0.32), (s * 0.07, -0.13, 0.18), (s * 0.065, -0.14, 0.07)], [0.035, 0.026, 0.022], hide_d)
        limb(m, [(s * 0.06, 0.15, 0.3), (s * 0.08, 0.2, 0.2), (s * 0.07, 0.14, 0.12), (s * 0.065, 0.17, 0.07)],
             [0.04, 0.03, 0.024, 0.022], hide_d)
        for y in (-0.15, 0.16):
            m.sphere((s * 0.065, y - 0.015, 0.068), (0.028, 0.04, 0.016), BLACK, seg=8, rings=5)
    # spines down the back
    for k in range(6):
        y = 0.16 - k * 0.06
        m.tube([(0, y, 0.36 + 0.01 * k), (0, y + 0.03, 0.43 + 0.012 * k)], [0.018, 0.0], hexcol(0x2A1A14), seg=5)
    # head with snout, open jaw and a fire throat
    head = (0, -0.2, 0.4)
    m.sphere(head, (0.07, 0.075, 0.065), hide, seg=12, rings=8)
    m.tube([(0, -0.24, 0.4), (0, -0.31, 0.39)], [0.045, 0.032], hide, seg=10)
    m.tube([(0, -0.23, 0.355), (0, -0.3, 0.34)], [0.035, 0.024], hide_d, seg=8, flat=(1, 0.55))
    m.sphere((0, -0.27, 0.37), (0.03, 0.04, 0.015), fire, seg=8, rings=5, glow=1.0, ao=False)
    for s in (-1, 1):
        for k in range(3):
            m.tube([(s * 0.025, -0.27 - k * 0.012, 0.38), (s * 0.025, -0.27 - k * 0.012, 0.365)], [0.005, 0.0], WHITE, seg=4)
        m.tube([(s * 0.04, -0.18, 0.45), (s * 0.06, -0.15, 0.52)], [0.022, 0.0], hide_d, seg=5, flat=(1, 0.5))
        horn(m, (s * 0.035, -0.19, 0.46), (s * 0.3, 0.6, 0.5), (0, 0.05, -0.1), 0.09, 0.014, BONE)
    eyes(m, (0, -0.215, 0.41), 0.07, hexcol(0xFFD24A), glow=1.0, spread=0.55, size=0.2, height=0.15)
    # tail of flame
    m.tube([(0, 0.2, 0.3), (0, 0.27, 0.33), (0.03, 0.31, 0.4)], [0.025, 0.018, 0.0], hide, seg=6)
    m.ico((0.03, 0.31, 0.42), (0.025, 0.025, 0.045), fire, glow=1.0, ao=False)
    m.anchors["head"] = (0, 0, 0.55)
    return m


def rift_caller():
    m = Model("enemy_riftcaller")
    base(m, 0.3, hero=False)
    robe, robe_d = hexcol(0x183844), hexcol(0x0E222A)
    cyan = hexcol(0x5FE6FF)
    # floating robe with tendrils instead of legs
    m.lathe([((0.13, 0.11), 0.2), ((0.11, 0.09), 0.3), ((0.1, 0.08), 0.42), ((0.15, 0.11), 0.58), ((0.13, 0.1), 0.63),
             ((0.06, 0.05), 0.66), (0, 0.665)], robe, seg=16)
    for k in range(7):
        a = 2 * math.pi * k / 7
        r0 = (math.cos(a) * 0.1, math.sin(a) * 0.08, 0.22)
        r1 = (math.cos(a + 0.4) * 0.16, math.sin(a + 0.4) * 0.14, 0.12)
        r2 = (math.cos(a + 0.9) * 0.18, math.sin(a + 0.9) * 0.16, 0.065)
        m.tube([r0, r1, r2], [0.03, 0.018, 0.0], robe_d, seg=6)
    # floating stones under the hem
    for k in range(3):
        a = 2 * math.pi * k / 3 + 0.5
        m.ico((math.cos(a) * 0.07, math.sin(a) * 0.07, 0.14), 0.025, hexcol(0x2A3A44), subdiv=0)
    m.ico((0, 0, 0.15), (0.04, 0.04, 0.06), RIFT, glow=1.0, ao=False)
    # wide sleeves raised, channeling
    for s in (-1, 1):
        m.tube([(s * 0.13, 0, 0.57), (s * 0.21, -0.04, 0.62), (s * 0.25, -0.08, 0.7)], [0.04, 0.05, 0.065], robe, seg=10)
        m.ico((s * 0.26, -0.1, 0.74), 0.03, cyan, glow=1.0, ao=False)
    # hood with a void face and a third eye
    m.lathe([(0.11, 0.6), (0.115, 0.68), (0.1, 0.75), (0.07, 0.8), (0.0, 0.83)], robe, seg=16, center=(0, 0.01, 0),
            arc=(40, 320))
    m.sphere((0, -0.06, 0.69), (0.07, 0.04, 0.065), BLACK, seg=10, rings=6, ao=False)
    eyes(m, (0, 0.0, 0.69), 0.075, cyan, glow=1.0, spread=0.35, size=0.12)
    m.sphere((0, -0.085, 0.735), 0.012, RIFT, seg=6, rings=4, glow=1.0, ao=False)
    # a rift tear held between the hands: a ring of shards around a purple core
    m.ico((0, -0.16, 0.8), (0.05, 0.02, 0.07), RIFT, subdiv=1, glow=1.0, ao=False)
    for k in range(8):
        a = 2 * math.pi * k / 8
        m.ico((math.cos(a) * 0.1, -0.16, 0.8 + math.sin(a) * 0.1), (0.012, 0.012, 0.03), hexcol(0x9AF0FF), subdiv=0,
              rot=rot(y=-math.degrees(a)), glow=0.8, ao=False)
    m.anchors["head"] = (0, 0, 0.92)
    return m


def rift_lord():
    m = Model("enemy_riftlord")
    base(m, 0.44, hero=False, rim=hexcol(0xB46CFF))
    hide, hide_d = hexcol(0x3A1E2E), hexcol(0x24111C)
    armor = hexcol(0x1C1A22)
    s_ = 1.45  # scale of the demon relative to a hero
    sub = Model("lord")
    head = humanoid(sub, skin=hide, torso=hide, arms=hide, legs=hide_d, boots=BLACK, belt=armor, chest_w=0.19,
                    waist_w=0.12, shoulder_x=0.2, head_r=0.075, head_z=0.73, stance=0.1, hip=0.38,
                    left_hand=(-0.27, -0.1, 0.4), right_hand=(0.26, -0.12, 0.62), elbow_out=0.08, face=False)
    # muscle plates and the rift crystal in the chest
    for s in (-1, 1):
        sub.sphere((s * 0.075, -0.1, 0.53), (0.075, 0.05, 0.06), hide, seg=10, rings=6)
        pauldron(sub, s, armor, hexcol(0xB46CFF), z=0.6, x=0.21, r=0.09, spikes=3, spike_color=BONE)
    sub.ico((0, -0.14, 0.5), (0.04, 0.03, 0.06), RIFT, subdiv=1, glow=1.0, ao=False)
    for k in range(5):
        a = math.radians(-60 + 30 * k + 90)
        sub.tube([(0, -0.14, 0.5), (math.cos(a) * 0.11, -0.12, 0.5 + math.sin(a) * 0.09)], [0.008, 0.0], RIFT, seg=4,
                 glow=1.0, ao=False)
    # skull-like head with a jaw, great ram horns and a burning crown
    sub.sphere((0, -0.03, 0.73), (0.08, 0.085, 0.08), hexcol(0x4A2A38), seg=12, rings=8)
    sub.sphere((0, -0.075, 0.69), (0.06, 0.05, 0.04), hexcol(0x2A1620), seg=10, rings=6)
    for k in range(6):
        sub.tube([(-0.035 + k * 0.014, -0.115, 0.7), (-0.035 + k * 0.014, -0.12, 0.68)], [0.006, 0.0], BONE, seg=4)
    eyes(sub, (0, -0.03, 0.74), 0.08, hexcol(0xD9A0FF), glow=1.0, spread=0.42, size=0.2)
    for s in (-1, 1):
        horn(sub, (s * 0.06, -0.01, 0.79), (s * 0.7, 0.3, 0.4), (s * 0.0, 0.1, -0.22), 0.3, 0.035, hexcol(0x2A1A14),
             steps=9)
        horn(sub, (s * 0.03, -0.03, 0.8), (s * 0.15, -0.1, 0.9), (0, 0.02, 0.0), 0.09, 0.015, hexcol(0x2A1A14))
    for k in range(5):
        a = math.radians(200 + 35 * k)
        sub.ico((math.cos(a) * 0.05, -0.02 + math.sin(a) * 0.05, 0.83), (0.012, 0.012, 0.03), HELLFIRE, subdiv=0,
                glow=1.0, ao=False)
    # wings
    for s in (-1, 1):
        wing(sub, (s * 0.07, 0.1, 0.6), s, hide_d, hexcol(0x3A1450), span=0.5, height=0.36, back=0.12, glow=0.12)
    # loincloth, tail
    sub.extrude([(-0.07, 0), (0.07, 0), (0.05, -0.2), (0, -0.24), (-0.05, -0.2)], 0.01, hexcol(0x3A1450),
                center=(0, -0.13, 0.38), rot=rot(x=-6))
    sub.tube([(0, 0.1, 0.32), (0.05, 0.25, 0.2), (0.1, 0.3, 0.1), (0.18, 0.28, 0.07)], [0.03, 0.022, 0.014, 0.0], hide,
             seg=8)
    # rift-blade greatsword raised in the right hand
    sword(sub, (0.26, -0.12, 0.62), (0.15, -0.15, 0.97), length=0.45, width=0.05, guard=0.12, blade=hexcol(0x2A1A3A),
          hilt=armor)
    sub.tube([(0.27, -0.13, 0.69), (0.31, -0.17, 0.95), (0.33, -0.19, 1.07)], [0.009, 0.008, 0.0], RIFT, seg=4,
             glow=1.0, ao=False)
    # a claw with rift flame in the left hand
    sub.ico((-0.27, -0.13, 0.43), 0.045, RIFT, subdiv=1, glow=1.0, ao=False)
    from mathutils import Matrix
    mtx = Matrix.Translation((0, 0, 0.05 * (1 - 1))) @ Matrix.Diagonal((s_, s_, s_, 1))
    # The base stays at the bottom: shift everything up so the scaled feet still touch it.
    mtx = Matrix.Translation((0, 0, 0.056 - 0.056 * s_)) @ mtx
    m.merge(sub, matrix=mtx)
    m.anchors["head"] = (0, 0, 0.85 * s_ + 0.15)
    return m


HEROES = {
    "barbarian": barbarian, "paladin": paladin, "rogue": rogue, "sorcerer": sorcerer, "hunter": hunter,
    "shaman": shaman, "priest": priest,
}
ENEMIES = {
    "ghoul": ghoul, "imp": imp, "hellknight": hellknight, "cultist": cultist, "hellhound": hellhound,
    "riftcaller": rift_caller, "riftlord": rift_lord,
}
