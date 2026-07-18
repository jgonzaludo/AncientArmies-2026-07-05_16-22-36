# Roadmap Deep Review — 2026-07-18 (read-only investigation)

Four parallel investigations + primary-session analysis. No code changed.
This document is the working reference for the next five patches.

---

## A. Mobile rendering bugs (fold into Patch 1) — root causes CONFIRMED

### A1. Banners drift off their century at far zoom (device)
The declutter solver works in RAW PIXELS (`overlapPaddingX/Y` 95/80,
`overlapStepPx` 70, tuned at 1080p). On a ~2556-px phone the same cluster
spans ~2.4× more pixels → different collision-step counts → different
accumulated lift, which at far zoom dwarfs the tiny projected hover offset.
Also: `SoldierScreenPixels` is NOT resolution-independent (the code comment
lies) — it doubles on device, shifting every detail band and Adaptive
visibility. FIX: express all banner pixel constants and detail bands as
fractions of `Screen.height` (or in `SoldierScreenPixels` units).

### A2. 80-circle destination preview invisible on device
The circles are the ONLY user of `Graphics.RenderMeshInstanced`; the
transparent URP Unlit material is runtime-generated, so the INSTANCING_ON
shader variant is never referenced by any serialized asset and gets STRIPPED
from device builds (editor compiles variants on demand — classic
works-in-editor signature). FIX: replace instancing with one combined mesh
of ≤80 discs per formation drawn via the already-proven non-instanced path
(also simpler); optionally keep instancing behind a
`SystemInfo.supportsInstancing` + variant-collection guard.

### A3. Buttons render as ovals
`RoundedSprite` is created with `pixelsPerUnit: 1` while the canvas
`referencePixelsPerUnit` is 100 → sliced corners want 14/(1/100) = 1400 px
each; Unity squeezes them anisotropically into 215×84 buttons → ellipse.
The radius 18→12 change was irrelevant. FIX SPEC: 64-px sprite, corner
radius 16, `Sprite.Create(..., pixelsPerUnit: 100f, ..., border(16,16,16,16))`,
`Image.Type.Sliced` — fixed-radius true rounded rectangles at every size.

### A4. Divergence sweep (severity-ordered)
1. **Per-slot MaterialPropertyBlocks on every soldier defeat the SRP batcher**
   (6–12 SetPropertyBlock per soldier) — the army becomes thousands of
   unbatched skinned draws on device. Biggest silent device cost.
2. Runtime `Shader.Find("Universal Render Pipeline/…")` everywhere — URP
   shaders/variants must be in Always Included Shaders or a variant
   collection or device builds can go magenta/invisible (same family as A2).
3. `Screen.dpi`-based touch slop unreliable on Android; tap-padding constants
   in raw px shrink physically on high-DPI → accidental drags, hard taps.
   Move to Screen.height fractions.
4. Soldier SkinnedMeshRenderers cast shadows (double-draw the whole army).
5. Legacy dynamic font softness at high DPI (cosmetic).

---

## B. Patch 1 — Far-zoom performance LOD + camera framing (v0.10.0)

### Cost ranking at far zoom (all 1,280 on screen, tiny)
1. Skinned mesh render ×2 (shadow pass!) — geometry-bound, screen size
   irrelevant; archers have ≥2 SMRs each.
2. Animator evaluation + bone writes — `CullUpdateTransforms` only helps
   OFFSCREEN; tiny-but-visible soldiers evaluate fully.
3. MPB batch fragmentation + 2,560 visual-controller `Update()`s.

### Imposter design (recommended)
- Per-FORMATION instanced batch: ONE `RenderMeshInstanced` call of ~80
  camera-yaw-billboarded team-colored quads per century → 16 draw calls for
  the whole battle. `FormationImposterRenderer` sibling component. Shadows
  OFF. (Note A2's stripping lesson: ship the shader variant deliberately, or
  use the combined-mesh fallback here too.)
- Trigger: `SoldierScreenPixels` ≤ ~34 px enter / ≥ 40 px exit (dual
  threshold hysteresis, DPI-normalized per A1), aligned with the banner's
  Expanded band — imposters engage exactly when the banner becomes the unit.
- Swap staggered per formation over frames (no 1,280-swap spike).
- LOD-out routine per soldier: `ReleasePendingShot()` FIRST (archers defer
  projectile spawn to an animator frame — disabling the Animator mid-draw
  would silently swallow validated shots; OnDestroy already models the fix)
  → disable Animator → disable visual controller → hide SMRs → imposter on.
  LOD-in reverses + one `ApplyPalette(Health01(), 0)` to restore damage tint.
