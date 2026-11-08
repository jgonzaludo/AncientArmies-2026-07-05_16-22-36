# builds the mood terrain art from the higgsfield sources: seamless ground tiles, the macro variation map, and the
# repacked sprite atlas. run from the project root: python3 -I ArtSource/Terrain/mood/build_mood_art.py
import json, os
import numpy as np
from PIL import Image

SRC = 'ArtSource/Terrain/mood'
GROUND = 'Assets/Resources/Terrain/Ground'
SPRITES = 'Assets/Resources/Terrain/Sprites'

def seamless(img, n=1024):
    a = np.asarray(img.convert('RGB').resize((n, n), Image.LANCZOS)).astype(np.float32)
    r = np.roll(a, (n // 2, n // 2), axis=(0, 1))
    t = np.linspace(0, 1, n, dtype=np.float32)
    edge = np.minimum(t, 1 - t) * 2                        # 0 at the borders, 1 in the middle
    w = np.minimum(edge[:, None], edge[None, :])
    w = np.clip((w - 0.12) / 0.5, 0, 1); w = w * w * (3 - 2 * w)
    out = a * w[..., None] + r * (1 - w[..., None])
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8))

def fbm(n, octaves, seed, base=4):
    rng = np.random.default_rng(seed)
    acc = np.zeros((n, n), np.float32); amp = 1.0; tot = 0.0
    for o in range(octaves):
        f = base * 2 ** o
        spec = rng.normal(size=(n, n)) + 1j * rng.normal(size=(n, n))
        k = np.fft.fftfreq(n) * n
        kk = np.sqrt(k[:, None] ** 2 + k[None, :] ** 2)
        band = np.exp(-((kk - f) ** 2) / (2 * (f * 0.5) ** 2))
        layer = np.real(np.fft.ifft2(spec * band))
        layer = (layer - layer.mean()) / (layer.std() + 1e-6)
        acc += layer * amp; tot += amp; amp *= 0.5
    acc /= tot
    return (acc - acc.min()) / (acc.max() - acc.min())

def macro():
    # r: dry grass amount, g: bare dirt amount, b: soft light (cloud shade), all tileable
    n = 512
    r = fbm(n, 4, 1, 3); g = fbm(n, 4, 2, 5); b = fbm(n, 3, 3, 2)
    img = np.stack([r, g, b], -1)
    Image.fromarray((img * 255).astype(np.uint8)).save(os.path.join(GROUND, 'macro.png'))

def trim(im):
    a = np.asarray(im)[..., 3]
    ys, xs = np.where(a > 8)
    return im.crop((xs.min(), ys.min(), xs.max() + 1, ys.max() + 1))

FOLIAGE = ('broadleaf_', 'pine_', 'bush_')

def foliage_grade(im):
    # the mockup's canopies: deep olive shadows, yellow-green body, golden sunlit edges, strong contrast.
    # a gradient map by lightness, mixed with the painted colour so the leaf detail stays
    a = np.asarray(im).astype(np.float32) / 255
    rgb, al = a[..., :3], a[..., 3:]
    l = rgb @ np.array([0.30, 0.59, 0.11])
    lo, hi = np.percentile(l[al[..., 0] > 0.5], 3), np.percentile(l[al[..., 0] > 0.5], 99)
    t = np.clip((l - lo) / (hi - lo + 1e-6), 0, 1) ** 1.15
    stops = np.array([0.0, 0.35, 0.7, 1.0]); cols = np.array([[0.07, 0.10, 0.04], [0.22, 0.29, 0.09], [0.52, 0.55, 0.16], [0.98, 0.86, 0.42]])
    grad = np.stack([np.interp(t, stops, cols[:, k]) for k in range(3)], -1)
    out = grad * 0.72 + rgb * 0.28
    leaf = np.clip((rgb[..., 1] - rgb[..., 0] * 0.86) / 0.06, 0, 1)[..., None]   # leaves only: brown bark keeps its colour
    out = out * leaf + rgb * (1 - leaf)
    return Image.fromarray((np.dstack([np.clip(out, 0, 1), al]) * 255).astype(np.uint8), 'RGBA')

