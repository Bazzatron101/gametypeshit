"""Draws an elevation map of the overworld from the reference drawing (ticket 39).

Land comes from the drawing's colours; mountains are ridges drawn along the drawing's mountain
icons by hand (pixel coordinates on the 909 x 673 reference). Heights are in metres; the
greyscale file stores metres / SCALE (16-bit).

Run: python scripts/make-overworld-heights.py  (needs numpy, scipy, pillow, matplotlib).
Writes docs/maps/overworld-heights-ai.png, overworld-water-ai.png and overworld-topo-ai.png.
"""
import numpy as np
from PIL import Image
from scipy import ndimage as ndi
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.colors import LinearSegmentedColormap, LightSource

import os
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
REF = os.path.join(ROOT, "docs", "maps", "overworld-reference.webp")
OUT = os.path.join(ROOT, "docs", "maps", "")
SCALE = 250.0          # metres for white
UP = 2                 # output is 2x the drawing
SEA_LEVEL = 4.0
WIDEN = 1.5            # ranges are drawn this much wider than the icons
BANK_DEGREES = 28.0    # the steepest a shore rises from water
WATER = 2.5            # river and lake surfaces sit under sea level, so the bake makes them water

im = np.asarray(Image.open(REF).convert("RGB")).astype(float)
H0, W0 = im.shape[:2]
R, G, B = im[..., 0], im[..., 1], im[..., 2]

# --- Land -------------------------------------------------------------------------------
land = (G > 60) & (R - B > 30) & (R - G < 35) & (R < 235)
yy, xx = np.mgrid[0:H0, 0:W0]
snowy = (B > 105) & (B >= R - 10) & (xx > 455) & (xx < 700) & (yy > 300) & (yy < 545)
land |= ndi.binary_dilation(snowy, iterations=3)
land = ndi.binary_closing(land, structure=np.ones((5, 5)))
land = ndi.binary_fill_holes(land)
# Where the drawing's icons hide the land (a grey castle on the coast reads as sea), say so by hand.
LAND_PATCHES = [(805, 195, 18), (95, 270, 26)]   # Saltreach castle; the guild town (blue spires)
for (px, py, r) in LAND_PATCHES:
    land |= (xx - px) ** 2 + (yy - py) ** 2 <= r * r
land = ndi.binary_opening(land, structure=np.ones((3, 3)))
lab, n = ndi.label(land)
sizes = ndi.sum(land, lab, range(1, n + 1))
land = np.isin(lab, [i + 1 for i, s in enumerate(sizes) if s > 150])

# Up-sample the mask smoothly.
H, W = H0 * UP, W0 * UP
soft = ndi.gaussian_filter(land.astype(float), 1.2)
soft = np.asarray(Image.fromarray((soft * 255).astype(np.uint8)).resize((W, H), Image.BILINEAR)) / 255.0
land_up = soft > 0.5

rng = np.random.default_rng(7)
def noise(sigma, amp):
    n = ndi.gaussian_filter(rng.standard_normal((H, W)), sigma)
    return n / (np.abs(n).max() + 1e-9) * amp

# --- Base: lowlands rising gently inland ---------------------------------------------------
inland = ndi.distance_transform_edt(land_up)          # px from the coast, on land
offshore = ndi.distance_transform_edt(~land_up)       # px from the coast, at sea
h = np.where(land_up, 6.0 + 28.0 * (1 - np.exp(-inland / (30 * UP))), 0.0)
rolling = noise(28 * UP, 10.0) + np.maximum(noise(12 * UP, 30.0), 0.0) + noise(6 * UP, 4.0)  # broad swells, scattered hills, small bumps
h += np.where(land_up, rolling, 0.0) * np.clip(inland / (6 * UP), 0, 1)
# Shallows: just under the sea near the coast, then the sea floor.
h = np.where(land_up, h, np.clip(SEA_LEVEL - 1.5 - offshore / (6 * UP) * 3.0, 0.0, None))

