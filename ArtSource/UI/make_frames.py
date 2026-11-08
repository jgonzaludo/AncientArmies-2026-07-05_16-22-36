# draws the gold-bevel ui sprites in code, at 4x and downsampled, so rims are thin, ends are round and slicing is exact.
# run: python3 ArtSource/UI/make_frames.py   (writes ArtSource/UI/generated/*.png)
import os
import numpy as np
from PIL import Image, ImageDraw
from scipy.ndimage import distance_transform_edt as edt
S = 4
def hexc(h): h = h.lstrip('#'); return np.array([int(h[i:i+2], 16) for i in (0, 2, 4)], dtype=np.float32)
GOLD_HI, GOLD_MID, GOLD_LO, OUTLINE = hexc('F4CD6E'), hexc('DFAB4B'), hexc('B07A22'), hexc('4A2E0C')
def grad(h, w, top, bot):
    t = np.linspace(0, 1, h, dtype=np.float32)[:, None, None]
    return np.broadcast_to(top * (1 - t) + bot * t, (h, w, 3))
def draw(name, w, h, shape, outline, rim, shadow, fill_top, fill_bot, gloss=0.0, rim_hi=GOLD_HI, rim_lo=GOLD_LO):
    W, H = w * S, h * S
    img = Image.new('L', (W, H), 0); d = ImageDraw.Draw(img)
    if shape[0] == 'round': d.rounded_rectangle((0, 0, W - 1, H - 1), radius=shape[1] * S, fill=255)
    else: d.polygon([(x * S, y * S) for x, y in shape[1]], fill=255)
    m0 = np.array(img) > 127
    dist = edt(np.pad(m0, 1))[1:-1, 1:-1]            # distance to the edge, inside the shape (padded so the image border counts as an edge)
    e1, e2, e3 = dist > outline * S, dist > (outline + rim) * S, dist > (outline + rim + shadow) * S
    rgb = np.zeros((H, W, 3), np.float32)
    rgb[m0] = OUTLINE
    gold = grad(H, W, rim_hi, rim_lo)
    # a soft light on the top edge, darker on the bottom edge of the rim
    rgb[e1] = gold[e1]
    rgb[e2] = (fill_top * 0.45)                      # inner shadow line
    fill = grad(H, W, fill_top, fill_bot)
    if gloss > 0:                                     # glossy top half for buttons
        t = np.linspace(0, 1, H, dtype=np.float32)[:, None, None]
        fill = fill + (255 - fill) * gloss * np.clip(1 - t * 2.1, 0, 1)
    rgb[e3] = fill[e3]
    a = (m0.astype(np.float32) * 255)
    out = np.dstack([rgb, a]).astype(np.uint8)
    im = Image.fromarray(out, 'RGBA').resize((w, h), Image.LANCZOS)
    im.save(f'ArtSource/UI/generated/{name}.png'); print(name, im.size)
NAVY_T, NAVY_B = hexc('2A3756'), hexc('1B2540')
draw('UI_Plate',      256,  88, ('round', 44), 2, 5, 3, NAVY_T, NAVY_B)
draw('UI_PlateBlue',  256,  88, ('round', 44), 2, 5, 3, hexc('3E7FDC'), hexc('1F56B4'), gloss=0.28)
draw('UI_PlateRed',   256,  88, ('round', 44), 2, 5, 3, hexc('D8453B'), hexc('A32219'), gloss=0.28)
draw('UI_Row',        256,  64, ('round', 32), 2, 3, 2, hexc('25314E'), hexc('18213A'), rim_hi=GOLD_MID, rim_lo=GOLD_LO)
draw('UI_Panel',      512, 512, ('round', 60), 3, 9, 6, hexc('243050'), hexc('171F36'))
draw('UI_Ribbon',     512, 128, ('poly', [(0, 0), (512, 0), (462, 64), (512, 128), (0, 128), (50, 64)]), 2, 6, 3, hexc('FFFFFF'), hexc('D9DEEA'))