def normal_map(im, dome=1.0, detail=0.9):
    # a height field for a painted prop: a smooth dome from the silhouette (distance to the edge) plus the brushwork
    # (bright leaf clusters stand out), turned into a tangent-space normal map. x right, y up, z toward the viewer.
    from scipy.ndimage import distance_transform_edt, gaussian_filter, sobel
    a = np.asarray(im).astype(np.float32) / 255
    al = a[..., 3]; lum = a[..., :3] @ np.array([0.30, 0.59, 0.11])
    inside = al > 0.5
    d = distance_transform_edt(inside)
    dmax = max(d.max(), 1.0)
    h = np.sqrt(np.clip(d / (dmax * 0.6), 0, 1)) * dome
    h = gaussian_filter(h, 3)
    h += gaussian_filter(lum, 1.2) * detail * (al > 0.05)
    k = 4.0 / max(im.width, im.height) * 100
    nx = -sobel(h, axis=1) * k; ny = sobel(h, axis=0) * k   # image rows go down, normal y goes up
    n = np.dstack([nx, ny, np.ones_like(h)])
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    rgb = n * 0.5 + 0.5
    rgb[~(al > 0.02)] = (0.5, 0.5, 1.0)
    return Image.fromarray((np.dstack([rgb, al]) * 255).astype(np.uint8), 'RGBA')

def atlas():
    names = sorted(json.load(open(os.path.join(SPRITES, 'terrain_atlas_data.json')))['sprites'], key=lambda s: s['name'])
    old = Image.open(os.path.join(SPRITES, 'terrain_atlas.png')).convert('RGBA')
    known = {s['name'] for s in names}
    for f in sorted(os.listdir(os.path.join(SRC, 'raw'))):
        n = f[:-4]
        if f.endswith('.png') and n not in known: names.append({'name': n, 'x': 0, 'y': 0, 'w': 220, 'h': 160})   # new sprites: stepping stones
    tiles = []
    for s in names:
        p = os.path.join(SRC, 'raw', s['name'] + '.png')
        if os.path.exists(p):
            im = trim(Image.open(p).convert('RGBA'))
            if s['name'].startswith(FOLIAGE): im = foliage_grade(im)
            if s['name'].startswith('ford_'):   # river stones: muted tan-grey, not orange
                a_ = np.asarray(im).astype(np.float32); g_ = a_[..., :3] @ np.array([0.3, 0.59, 0.11])
                a_[..., :3] = a_[..., :3] * 0.5 + np.stack([g_ * 1.02, g_ * 0.98, g_ * 0.88], -1) * 0.5
                im = Image.fromarray(np.clip(a_, 0, 255).astype(np.uint8), 'RGBA')
            # keep the old long side so on-screen sizes and ratios stay where the scatter code expects them
            k = max(s['w'], s['h']) / max(im.size)
            im = im.resize((max(1, round(im.width * k)), max(1, round(im.height * k))), Image.LANCZOS)
        else:
            im = old.crop((s['x'], s['y'], s['x'] + s['w'], s['y'] + s['h']))
        tiles.append((s['name'], im))
    W, pad = 2048, 6
    x = y = row = 0; place = []
    for name, im in sorted(tiles, key=lambda t: -t[1].height):
        if x + im.width > W: x = 0; y += row + pad; row = 0
        place.append((name, x, y, im)); x += im.width + pad; row = max(row, im.height)
    H = y + row
    H = 1 << (H - 1).bit_length() if H > 0 else 1
    sheet = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    nsheet = Image.new('RGBA', (W, H), (128, 128, 255, 0))
    data = {'width': W, 'height': H, 'sprites': []}
    for name, x, y, im in sorted(place):
        sheet.alpha_composite(im, (x, y))
        flat = name.startswith(('bridge_', 'ford_'))
        nsheet.paste(normal_map(im, dome=0.35 if flat else 1.0, detail=0.5 if flat else 0.9), (x, y))
        data['sprites'].append({'name': name, 'x': x, 'y': y, 'w': im.width, 'h': im.height})
    # bleed colour into the transparent pixels so mipmaps do not pick up a dark halo
    arr = np.asarray(sheet).astype(np.float32)
    a = arr[..., 3:4] / 255
    from scipy.ndimage import gaussian_filter
    col = np.stack([gaussian_filter(arr[..., c] * a[..., 0], 4) for c in range(3)], -1)
    wt = gaussian_filter(a[..., 0], 4)[..., None] + 1e-4
    bleed = col / wt
    arr[..., :3] = np.where(a > 0.02, arr[..., :3], bleed)
    Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8)).save(os.path.join(SPRITES, 'terrain_atlas.png'))
    json.dump(data, open(os.path.join(SPRITES, 'terrain_atlas_data.json'), 'w'), indent=1)
    nsheet.save(os.path.join(SPRITES, 'terrain_atlas_n.png'))
    return W, H

if __name__ == '__main__':
    for n in ['grass', 'dry', 'road', 'mud', 'forest', 'water']:
        p = os.path.join(SRC, 'ground', n + '.png')
        if os.path.exists(p): seamless(Image.open(p)).save(os.path.join(GROUND, n + '.png'))
    macro()
    print('atlas', atlas())