# --- Mountains: ridges along the drawing's mountain icons ----------------------------------
def seg_dist(px, py, a, b):
    ax, ay = a; bx, by = b
    dx, dy = bx - ax, by - ay
    t = np.clip(((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy + 1e-9), 0, 1)
    return np.hypot(px - (ax + t * dx), py - (ay + t * dy))

PY, PX = np.mgrid[0:H, 0:W] / UP   # coordinates on the drawing

RIDGES = [
    # name, height m, half-width px, points (on the 909 x 673 drawing)
    ("North-west mountains", 150, 20, [(84, 112), (108, 84), (150, 70), (212, 74)]),
    ("Brokenstone range", 175, 26, [(330, 338), (362, 292), (395, 248), (372, 190)]),
    ("Brokenstone east spur", 135, 18, [(395, 248), (440, 252), (466, 290)]),
    ("North-east mountains (west)", 150, 18, [(522, 136), (568, 150), (598, 176), (606, 214)]),
    ("North-east mountains (east)", 140, 18, [(660, 178), (700, 142), (740, 122), (768, 110)]),
    ("West-coast range", 165, 22, [(126, 536), (136, 480), (150, 422), (166, 372), (182, 356)]),
    ("West-coast inner spur", 110, 15, [(166, 396), (210, 402), (242, 420)]),
    ("Southern hills", 95, 16, [(290, 400), (322, 420), (352, 440), (368, 452)]),
    ("Snowy mountains", 205, 22, [(552, 318), (546, 360), (545, 395), (526, 425), (500, 455), (482, 486), (470, 522)]),
    ("Snowy east arm", 180, 18, [(545, 395), (590, 396), (630, 376), (668, 366)]),
]

crag = np.clip(1.0 + noise(4 * UP, 0.22) + noise(14 * UP, 0.22), 0.55, 1.2)
widen = 1.0 + noise(12 * UP, 0.45)
mountains = np.zeros((H, W))
for name, height, width, pts in RIDGES:
    d = np.full((H, W), np.inf)
    for a, b in zip(pts, pts[1:]):
        d = np.minimum(d, seg_dist(PX, PY, a, b))
    # Ends taper so ranges do not stop like a wall.
    w = width * WIDEN
    shape = np.exp(-(d * np.clip(widen, 0.6, 1.6) / w) ** 1.6)
    foothills = 0.3 * np.exp(-(d / (w * 2.6)) ** 2)   # a wide skirt of hills round each range
    mountains = np.maximum(mountains, height * np.maximum(shape, foothills))
h += mountains * crag * np.where(land_up, 1.0, 0.0)

# A few lone hills (the barrow hill in the Greenmarch, the lighthouse headland).
for (x, y, height, r) in [(118, 165, 45, 14), (790, 92, 35, 12), (282, 300, 30, 12)]:
    h += height * np.exp(-((PX - x) ** 2 + (PY - y) ** 2) / r ** 2) * land_up

# --- Rivers and lakes ------------------------------------------------------------------------
# Every river starts high in a range, runs downhill through the lakes on its way, and ends in
# the sea or in another river. Points are on the drawing, from the source to the mouth.
RIVERS = [
    ("Greywater (the ford)", [(178, 84), (212, 112), (255, 150), (300, 190), (298, 236), (292, 284)]),   # NW mountains, Greenmarch mere, the bay
    ("Fen river", [(158, 404), (186, 438), (216, 466), (228, 500), (236, 534), (240, 568)]),           # west-coast range, past Stiltmoor, Fen lakes, south coast
    ("Fen beck", [(222, 410), (238, 432), (250, 455), (240, 482), (229, 502)]),                         # inner spur, Fen pool, joins the Fen river
    ("Ember river", [(632, 384), (662, 345), (690, 300), (740, 302), (790, 300), (830, 302), (852, 304)]),  # snowy east arm, Steppe lake, east coast
    ("North-east river", [(598, 190), (606, 225), (645, 240), (690, 222), (724, 194)]),                # NE mountains, the north-east bay
    ("Quarry brook", [(386, 262), (378, 300), (380, 340), (402, 382), (430, 420), (442, 454)]),        # Brokenstone, the south bay
    ("Tarn beck", [(544, 342), (520, 318), (500, 308), (478, 300), (450, 344), (418, 396)]),                        # snowy mountains, the Tarn, joins Quarry brook
]
LAKES = [
    # name, centre, radius (on the drawing)
    ("Fen lakes", (228, 500), 9), ("Fen pool", (250, 455), 6),
    ("Steppe lake", (690, 300), 11), ("Tarn", (478, 300), 8), ("Greenmarch mere", (300, 190), 9),
]
from PIL import ImageDraw

def meander(pts, amp=4.0, wavelength=40.0, seed=0):
    """The drawn line, densified and swung side to side so the river winds. It stays on the
    drawn points (sources, lakes, mouths), so it still goes through each lake."""
    r = np.random.default_rng(seed)
    out = []
    phase = r.uniform(0, 6.28)
    for a, b in zip(pts, pts[1:]):
        length = np.hypot(b[0] - a[0], b[1] - a[1])
        nx, ny = -(b[1] - a[1]) / length, (b[0] - a[0]) / length
        for k in range(int(length)):
            t = k / length
            taper = np.sin(np.pi * t)  # back on the line at every drawn point
            off = amp * taper * (np.sin(k / wavelength * 6.28 + phase) + 0.5 * np.sin(k / (wavelength * 0.37) * 6.28 + 2 * phase))
            out.append((a[0] + (b[0] - a[0]) * t + nx * off, a[1] + (b[1] - a[1]) * t + ny * off))
        phase += 1.7
    out.append(pts[-1])
    return out

def distance_to(draw_fn):
    """Distance (in drawing px) to whatever draw_fn paints, via a rasterised mask."""
    canvas = Image.new("L", (W, H), 0)
    draw_fn(ImageDraw.Draw(canvas))
    return ndi.distance_transform_edt(np.asarray(canvas) == 0) / UP

# Lakes first: a bowl round each, its surface at the water level.
lakes = np.zeros((H, W), bool)
bowl = np.zeros((H, W))
for i, (name, (x, y), r) in enumerate(LAKES):
    rr = np.random.default_rng(100 + i)
    angles = np.linspace(0, 6.28, 40, endpoint=False)
    stretch, tilt = rr.uniform(1.0, 1.7), rr.uniform(0, 3.14)
    radii = r * (1 + 0.22 * ndi.gaussian_filter1d(rr.standard_normal(40), 2, mode="wrap") / 0.35)
    shore = [((x + rad * np.cos(a) * stretch * np.cos(tilt) - rad * np.sin(a) * np.sin(tilt)) * UP,
              (y + rad * np.cos(a) * stretch * np.sin(tilt) + rad * np.sin(a) * np.cos(tilt)) * UP) for a, rad in zip(angles, radii)]
    d = distance_to(lambda g: g.polygon(shore, fill=255))
    bowl = np.maximum(bowl, np.exp(-(d / (r * 1.6)) ** 2))
    lakes |= d < 0.5
banks = SEA_LEVEL + 2.0 + 0.25 * np.maximum(h - SEA_LEVEL, 0)
h = np.where(land_up, h * (1 - bowl) + np.minimum(h, banks) * bowl, h)
h = np.where(lakes, np.minimum(h, WATER), h)

# Rivers: a bed that only ever goes down from the source to the mouth, cut into the land as a
# V-shaped valley. High up it is a mountain stream in a gully; in the lowlands, through lakes
# and at the mouth its bed is at the water level.
streams = np.zeros((H, W), bool)
for i, (name, pts) in enumerate(RIVERS):
    path = meander(pts, seed=i)
    cols = np.clip(np.round(np.array([p[0] for p in path]) * UP).astype(int), 0, W - 1)
    rows = np.clip(np.round(np.array([p[1] for p in path]) * UP).astype(int), 0, H - 1)
    ground = h[rows, cols]
    n = len(path)
    s = np.arange(n) / (n - 1)
    # Falls steadily to the water level at the mouth, never rises, and is at the water level in any lake.
    bed = np.minimum(ground - 1.5, WATER + (ground[0] - WATER) * (1 - s) ** 1.4)
    bed = np.where(lakes[rows, cols], WATER, bed)
    bed = np.minimum.accumulate(np.maximum(bed, WATER))
    profile = np.full((H, W), np.inf)
    canvas = np.zeros((H, W), bool)
    for k in range(n - 1):  # rasterise each step with its bed height
        for t in np.linspace(0, 1, 4 * UP):
            c = int(round((path[k][0] + (path[k + 1][0] - path[k][0]) * t) * UP))
            r_ = int(round((path[k][1] + (path[k + 1][1] - path[k][1]) * t) * UP))
            if 0 <= c < W and 0 <= r_ < H:
                canvas[r_, c] = True
                profile[r_, c] = min(profile[r_, c], bed[k])
    d, (ri, ci) = ndi.distance_transform_edt(~canvas, return_indices=True)
    d = d / UP
    target = profile[ri, ci]
    near = d < 30
    valley_floor = target + 2.2 * np.maximum(d - 1.2, 0) ** 1.25   # sides rise away from the bed
    h = np.where(near & land_up, np.minimum(h, valley_floor), h)
    streams |= (d < 1.3) & land_up                                  # about 30 m wide

water_full = (lakes | streams) & land_up      # every river and lake, high or low (for the map and the water mask)
water = water_full & (h <= WATER + 0.01)      # the parts the bake already treats as water (under sea level)

h = ndi.gaussian_filter(h, 0.8)
h = np.where(land_up, np.maximum(h, np.where(water, 0.0, SEA_LEVEL + 1.0)), np.minimum(h, SEA_LEVEL - 0.5))
h = np.where(water, np.minimum(h, WATER), h)
h = ndi.gaussian_filter(h, 0.5)

# Banks: no shore of the sea, a river or a lake rises steeper than BANK_DEGREES, so you can walk down to
# the water and a road along a valley is walkable. Mountains away from water keep their steep faces.
wet = (~land_up) | water_full
step = np.tan(np.radians(BANK_DEGREES)) * (10500.0 / W)               # metres of rise per pixel
cap = np.where(wet, np.where(land_up, h, SEA_LEVEL + 1.0), np.inf)   # the sea counts as its shore line; a stream as its own bed
for _ in range(int(SCALE / step) + 2):                                 # spread each water's height outwards as a cone
    grown = cap.copy()
    for dy, dx, cost in ((0, 1, 1.0), (1, 0, 1.0), (1, 1, 1.4142), (1, -1, 1.4142)):
        for sy, sx in ((dy, dx), (-dy, -dx)):
            shifted = np.roll(np.roll(cap, sy, axis=0), sx, axis=1) + step * cost
            grown = np.minimum(grown, shifted)
    if np.array_equal(grown, cap):
        break
    cap = grown
h = np.where(wet, h, np.minimum(h, cap))
h = np.clip(h, 0, SCALE)

# --- The greyscale heightmap (16-bit) -----------------------------------------------------
grey = np.round(h / SCALE * 65535).astype(np.uint16)
Image.fromarray(grey).save(OUT + "overworld-heights-ai.png")
# Every river and lake, white on black: rivers high in the hills are above sea level, so the bake
# will need this mask to make them water (today only ground under sea level is water).
Image.fromarray((water_full * 255).astype(np.uint8)).save(OUT + "overworld-water-ai.png")

# --- A coloured topographic map to look at -------------------------------------------------
km_w, km_h = 10.5, 10.5 * H0 / W0
extent = [0, km_w, 0, km_h]
land_cmap = LinearSegmentedColormap.from_list("land", [
    (0.00, "#5f8f4e"), (0.10, "#86a85a"), (0.22, "#c2c27a"), (0.38, "#c9a66b"),
    (0.55, "#a07850"), (0.75, "#8a7a70"), (0.90, "#d8d8dc"), (1.00, "#ffffff")])
ls = LightSource(azdeg=315, altdeg=40)
norm = np.clip((h - SEA_LEVEL) / (SCALE - SEA_LEVEL), 0, 1)
rgb = land_cmap(norm)[..., :3]
shaded = ls.shade_rgb(rgb, h, vert_exag=0.08, blend_mode="soft")
sea_rgb = np.array([0.16, 0.30, 0.48]) * (0.75 + 0.25 * np.clip(h / SEA_LEVEL, 0, 1))[..., None]
img = np.where(land_up[..., None], shaded, sea_rgb)
img = np.where(water_full[..., None], np.array([0.25, 0.45, 0.68]), img)

fig, ax = plt.subplots(figsize=(W / 150, H / 150 + 0.9), dpi=150)
ax.imshow(img, extent=extent, origin="upper")
flipped = h[::-1]  # contour wants rows from the bottom
X = np.linspace(0, km_w, W); Y = np.linspace(0, km_h, H)
ax.contour(X, Y, flipped, levels=np.arange(25, SCALE, 25), colors="k", linewidths=0.35, alpha=0.45)
major = ax.contour(X, Y, flipped, levels=np.arange(50, SCALE, 50), colors="k", linewidths=0.8, alpha=0.6)
ax.clabel(major, fmt="%d m", fontsize=6, inline=True)
ax.contour(X, Y, flipped, levels=[SEA_LEVEL], colors="#1b2b40", linewidths=1.0)
for name, height, width, pts in RIDGES:
    x, y = pts[len(pts) // 2]
    ax.text(x / W0 * km_w, km_h - y / H0 * km_h, name, fontsize=6.5, ha="center", va="center", color="#202020",
            bbox=dict(boxstyle="round,pad=0.2", fc=(1, 1, 1, 0.65), ec="none"))
for name, pts in RIVERS:
    x, y = pts[len(pts) // 2]
    ax.text(x / W0 * km_w, km_h - y / H0 * km_h, name, fontsize=5.5, ha="left", va="bottom", color="#103060", style="italic")
for name, (x, y), r in LAKES:
    ax.text(x / W0 * km_w, km_h - (y + r + 4) / H0 * km_h, name, fontsize=5.5, ha="center", va="top", color="#103060", style="italic")
# Roads, towns and gate spots from the game's layout (Assets/Editor/Overworld/ContinentLayout.cs), if present.
import re
layout_path = os.path.join(ROOT, "Assets", "Editor", "Overworld", "ContinentLayout.cs")
if os.path.exists(layout_path):
    layout = open(layout_path, encoding="utf-8").read()
    def K(px, py): return px / W0 * km_w, km_h - py / H0 * km_h
    for name, nums in re.findall(r'new Road\("([^"]+)", ([\d, ]+)\)', layout):
        v = [float(n) for n in nums.split(",")]
        pts = [K(v[i], v[i + 1]) for i in range(0, len(v), 2)]
        ax.plot([p[0] for p in pts], [p[1] for p in pts], color="#5a3a1a", linewidth=1.1, linestyle=(0, (3, 1.5)), alpha=0.9)
    for name, x, y, castle in re.findall(r'new Place\("([^"]+)", (\d+)f, (\d+)f(, castle: true)?\)', layout):
        kx, ky = K(float(x), float(y))
        ax.plot(kx, ky, marker="s" if castle else "o", markersize=4.5, color="#7a1f1f", markeredgecolor="white", markeredgewidth=0.6)
        ax.text(kx + 0.06, ky + 0.05, name, fontsize=5, color="#3a0d0d")
    for name, x, y in re.findall(r'new GateSpot\("([^"]+)", (\d+)f, (\d+)f', layout):
        kx, ky = K(float(x), float(y))
        ax.plot(kx, ky, marker="X", markersize=5, color="#6b2fb3", markeredgecolor="white", markeredgewidth=0.5)
        ax.text(kx + 0.06, ky - 0.12, name, fontsize=4.8, color="#3d1470")
ax.set_xlabel("km east"); ax.set_ylabel("km north")
ax.set_title("Overworld (AI draft, from the reference map): contours every 25 m, sea level 4 m; roads dashed, towns ●, castles ■, gate spots ✕", fontsize=8)
sm = plt.cm.ScalarMappable(cmap=land_cmap, norm=plt.Normalize(SEA_LEVEL, SCALE))
fig.colorbar(sm, ax=ax, fraction=0.025, pad=0.01, label="metres")
fig.tight_layout()
fig.savefig(OUT + "overworld-topo-ai.png")

print("size", W, H, "max m", round(float(h.max()), 1), "land mean m", round(float(h[land_up].mean()), 1))
