"""
Board pieces for the four rift themes, plus shared pieces (rift door, spikes, spawn crack, loot shard, the island
rock under the board and the distant backdrop). Every tile piece fills a 1 x 1 footprint centered on the origin with
the walkable surface at z = 0.
"""
import math
import random
import zlib

from kit import Model, hexcol, mix, rot, shade

THEMES = {
    "crypt": dict(
        floor=hexcol(0x4A4C57), floor2=hexcol(0x3B3D47), grout=hexcol(0x1C1D24), wall=hexcol(0x3A3B45),
        wall2=hexcol(0x2C2D36), trim=hexcol(0x5E6070), accent=hexcol(0x7FD4A0), moss=hexcol(0x3E5A3A),
        rock=hexcol(0x2A2B33), glow=0.0),
    "hellforge": dict(
        floor=hexcol(0x3C2A26), floor2=hexcol(0x2E211E), grout=hexcol(0xC8400F), wall=hexcol(0x2A1E1B),
        wall2=hexcol(0x1E1513), trim=hexcol(0x4A3430), accent=hexcol(0xFF7A3D), moss=hexcol(0x5A2A1A),
        rock=hexcol(0x22181A), glow=0.35),
    "cathedral": dict(
        floor=hexcol(0x3A3550), floor2=hexcol(0x262236), grout=hexcol(0x15121E), wall=hexcol(0x2E2A40),
        wall2=hexcol(0x201D2E), trim=hexcol(0x8A7A50), accent=hexcol(0xB58CFF), moss=hexcol(0x3A2A50),
        rock=hexcol(0x1E1B2A), glow=0.0),
    "wastes": dict(
        floor=hexcol(0x6A5A46), floor2=hexcol(0x544736), grout=hexcol(0x2A231B), wall=hexcol(0x5A4A3A),
        wall2=hexcol(0x45382B), trim=hexcol(0xB8A27A), accent=hexcol(0xFFC857), moss=hexcol(0x7A6A4E),
        rock=hexcol(0x3A3026), glow=0.0),
}

def _seed(*parts):
    return zlib.crc32(repr(parts).encode()) & 0xFFFF


RIFT = hexcol(0xB46CFF)
RIFT_HOT = hexcol(0xE6C2FF)
IRON = hexcol(0x3A3C44)
STEEL = hexcol(0x8E939E)
BONE = hexcol(0xD8CDB0)
WAX = hexcol(0xE8DFC8)


def _slab(m, cx, cy, w, d, top, color, rng, h=0.12, tilt=1.5, bevel=0.018):
    """One flagstone: a beveled box with a slightly random height and tilt."""
    dz = rng.uniform(-0.012, 0.004)
    m.box((cx, cy, top - h / 2 + dz), (w, d, h), color, bevel=bevel,
          rot=rot(x=rng.uniform(-tilt, tilt), y=rng.uniform(-tilt, tilt), z=rng.uniform(-2, 2)))