- Hit flash at LOD: per-instance color array entry → white, decay 0.08 s
  (free — array re-uploads anyway). Deaths: quad falls flat 0.3 s + fades
  0.8 s, then removed (no instant pop, no 2.4 s clip). Dead-mid-swap guard:
  imposted corpses go straight to the dying-fade list.
- Charge/break at far zoom: positions are simulated regardless — imposters
  scatter/pack exactly like the real soldiers; banner reads the state.
- Selection at LOD: hide per-soldier discs; brighten the formation's
  instances + banner selection carries it.

### Camera framing (formulas verified; live bug found)
- Visible ground: halfWidth = S·aspect; halfDepth = S/sin(pitch).
- **maxZoom = min(fieldHalfX/aspect, fieldHalfZ·sin40°)** ≈ 94.6 at 19.5:9,
  109 depth-bound — the CURRENT maxZoom=120 already shows off-field ground
  at full zoom-out (live bug).
- Zoom-dependent pan clamps: limX = fieldHalfX − S·aspect, limZ = fieldHalfZ
  − S/sin40 (minus the existing zOffset bias); recompute on ZoomBy + aspect
  change; re-clamp target position immediately when zooming out.
- Fully zoomed out = 16 imposter batches + 16 banners; the entire
  skinned/animator/shadow stack dormant.

---

## C. Patch 2 — Controls & UI revamp (v0.11.0) [primary-session analysis]

- **Kill the bottom bar**: the panel Image is a raycast target across the
  whole screen width — it eats unit taps (real bug). Replace with floating
  standalone buttons (top-button style), rounded-rect sprite per A3 spec,
  anchored bottom-right; info text becomes a compact floating chip or merges
  into the selected banner. `PointerOverUI` continues to work per-button.