# muted danger plate for destructive buttons, and a plain pill (no rim) used to clip bar fills
draw('UI_PlateDanger', 256, 88, ('round', 44), 2, 5, 3, hexc('7A2E2A'), hexc('52201D'), rim_hi=GOLD_MID, rim_lo=GOLD_LO)
draw('UI_PlateDisabled', 256, 88, ('round', 44), 2, 5, 3, hexc('2A3047'), hexc('1E2336'), rim_hi=hexc('8A91AA'), rim_lo=hexc('5E657D'))
draw('UI_Pill', 256, 88, ('round', 44), 0, 0, 0, hexc('FFFFFF'), hexc('FFFFFF'))

OUT = 'ArtSource/UI/generated'
# ---- icons: white strokes on transparent, drawn at 4x and downsampled ----
from PIL import ImageDraw
import math
def icon(name, fn, size=64):
    big = Image.new('RGBA', (size * S, size * S), (0, 0, 0, 0))
    d = ImageDraw.Draw(big)
    fn(d, size * S, 5.5 * S)
    img = big.resize((size, size), Image.LANCZOS)
    img.save(os.path.join(OUT, name + '.png'))
W_ = (255, 253, 248, 255)
def arc_arrow(d, cx, cy, r, a0, a1, w, head=1):
    d.arc([cx - r, cy - r, cx + r, cy + r], a0, a1, fill=W_, width=int(w))
    a = math.radians(a1)
    px, py = cx + r * math.cos(a), cy + r * math.sin(a)
    tx, ty = -math.sin(a), math.cos(a)   # tangent
    nx, ny = math.cos(a), math.sin(a)
    L = w * 2.2
    d.polygon([(px + tx * L * head, py + ty * L * head), (px + nx * L * 0.9, py + ny * L * 0.9), (px - nx * L * 0.9, py - ny * L * 0.9)], fill=W_)
def clock(d, n, w):
    c = n / 2; r = n * 0.38
    d.ellipse([c - r, c - r, c + r, c + r], outline=W_, width=int(w))
    d.line([(c, c), (c, c - r * 0.6)], fill=W_, width=int(w * 0.8)); d.line([(c, c), (c + r * 0.45, c + r * 0.2)], fill=W_, width=int(w * 0.8))
def restart(d, n, w): arc_arrow(d, n / 2, n / 2, n * 0.33, 40, 330, w)
def rotate(d, n, w):
    arc_arrow(d, n / 2, n / 2, n * 0.32, 200, 330, w); arc_arrow(d, n / 2, n / 2, n * 0.32, 20, 150, w)
def chevron(d, n, w):
    c = n / 2; d.line([(c - n * 0.22, c - n * 0.1), (c, c + n * 0.12), (c + n * 0.22, c - n * 0.1)], fill=W_, width=int(w), joint='curve')
def soldier(d, n, w):
    c = n / 2
    d.ellipse([c - n * 0.15, n * 0.14, c + n * 0.15, n * 0.44], fill=W_)
    d.pieslice([c - n * 0.3, n * 0.52, c + n * 0.3, n * 1.1], 180, 360, fill=W_)
icon('UI_Icon_Clock', clock); icon('UI_Icon_Restart', restart); icon('UI_Icon_Rotate', rotate); icon('UI_Icon_Chevron', chevron); icon('UI_Icon_Soldier', soldier)

# selection ring: thin gold rounded outline with a soft glow, nine-sliced
def ring(size=128, radius=30):
    n = size * S
    m = Image.new('L', (n, n), 0)
    ImageDraw.Draw(m).rounded_rectangle([14 * S, 14 * S, n - 14 * S, n - 14 * S], radius * S, fill=255)
    inner = Image.new('L', (n, n), 0)
    ImageDraw.Draw(inner).rounded_rectangle([(14 + 2.5) * S, (14 + 2.5) * S, n - (14 + 2.5) * S, n - (14 + 2.5) * S], (radius - 2.5) * S, fill=255)
    line = np.clip(np.array(m, float) - np.array(inner, float), 0, 255) / 255
    from scipy.ndimage import gaussian_filter
    glow = gaussian_filter(line, 7 * S) * 2.2
    a = np.clip(np.maximum(line, glow * 0.55), 0, 1)
    rgb = np.zeros((n, n, 4), float)
    rgb[..., 0], rgb[..., 1], rgb[..., 2] = 0.957, 0.804, 0.431
    rgb[..., 3] = a
    Image.fromarray((rgb * 255).astype(np.uint8), 'RGBA').resize((size, size), Image.LANCZOS).save(os.path.join(OUT, 'UI_Ring.png'))
ring()