def floor(theme, variant):
    t = THEMES[theme]
    m = Model(f"floor_{theme}_{variant}")
    rng = random.Random(_seed('floor', theme, variant))
    # The grout bed under the stones. In the Hellforge it is molten.
    # Its top sits below the stones (which tilt a little), so it only shows in the gaps.
    m.box((0, 0, -0.1), (0.995, 0.995, 0.08), t["grout"], glow=t["glow"] * 0.9, ao=t["glow"] == 0)
    stones = []
    if variant == 0:
        stones = [(0, 0, 0.93, 0.93)]
    elif variant == 1:
        stones = [(-0.235, -0.235, 0.45, 0.45), (0.235, -0.235, 0.45, 0.45), (-0.235, 0.235, 0.45, 0.45),
                  (0.235, 0.235, 0.45, 0.45)]
    elif variant == 2:
        stones = [(-0.24, 0, 0.44, 0.93), (0.235, -0.24, 0.45, 0.44), (0.235, 0.24, 0.45, 0.44)]
    else:
        stones = [(0, -0.31, 0.93, 0.29), (-0.235, 0.155, 0.45, 0.6), (0.235, 0.155, 0.45, 0.6)]
    for i, (x, y, w, d) in enumerate(stones):
        c = mix(t["floor"], t["floor2"], rng.random() * 0.8)
        _slab(m, x, y, w, d, 0.0, c, rng)
    if theme == "cathedral" and variant == 0:
        # an inlaid gold-and-violet rosette on the big slab
        for k in range(8):
            a = 2 * math.pi * k / 8
            m.box((math.cos(a) * 0.2, math.sin(a) * 0.2, 0.001), (0.12, 0.03, 0.01), t["trim"], rot=rot(z=math.degrees(a)))
        m.cyl((0, 0, 0.001), 0.09, 0.09, 0.01, t["accent"], seg=16, glow=0.35, smooth=False)
    if theme == "crypt":
        for k in range(rng.randint(1, 3)):
            m.ico((rng.uniform(-0.4, 0.4), rng.uniform(-0.4, 0.4), 0.0), (0.08, 0.06, 0.012), t["moss"], subdiv=1,
                  jitter=0.01, seed=k)
    if theme == "wastes":
        for k in range(rng.randint(2, 4)):
            m.ico((rng.uniform(-0.42, 0.42), rng.uniform(-0.42, 0.42), 0.0), (0.12, 0.09, 0.012), t["moss"], subdiv=1,
                  jitter=0.015, seed=k + 9)
    if variant == 3 or (variant == 2 and theme in ("crypt", "wastes")):
        # rubble
        for k in range(3):
            s = rng.uniform(0.025, 0.05)
            m.ico((rng.uniform(-0.38, 0.38), rng.uniform(-0.38, 0.38), s * 0.4), (s, s * 0.9, s * 0.7),
                  shade(t["floor"], 0.85), subdiv=0, jitter=0.006, seed=k + 30)
    if theme == "hellforge" and variant in (0, 3):
        # a glowing fissure across the stone
        pts = [(-0.4, rng.uniform(-0.3, 0.3), 0.002)]
        for k in range(4):
            pts.append((pts[-1][0] + 0.2, pts[-1][1] + rng.uniform(-0.12, 0.12), 0.002))
        m.tube(pts, [0.012] * len(pts), hexcol(0xFF6A1F), seg=4, glow=1.0, ao=False, flat=(1, 0.3))
    return m


def _bricks(m, t, rng, x0, x1, y0, y1, z0, z1, course=0.16, broken_top=True):
    """A block of staggered bricks filling a box, with a ragged top course."""
    z = z0
    row = 0
    while z < z1 - 0.01:
        h = min(course, z1 - z)
        last = z + h >= z1 - 0.01
        w = 0.34 if row % 2 == 0 else 0.26
        x = x0 + (0 if row % 2 == 0 else -0.13)
        while x < x1 - 0.01:
            xa, xb = max(x, x0), min(x + w, x1)
            if xb - xa > 0.04:
                if last and broken_top and rng.random() < 0.3:
                    x += w
                    continue
                hh = h * (rng.uniform(0.55, 1.0) if last and broken_top else 1.0)
                c = mix(t["wall"], t["wall2"], rng.random())
                m.box(((xa + xb) / 2, (y0 + y1) / 2, z + hh / 2), (xb - xa - 0.012, y1 - y0 - rng.uniform(0, 0.02), hh - 0.012),
                      c, bevel=0.012)
            x += w
        z += h
        row += 1