- **Drag = live slot preview**: replace the drag line with the 80-dot
  destination preview during the drag itself. `FormationDestinationPreview`
  gains a candidate mode: PlayerCommander feeds (candidateDest,
  candidateFacing) while dragging; on release it becomes the real order (or
  discards). Reuses the A2-fixed combined-mesh path. Keep the enemy-target
  ring for attack drags; drop the line entirely (or keep a faint line only
  for attack drags where dots don't apply).
- **Double-tap multi-select**: single tap = exclusive select (immediate, no
  timing lag); second tap on ANOTHER friendly century within the double-tap
  window while one is selected = ADD to selection (upgrade). Tap a selected
  century again = remove it. Empty tap clears. Edge cases: double-tap
  threshold ~0.3 s; camera pan unaffected; enemy tap still inspects.
- Also fold in sweep items #3/#4 (touch slop + tap padding DPI fixes).

---

## D. Patch 3 — Charge replaces Break Ranks (v0.12.0) [agent design, hybrid]

Historical grounding: Roman assault = controlled short surge (that's our
existing Attack order); "all soldiers rush and keep hunting" = pursuit/
warband behavior with a discipline cost; the signifer's standard MOVED with
the century, and planting it was the rally act.

**Design (c) hybrid, recommended:**
- `OrderType.Charge`, new `FormationState.Charging` (rush ~3–5 s or until
  contact): pack sprints (~1.3×) at the target's cluster, slot weight ~0.4,
  first-hit damage bonus ~1.5× inside the window (stacks with directional).
- Then degrades into the EXISTING BrokenRanks machinery = pursuit: individual
  fighting, zero slots, formation-level retarget of nearest enemy formation
  within ~35 m when the target dies (pack never map-chases; idles at cluster
  when nothing near). Moving leash: AnchorPos follows the smoothed dominant
  cluster (the one change to UpdateAnchorMovement); leash tightens ~18.
- **Banner follows the pack** (dominant cluster — the machinery exists) with
  the broken/faded treatment during pursuit; **Reform = "plant the
  standard"**: RallyAnchor snaps to the CURRENT cluster at the moment Reform
  is pressed, frozen from then on; all existing reform rules unchanged.
  (Consciously amends the fixed-anchor rule — the old rule marked where a
  *stationary* dissolve happened; a charge's standard moved with the men.)
- Edge cases: no enemy within ~45 m → order refused (ChargeBlock.NoTarget,
  greyed button); target dies → retarget ≤35 m or idle; mutual charges →
  symmetric, no special case; archers may charge (last resort) but get NO
  bonus (0.4× sidearm already prices it); arrows never abort phases
  (projectiles don't set IsEngaged); AI uses it rarely — reserves finishing
  weak/routing targets; morale hooks later (charging = brief immunity,
  being charged = impact shock, pursuing = casualties weigh heavier).

---

## E. Patch 4 — Unit variants + officers (v0.13.0) [primary-session scoping]

Plumbing already in place: `SoldierRole` slots per century, data-driven
`UnitStats`, extensible `BadgeSprites` map, prefab loading in SoldierFactory,
and the Blender source's modular equipment system (v014 `MOD_*` pieces —
crests, pteruges, shields, helmets) on the shared rig = reskins with ZERO new
animation work. Scope: (1) officer visuals for existing role slots
(centurion crest, signifer standard — the physical standard stays cosmetic;
the floating banner remains the logical banner), (2) 1–2 reskinned melee
variants with small stat deltas (e.g. veteran: +hp/+baseMorale later;
auxiliary: cheaper/lighter), each with banner badge variant. Composition
stays 80-total (specialists occupy slots). Export via the established
Blender→FBX pipeline; scene-stat pinning rules apply.

---

## F. Patch 5 — Morale & routing (v0.14.0) [agent integration of TW research]

Hidden continuous morale on Formation (0–100, base per UnitStats; archers
~75 vs melee 100), recomputed on the existing 0.25 s tick, zero per-frame
work. Modifier → existing-signal map: casualties (NotifyDeath/TotalSpawned),
morale shock (NEW decaying accumulator fed from TakeDamage, weighted by the
directional multiplier the combat already computes — rear hits hurt morale
2×), chain routs (formation scan ≤16 entries), surrounded (engagedCentroid
≈ anchor), commander aura (own Centurion alive — from Patch 4), winning
proxy (kill credit vs shock; needs attacker-formation threading — open
decision), regen when disengaged. FATIGUE: explicitly deferred (no signal).

States: Steady/Pressured/Shaken/Breaking/Routing + Shattered latch, with
dwell-time hysteresis (1.5 s escalate, 4 s rally window, 5-pt de-escalate
band). **Routing = parallel `IsRouting` flag on top of the existing
BrokenRanks state** (all order gates, rotate blocks, AI filters work
unchanged) + flee: anchor drags toward own map edge, the existing leash
pulls soldiers, acquire radius 0. Rally = TW-style (clear of danger →
halt, RallyAnchor = cluster, reform); repeat/rock-bottom = Shattered →
flees off-field permanently. Despawn path unregisters soldiers WITHOUT
Die() → existing victory check works; army-wide collapse (all formations
routing/shattered) ends the battle early for a pursuit read; both armies
collapsing = DRAW.

Banner: no morale bar — state escalation icons (yellow !, red !! + local
shake on the scale container only so the overlap solver is untouched,
white flag while routing — banner follows the FLEEING cluster, persists
until despawn even under imposter LOD where fleeing quads fade at the
edge), icon-only RALLIED pulse (text stays removed). AI: press
Breaking/Routing enemies (score bonus), reserves move up at Shaken.

Open decisions (owner): kill-credit threading vs skip winning-bonus;
chain-rout radius/cap tuning; leash-drag flee visual acceptability;
parallel-flag vs first-class state; victory timing (collapse vs off-field);
base morale + regen values; officer aura scope (own-century only for now);
fatigue deferred.

---

## G. Revised priority order

| # | Patch | Version | Contents |
|---|---|---|---|
| 1 | Mobile presentation | v0.10.0 | Imposter LOD + camera framing/zoom clamp + ALL section-A bug fixes (banner DPI, preview instancing, button PPU, MPB batching, shadows off, input DPI) |
| 2 | Controls & UI | v0.11.0 | Bar removal, standalone rounded-rect buttons, drag-time slot preview, double-tap multi-select |
| 3 | Charge | v0.12.0 | Hybrid rush→pursuit, banner-with-pack, plant-the-standard reform |
| 4 | Variants + officers | v0.13.0 | Officer visuals, 1–2 reskin variants, badge variants |
| 5 | Morale & routing | v0.14.0 | Full TW-adapted system per section F |
| — | Terrain | later | parked |

Section A folds into Patch 1 because every fix touches the same subsystems
(banner manager, preview renderer, HUD sprite, URP build config).
