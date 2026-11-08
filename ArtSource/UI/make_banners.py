# redraws the four formation banners as roman vexilla (crossbar, cloth, swallow-tail, spearhead point), same canvas and
# bottom point as the old shield banners so the in-world banners still line up. icons come from the old art in banner_originals/.
# run: python3 ArtSource/UI/make_banners.py   (writes into Assets/Art/UI/Banners/Resources)
import numpy as np
from PIL import Image, ImageDraw
from scipy.ndimage import distance_transform_edt as edt, gaussian_filter
W, H, S = 281, 473, 4
def hexc(h): h = h.lstrip('#'); return np.array([int(h[i:i+2], 16) for i in (0, 2, 4)], np.float32)
BR_HI, BR_LO, OUT = hexc('E6C77E'), hexc('7A5420'), hexc('24160A')
def mask(draw_fn):
    im = Image.new('L', (W * S, H * S), 0); d = ImageDraw.Draw(im); draw_fn(d); return np.array(im) > 127
def poly(pts): return lambda d: d.polygon([(x * S, y * S) for x, y in pts], fill=255)
def vgrad(top, bot, y0, y1):
    t = np.clip((np.arange(H * S, dtype=np.float32) - y0 * S) / max(1, (y1 - y0) * S), 0, 1)[:, None, None]
    return np.broadcast_to(top * (1 - t) + bot * t, (H * S, W * S, 3))
def layer(rgb, m, color): rgb[m] = color[m] if color.ndim == 3 else color
CLOTH = [(34, 30), (247, 30), (247, 392), (140.5, 348), (34, 392)]
def bar_fn(d):
    d.rounded_rectangle((6 * S, 8 * S, 275 * S, 34 * S), radius=10 * S, fill=255)
    for cx in (9, 272): d.ellipse(((cx - 13) * S, (21 - 13) * S, (cx + 13) * S, (21 + 13) * S), fill=255)
POINT = [(108, 412), (173, 412), (140.5, 466)]
def build(team, ranged):
    top, bot = (hexc('3E68A8'), hexc('1F3A6E')) if team == 'Blue' else (hexc('B63C30'), hexc('781C15'))
    rgb = np.zeros((H * S, W * S, 3), np.float32); alpha = np.zeros((H * S, W * S), bool)
    bronze = vgrad(BR_HI, BR_LO, 0, H)
    # cloth: dark outline, bronze edge band, inner shadow, then the dyed field with soft vertical folds
    mc = mask(poly(CLOTH)); dist = edt(np.pad(mc, 1))[1:-1, 1:-1]
    layer(rgb, mc, np.array(OUT, np.float32)); alpha |= mc
    band = dist > 2 * S; layer(rgb, band, bronze)
    inner = dist > 9 * S; layer(rgb, inner, np.array(OUT, np.float32) * 0 + top * 0.35)
    field = dist > 11 * S
    xs = np.arange(W * S, dtype=np.float32)[None, :, None]
    fold = 1 + 0.07 * np.sin(xs / S / 11.0) + 0.04 * np.sin(xs / S / 4.3)
    cloth = vgrad(top, bot, 30, 392) * fold
    layer(rgb, field, np.clip(cloth, 0, 255))
    # crossbar and the spearhead point, bronze with a dark outline
    for pts, fn in ((None, bar_fn), (POINT, poly(POINT))):
        m = mask(fn); d2 = edt(np.pad(m, 1))[1:-1, 1:-1]
        layer(rgb, m, np.array(OUT, np.float32)); layer(rgb, d2 > 2 * S, bronze); layer(rgb, d2 > 5 * S, vgrad(BR_HI * 0.9 + 20, BR_LO * 1.1, 0, H)); alpha |= m
    # the icon: white pixels of the old art inside the field, redrawn cream with a soft dark shadow
    old = np.array(Image.open(f'ArtSource/UI/banner_originals/UI_Banner_{team}_{"Ranged" if ranged else "Melee"}.png').convert('RGBA')).astype(np.float32)
    white = (old[..., 0] > 215) & (old[..., 1] > 215) & (old[..., 2] > 205) & (old[..., 3] > 200)
    white[:62, :] = False; white[335:, :] = False; white[:, :48] = False; white[:, 235:] = False
    im = Image.fromarray((white * 255).astype(np.uint8)).resize((W * S, H * S), Image.LANCZOS)
    ic = np.array(im).astype(np.float32) / 255
    ic = np.clip((gaussian_filter(ic, 0.9 * S) - 0.5) * 4 + 0.5, 0, 1)   # round off the stair-steps from the upscale
    sh = np.roll(gaussian_filter(ic, 2 * S), (3 * S, 0), axis=(0, 1))
    rgb = rgb * (1 - 0.45 * sh[..., None]); cream = np.array([242, 232, 208], np.float32)
    rgb = rgb * (1 - ic[..., None]) + cream * ic[..., None]
    out = np.dstack([np.clip(rgb, 0, 255), alpha * 255]).astype(np.uint8)
    Image.fromarray(out, 'RGBA').resize((W, H), Image.LANCZOS).save(f'Assets/Art/UI/Banners/Resources/UI_Banner_{team}_{"Ranged" if ranged else "Melee"}.png')
for t in ('Blue', 'Red'):
    for r in (False, True): build(t, r); print(t, r)
