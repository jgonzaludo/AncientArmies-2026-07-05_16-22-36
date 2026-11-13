# draws the gold-bevel ui sprites in code, at 4x and downsampled, so rims are thin, ends are round and slicing is exact.
# run: python3 ArtSource/UI/make_frames.py   (writes ArtSource/UI/generated/*.png)
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
draw('UI_Pill', 256, 88, ('round', 44), 0, 0, 0, hexc('FFFFFF'), hexc('FFFFFF'))
