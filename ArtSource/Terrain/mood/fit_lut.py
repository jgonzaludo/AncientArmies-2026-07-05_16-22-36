# fits a 32^3 colour lookup that gives the game's rendered frame the mockup's colour distribution, and writes it as
# the urp user-lut strip (1024x32, slices by blue, sRGB in -> sRGB out). distribution matching, not per-pixel regression,
# so contrast is kept: lightness by histogram (cdf) matching, colour (lab a/b) by matching mean and spread per lightness band.
# run: python3 -I ArtSource/Terrain/mood/fit_lut.py <game.png> <mockup.png>
import sys, numpy as np
from PIL import Image
N = 32
LIFT = float(next((a.split('=')[1] for a in sys.argv if a.startswith('--lift=')), '0'))
OUT = 'Assets/Resources/Mood/MoodLut.bytes'

def srgb_to_lab(c):
    lin = np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
    M = np.array([[0.4124, 0.3576, 0.1805], [0.2126, 0.7152, 0.0722], [0.0193, 0.1192, 0.9505]])
    xyz = lin @ M.T / np.array([0.95047, 1.0, 1.08883])
    f = np.where(xyz > 0.008856, np.cbrt(xyz), 7.787 * xyz + 16 / 116)
    return np.stack([116 * f[..., 1] - 16, 500 * (f[..., 0] - f[..., 1]), 200 * (f[..., 1] - f[..., 2])], -1)

def lab_to_srgb(lab):
    fy = (lab[..., 0] + 16) / 116; fx = fy + lab[..., 1] / 500; fz = fy - lab[..., 2] / 200
    f = np.stack([fx, fy, fz], -1)
    xyz = np.where(f > 0.2069, f ** 3, (f - 16 / 116) / 7.787) * np.array([0.95047, 1.0, 1.08883])
    Mi = np.linalg.inv(np.array([[0.4124, 0.3576, 0.1805], [0.2126, 0.7152, 0.0722], [0.0193, 0.1192, 0.9505]]))
    lin = np.clip(xyz @ Mi.T, 0, 1)
    return np.clip(np.where(lin <= 0.0031308, lin * 12.92, 1.055 * lin ** (1 / 2.4) - 0.055), 0, 1)

if '--identity' in sys.argv:
    grid0 = np.stack(np.meshgrid(np.arange(N), np.arange(N), np.arange(N), indexing='ij'), -1).reshape(-1, 3) / (N - 1)
    st = np.zeros((N, N * N, 3), np.float32); l0 = grid0.reshape(N, N, N, 3)
    for b in range(N): st[:, b * N:(b + 1) * N] = l0[:, :, b].transpose(1, 0, 2)
    np.ascontiguousarray((st * 255 + 0.5).astype(np.uint8)).tofile(OUT); print('identity written'); sys.exit(0)
game = np.asarray(Image.open(sys.argv[1]).convert('RGB')).astype(np.float32) / 255
H, W = game.shape[:2]
conc = np.asarray(Image.open(sys.argv[2]).convert('RGB').resize((W, H), Image.LANCZOS)).astype(np.float32) / 255
cut = int(H * 0.13)                                  # skip the hud strip
G = srgb_to_lab(game[cut:].reshape(-1, 3)); C = srgb_to_lab(conc[cut:].reshape(-1, 3))
qs = np.linspace(0, 100, 201)
gq = np.percentile(G[:, 0], qs); cq = np.percentile(C[:, 0], qs)
def mapL(L): return np.interp(L, gq, cq)
# per lightness band colour statistics (bands by percentile so both images have the same number of pixels per band)
B = 12
edges_g = np.percentile(G[:, 0], np.linspace(0, 100, B + 1)); edges_c = np.percentile(C[:, 0], np.linspace(0, 100, B + 1))
def band_stats(X, edges):
    mu = np.zeros((B, 2)); sd = np.zeros((B, 2)); ctr = np.zeros(B)
    for k in range(B):
        sel = (X[:, 0] >= edges[k]) & (X[:, 0] <= edges[k + 1])
        mu[k] = X[sel, 1:].mean(0); sd[k] = X[sel, 1:].std(0) + 1e-3; ctr[k] = X[sel, 0].mean()
    return mu, sd, ctr