def wall(theme, variant):
    t = THEMES[theme]
    m = Model(f"wall_{theme}_{variant}")
    rng = random.Random(_seed('wall', theme, variant))
    height = 1.0 if variant == 0 else 0.82
    # mortar core so there are no gaps to see through
    m.box((0, 0, height / 2 - 0.08), (0.94, 0.94, height - 0.12), t["grout"] if t["glow"] == 0 else shade(t["wall2"], 0.6),
          ao=True)
    _bricks(m, t, rng, -0.5, 0.5, -0.5, 0.5, -0.12, height, course=0.17 if theme != "wastes" else 0.21)
    if theme == "crypt":
        # a skull niche on the front face and moss on top
        m.box((0, -0.47, 0.42), (0.26, 0.08, 0.22), t["grout"], bevel=0.02)
        m.sphere((0, -0.47, 0.39), (0.06, 0.055, 0.06), BONE, seg=10, rings=7)
        for s in (-1, 1):
            m.sphere((s * 0.022, -0.52, 0.4), 0.014, hexcol(0x101010), seg=6, rings=4, ao=False)
        for k in range(3):
            m.ico((rng.uniform(-0.35, 0.35), rng.uniform(-0.35, 0.35), height - 0.02), (0.12, 0.1, 0.03), t["moss"],
                  subdiv=1, jitter=0.02, seed=k)
    elif theme == "hellforge":
        # glowing seams between courses
        for z in (0.22, 0.56):
            m.box((0, -0.502, z), (0.9, 0.01, 0.012), t["grout"], glow=1.0, ao=False)
            m.box((-0.502, 0, z + 0.08), (0.01, 0.9, 0.012), t["grout"], glow=1.0, ao=False)
        if variant == 0:
            for s in (-1, 1):
                m.tube([(s * 0.3, -0.45, height), (s * 0.34, -0.4, height + 0.18)], [0.04, 0.0], hexcol(0x1A1210), seg=5)
    elif theme == "cathedral":
        # a gothic pointed window with violet glass on the front, trimmed in old gold
        arch = [(-0.13, 0.0), (0.13, 0.0), (0.13, 0.34), (0.08, 0.46), (0.0, 0.52), (-0.08, 0.46), (-0.13, 0.34)]
        m.extrude([(x * 1.25, z * 1.12 - 0.03) for (x, z) in arch], 0.04, t["trim"], center=(0, -0.49, 0.2))
        m.extrude(arch, 0.05, t["accent"], center=(0, -0.495, 0.2), glow=0.55, ao=False)
        m.box((0, -0.525, 0.38), (0.012, 0.01, 0.5), t["trim"])
        m.box((0, -0.525, 0.45), (0.26, 0.01, 0.012), t["trim"])
        # a gilded string course along the front
        m.box((0, -0.5, 0.16), (0.96, 0.03, 0.03), t["trim"], bevel=0.006)
    elif theme == "wastes":
        # a horned skull fixed to the wall and wind-cut tops
        m.sphere((0, -0.5, 0.5), (0.07, 0.06, 0.07), BONE, seg=10, rings=7)
        for s in (-1, 1):
            m.tube([(s * 0.05, -0.5, 0.55), (s * 0.13, -0.53, 0.6), (s * 0.17, -0.56, 0.53)], [0.022, 0.016, 0.0], BONE,
                   seg=6)
            m.sphere((s * 0.025, -0.555, 0.51), 0.014, hexcol(0x101010), seg=6, rings=4, ao=False)
        for k in range(4):
            m.ico((rng.uniform(-0.4, 0.4), rng.uniform(-0.4, 0.4), height - 0.03), (0.14, 0.11, 0.04), t["moss"], subdiv=1,
                  jitter=0.02, seed=k)
    return m


def pillar(theme):
    t = THEMES[theme]
    m = Model(f"pillar_{theme}")
    m.box((0, 0, 0.04), (0.7, 0.7, 0.08), t["trim"] if theme == "cathedral" else t["wall"], bevel=0.02)
    m.box((0, 0, 0.11), (0.6, 0.6, 0.06), t["wall2"], bevel=0.015)
    if theme == "hellforge":
        # an obsidian spike wrapped in a glowing chain of runes
        m.lathe([(0.24, 0.12), (0.22, 0.4), (0.16, 0.8), (0.08, 1.1), (0.0, 1.25)], hexcol(0x18121A), seg=7, smooth=False)
        for k in range(5):
            a = k * 1.3
            m.box((math.cos(a) * 0.2, math.sin(a) * 0.2, 0.3 + k * 0.13), (0.05, 0.02, 0.05), t["grout"], glow=1.0,
                  rot=rot(z=math.degrees(a)), ao=False)
    elif theme == "wastes":
        # a bone totem: stacked vertebrae and a ribcage crown
        m.lathe([(0.12, 0.12), (0.1, 0.9)], hexcol(0x6A5038), seg=8)
        for k in range(5):
            m.cyl((0, 0, 0.25 + k * 0.15), 0.15, 0.13, 0.08, BONE, seg=10)
        for k in range(6):
            a = 2 * math.pi * k / 6
            m.tube([(math.cos(a) * 0.1, math.sin(a) * 0.1, 0.9), (math.cos(a) * 0.24, math.sin(a) * 0.24, 1.0),
                    (math.cos(a) * 0.16, math.sin(a) * 0.16, 1.15)], [0.025, 0.02, 0.0], BONE, seg=5)
        m.sphere((0, -0.02, 1.0), (0.09, 0.08, 0.09), BONE, seg=10, rings=7)
    else:
        # a broken column with fluting
        top = 1.05 if theme == "cathedral" else 0.85
        prof = [(0.22, 0.14), (0.2, 0.2), (0.19, top - 0.1), (0.21, top - 0.05), (0.0, top)]
        m.lathe(prof, t["trim"] if theme == "cathedral" else hexcol(0x6A6C78), seg=12, smooth=False)
        for k in range(12):
            a = 2 * math.pi * (k + 0.5) / 12
            m.box((math.cos(a) * 0.195, math.sin(a) * 0.195, (top + 0.1) / 2), (0.02, 0.02, top - 0.3),
                  shade(t["trim"] if theme == "cathedral" else hexcol(0x6A6C78), 0.75), rot=rot(z=math.degrees(a)))
        # the broken top: jagged chunks
        rng = random.Random(7)
        for k in range(5):
            a = 2 * math.pi * k / 5
            m.ico((math.cos(a) * 0.12, math.sin(a) * 0.12, top + rng.uniform(0.0, 0.06)), (0.09, 0.08, 0.08),
                  t["trim"] if theme == "cathedral" else hexcol(0x6A6C78), subdiv=0, jitter=0.01, seed=k)
        if theme == "cathedral":
            m.ico((0.05, 0.0, top + 0.12), (0.06, 0.06, 0.16), t["accent"], subdiv=0, glow=0.9, ao=False)
        else:
            m.ico((0.27, -0.25, 0.05), (0.1, 0.08, 0.06), hexcol(0x6A6C78), subdiv=0, jitter=0.01)
    return m


def spikes():
    m = Model("spikes")
    m.box((0, 0, -0.01), (0.86, 0.86, 0.03), IRON, bevel=0.01)
    for k in range(5):
        m.box((-0.36 + k * 0.18, 0, 0.01), (0.03, 0.82, 0.02), shade(IRON, 0.7))
    rng = random.Random(3)
    for i in range(3):
        for j in range(3):
            x, y = -0.26 + i * 0.26, -0.26 + j * 0.26
            h = rng.uniform(0.16, 0.24)
            m.cyl((x, y, h / 2), 0.045, 0.0, h, STEEL, seg=5, smooth=False)
            m.cyl((x, y, 0.06), 0.03, 0.0, 0.06, hexcol(0x8A2020), seg=5, smooth=False, rot=rot(x=180))
    return m