gmu, gsd, gctr = band_stats(G, edges_g); cmu, csd, cctr = band_stats(C, edges_c)
def interp(L, ctr, v): return np.stack([np.interp(L, ctr, v[:, j]) for j in range(2)], -1)

def transform(lab):
    L = lab[..., 0]; L2 = mapL(L) + LIFT * (1.0 - np.abs(mapL(L) - 50.0) / 50.0)   # lift offsets the vignette that runs after the lut
    ab = lab[..., 1:]
    m_g = interp(L, gctr, gmu); s_g = interp(L, gctr, gsd)
    m_c = interp(L2, cctr, cmu); s_c = interp(L2, cctr, csd)
    ab2 = (ab - m_g) * np.clip(s_c / s_g, 0.4, 1.0) + m_c
    return np.concatenate([L2[..., None], ab2], -1)

grid = np.stack(np.meshgrid(np.arange(N), np.arange(N), np.arange(N), indexing='ij'), -1).reshape(-1, 3) / (N - 1)   # r,g,b
if '--compose' in sys.argv:
    # refine: the game already runs a lut, so map its current outputs onward instead of the identity grid
    old = np.fromfile(OUT, np.uint8).reshape(N, N * N, 3).astype(np.float32) / 255   # [g, b*N + r]
    gi, bi, ri = np.meshgrid(np.arange(N), np.arange(N), np.arange(N), indexing='ij')
    grid = np.stack([old[g_, b_ * N + r_] for r_ in range(N) for g_ in range(N) for b_ in range(N)])
if '--linear' in sys.argv:
    # urp looks the table up with linear colour and returns linear colour: grid points are linear, so convert in and out
    to_srgb = lambda x: np.where(x <= 0.0031308, x * 12.92, 1.055 * np.clip(x, 0, 1) ** (1 / 2.4) - 0.055)
    to_lin = lambda x: np.where(x <= 0.04045, x / 12.92, ((x + 0.055) / 1.055) ** 2.4)
    lut = to_lin(lab_to_srgb(transform(srgb_to_lab(to_srgb(grid))))).reshape(N, N, N, 3)
else:
    lut = lab_to_srgb(transform(srgb_to_lab(grid))).reshape(N, N, N, 3)
strip = np.zeros((N, N * N, 3), np.float32)
for b in range(N): strip[:, b * N:(b + 1) * N] = lut[:, :, b].transpose(1, 0, 2)   # rows by green, columns red inside each blue slice
# raw rgb24 bytes, bottom row first (unity's LoadRawTextureData order), so the texture is read exactly as written
np.ascontiguousarray((np.clip(strip, 0, 1) * 255 + 0.5).astype(np.uint8)).tofile(OUT)
Image.fromarray((np.flipud(strip) * 255 + 0.5).astype(np.uint8), 'RGB').save(OUT.replace('.bytes', '_preview.png'))
out = lab_to_srgb(transform(srgb_to_lab(game.reshape(-1, 3)))).reshape(game.shape)
both = np.hstack([game, out, conc])
Image.fromarray((both * 255).astype(np.uint8)).resize((2400, round(H * 2400 / (3 * W)))).save('ArtSource/Terrain/mood/_lut_check.jpg', quality=85)
for name, a in (('game', game), ('mapped', out), ('mockup', conc)):
    x = a[cut:]; l = x @ np.array([.2126, .7152, .0722])
    print(f"{name:7s} rgb={np.round(x.reshape(-1,3).mean(0),3)} lum p5/50/95={np.percentile(l,5):.2f}/{np.percentile(l,50):.2f}/{np.percentile(l,95):.2f}")