def exit_door(open_):
    """The rift door: a ring of floating shards around a swirling core, on a carved dais."""
    m = Model("exit_open" if open_ else "exit_closed")
    core = hexcol(0x8CFFC0) if open_ else RIFT
    hot = hexcol(0xE0FFE8) if open_ else RIFT_HOT
    m.cyl((0, 0, 0.02), 0.46, 0.44, 0.05, hexcol(0x2A2632), seg=20, smooth=False)
    m.cyl((0, 0, 0.05), 0.36, 0.36, 0.012, core, seg=20, glow=0.8 if open_ else 0.45, smooth=False, ao=False)
    for k in range(10):
        a = 2 * math.pi * k / 10
        m.box((math.cos(a) * 0.41, math.sin(a) * 0.41, 0.06), (0.07, 0.03, 0.02), hot, rot=rot(z=math.degrees(a)),
              glow=0.7, ao=False)
    # an upright oval portal standing at the back of the tile, facing the board
    pts = []
    for k in range(25):
        a = 2 * math.pi * k / 24
        pts.append((math.cos(a) * 0.3, 0.12, 0.5 + math.sin(a) * 0.42))
    m.tube(pts, [0.035] * 25, hexcol(0x3A3446), seg=6, cap=False)
    disc = [(math.cos(2 * math.pi * k / 20) * 0.27, 0.45 + math.sin(2 * math.pi * k / 20) * 0.39) for k in range(20)]
    m.extrude(disc, 0.02, core, center=(0, 0.12, 0.05), glow=1.0, ao=False)
    m.extrude([(x * 0.55, (z - 0.45) * 0.55 + 0.45) for (x, z) in disc], 0.03, hot, center=(0, 0.12, 0.05), glow=1.0, ao=False)
    for k in range(9):
        a = 2 * math.pi * k / 9 + 0.3
        r = 0.4
        m.ico((math.cos(a) * r, 0.12 + (0.05 if k % 2 else -0.05), 0.5 + math.sin(a) * r * 1.25), (0.03, 0.03, 0.07),
              hot, subdiv=0, rot=rot(y=-math.degrees(a)), glow=0.9, ao=False)
    return m


def spawn_crack():
    """A burning tear in the floor where reinforcements climb through."""
    m = Model("spawn_crack")
    rng = random.Random(11)
    for b in range(3):
        a = rng.uniform(0, math.pi)
        pts = []
        for k in range(6):
            t = k / 5 - 0.5
            pts.append((math.cos(a) * t * 0.8 + rng.uniform(-0.04, 0.04), math.sin(a) * t * 0.8 + rng.uniform(-0.04, 0.04),
                        0.004))
        radii = [0.0, 0.025, 0.04, 0.04, 0.025, 0.0]
        m.tube(pts, radii, hexcol(0xFF3A2A), seg=4, flat=(1, 0.2), glow=1.0, ao=False, smooth=False)
    for k in range(6):
        a = 2 * math.pi * k / 6
        m.box((math.cos(a) * 0.38, math.sin(a) * 0.38, 0.003), (0.08, 0.02, 0.006), hexcol(0xFF6A3A),
              rot=rot(z=math.degrees(a) + 90), glow=0.6, ao=False)
    return m


def loot_shard():
    """A floating crystal, white so the game can tint it by rarity."""
    m = Model("loot_shard")
    m.ico((0, 0, 0.0), (0.07, 0.07, 0.15), hexcol(0xFFFFFF), subdiv=0, glow=0.75, ao=False, rot=rot(z=20))
    for k in range(3):
        a = 2 * math.pi * k / 3
        m.ico((math.cos(a) * 0.09, math.sin(a) * 0.09, -0.04), (0.025, 0.025, 0.06), hexcol(0xFFFFFF), subdiv=0,
              glow=0.6, ao=False, rot=rot(y=math.degrees(a) * 0.3))
    return m


def active_ring():
    m = Model("active_ring")
    pts = [(math.cos(2 * math.pi * k / 32) * 0.4, math.sin(2 * math.pi * k / 32) * 0.4, 0.012) for k in range(33)]
    m.tube(pts, [0.018] * 33, hexcol(0xFFFFFF), seg=5, flat=(1, 0.4), glow=1.0, ao=False, cap=False)
    for k in range(4):
        a = 2 * math.pi * k / 4 + math.pi / 4
        m.extrude([(0, 0.0), (0.05, -0.05), (-0.05, -0.05)], 0.01, hexcol(0xFFFFFF),
                  center=(math.cos(a) * 0.47, math.sin(a) * 0.47, 0.01),
                  rot=rot(z=math.degrees(a) - 90) @ rot(x=90), glow=1.0, ao=False)
    return m


def island_rock(theme, variant):
    """Rock hanging under one tile of the board, so the room reads as a broken island floating over the abyss."""
    t = THEMES[theme]
    m = Model(f"rock_{theme}_{variant}")
    rng = random.Random(_seed('rock', theme, variant))
    depth = 0.55 + 0.45 * variant
    m.box((0, 0, -0.21), (1.0, 1.0, 0.18), t["rock"], bevel=0.03, jitter=0.0)
    for k in range(4 + variant * 2):
        x, y = rng.uniform(-0.25, 0.25), rng.uniform(-0.25, 0.25)
        d = depth * rng.uniform(0.5, 1.0)
        m.cyl((x, y, -0.25 - d / 2), rng.uniform(0.25, 0.38), rng.uniform(0.02, 0.1), d, mix(t["rock"], t["wall2"], rng.random()),
              seg=6, rot=rot(x=180, z=rng.uniform(0, 60)), smooth=False, jitter=0.02, seed=k)
    if theme == "hellforge" or variant == 1:
        glow_c = t["grout"] if theme == "hellforge" else RIFT
        for k in range(2):
            x, y = rng.uniform(-0.3, 0.3), rng.uniform(-0.3, 0.3)
            m.ico((x, y, -0.4 - k * 0.2), (0.05, 0.05, 0.09), glow_c, subdiv=0, glow=1.0, ao=False)
    return m


def floating_rock(variant):
    m = Model(f"float_rock_{variant}")
    rng = random.Random(100 + variant)
    c = hexcol(0x2A2732)
    m.ico((0, 0, 0), (0.9, 0.8, 0.35), hexcol(0x3A3644), subdiv=1, jitter=0.12, seed=variant)
    for k in range(5):
        m.cyl((rng.uniform(-0.4, 0.4), rng.uniform(-0.4, 0.4), -0.6), rng.uniform(0.25, 0.45), 0.02, rng.uniform(0.6, 1.2), c,
              seg=6, rot=rot(x=180), smooth=False, jitter=0.04, seed=k)
    # broken masonry on top
    for k in range(3):
        m.box((rng.uniform(-0.4, 0.4), rng.uniform(-0.3, 0.3), 0.35), (0.3, 0.25, rng.uniform(0.2, 0.7)), hexcol(0x4A4656),
              bevel=0.03, rot=rot(z=rng.uniform(0, 90), x=rng.uniform(-8, 8)))
    m.ico((0.1, 0.0, -0.9), (0.08, 0.08, 0.16), RIFT, subdiv=0, glow=1.0, ao=False)
    return m


def spire(kind):
    """Distant silhouettes: a ruined gothic spire on the hell side, a gleaming tower on the heaven side."""
    m = Model(f"spire_{kind}")
    if kind == "dark":
        body, trim, lit = hexcol(0x1A1622), hexcol(0x2A2434), hexcol(0xFF3A3A)
    else:
        body, trim, lit = hexcol(0xD8D0BC), hexcol(0xE8C46A), hexcol(0xFFF0C0)
    m.ico((0, 0, -0.6), (2.2, 2.0, 1.0), hexcol(0x2A2732) if kind == "dark" else hexcol(0x6A6458), subdiv=1, jitter=0.25)
    m.lathe([(0.9, 0.0), (0.85, 1.5), (0.6, 1.6), (0.55, 3.2), (0.4, 3.4), (0.35, 4.6), (0.0, 6.2)], body, seg=8,
            smooth=False)
    for k in range(4):
        a = 2 * math.pi * k / 4 + 0.4
        m.lathe([(0.3, 0.0), (0.28, 1.0 + k * 0.4), (0.0, 2.2 + k * 0.5)], trim, seg=6,
                center=(math.cos(a) * 1.0, math.sin(a) * 1.0, 0), smooth=False)
    for z in (1.0, 2.2, 3.6):
        m.box((0, -0.6 if z < 3 else -0.4, z), (0.12, 0.05, 0.3), lit, glow=1.0, ao=False)
    return m


def rift_tear():
    """A huge jagged tear in the sky behind the board."""
    m = Model("rift_tear")
    rng = random.Random(5)
    pts = []
    for k in range(9):
        t = k / 8
        pts.append(((t - 0.5) * 6.0, 0, math.sin(t * math.pi) * 0.4 + rng.uniform(-0.3, 0.3)))
    radii = [0.0, 0.35, 0.6, 0.8, 0.9, 0.8, 0.6, 0.35, 0.0]
    m.tube(pts, radii, RIFT, seg=6, flat=(1, 0.08), glow=1.0, ao=False, smooth=False)
    m.tube([(x, -0.05, z) for (x, _, z) in pts], [r * 0.4 for r in radii], RIFT_HOT, seg=6, flat=(1, 0.08), glow=1.0, ao=False)
    for k in range(14):
        a = rng.uniform(0, 2 * math.pi)
        m.ico((rng.uniform(-3, 3), rng.uniform(-0.3, 0.3), rng.uniform(-1.4, 1.4)), (0.15, 0.15, 0.3), hexcol(0x2A2238),
              subdiv=0, rot=rot(x=math.degrees(a), y=rng.uniform(0, 90)), ao=False)
    return m


def brazier(theme):
    """A light source that sits on top of a wall: candles in the crypt, fire bowls in the forge, and so on."""
    t = THEMES[theme]
    m = Model(f"light_{theme}")
    flame = {"crypt": hexcol(0x8CFFB0), "hellforge": hexcol(0xFF8A2A), "cathedral": hexcol(0xD0A0FF),
             "wastes": hexcol(0xFFB040)}[theme]
    if theme == "crypt" or theme == "cathedral":
        rng = random.Random(1)
        for k in range(5):
            x, y = rng.uniform(-0.12, 0.12), rng.uniform(-0.12, 0.12)
            h = rng.uniform(0.06, 0.16)
            m.cyl((x, y, h / 2), 0.025, 0.022, h, WAX, seg=7)
            m.ico((x, y, h + 0.025), (0.014, 0.014, 0.03), flame, subdiv=0, glow=1.0, ao=False)
        if theme == "cathedral":
            m.cyl((0, 0, 0.005), 0.18, 0.16, 0.02, t["trim"], seg=12)
    else:
        m.lathe([(0.05, 0.0), (0.04, 0.12), (0.14, 0.18), (0.16, 0.24), (0.0, 0.2)], IRON, seg=10)
        for s in range(3):
            a = 2 * math.pi * s / 3
            m.tube([(math.cos(a) * 0.04, math.sin(a) * 0.04, 0.12), (math.cos(a) * 0.12, math.sin(a) * 0.12, 0.0)],
                   [0.012, 0.012], IRON, seg=5)
        m.ico((0, 0, 0.27), (0.11, 0.11, 0.1), flame, subdiv=1, glow=1.0, ao=False)
        m.ico((0, 0, 0.35), (0.06, 0.06, 0.12), hexcol(0xFFE0A0), subdiv=0, glow=1.0, ao=False)
    m.anchors["light"] = (0, 0, 0.3)
    return m


def build_list():
    """Every environment model, by file name."""
    out = {}
    for theme in THEMES:
        for v in range(4):
            out[f"floor_{theme}_{v}"] = (lambda th=theme, vv=v: floor(th, vv))
        for v in range(2):
            out[f"wall_{theme}_{v}"] = (lambda th=theme, vv=v: wall(th, vv))
            out[f"rock_{theme}_{v}"] = (lambda th=theme, vv=v: island_rock(th, vv))
        out[f"pillar_{theme}"] = (lambda th=theme: pillar(th))
        out[f"light_{theme}"] = (lambda th=theme: brazier(th))
    out["spikes"] = spikes
    out["exit_closed"] = lambda: exit_door(False)
    out["exit_open"] = lambda: exit_door(True)
    out["spawn_crack"] = spawn_crack
    out["loot_shard"] = loot_shard
    out["active_ring"] = active_ring
    for v in range(3):
        out[f"float_rock_{v}"] = (lambda vv=v: floating_rock(vv))
    out["spire_dark"] = lambda: spire("dark")
    out["spire_light"] = lambda: spire("light")
    out["rift_tear"] = rift_tear
    return out
