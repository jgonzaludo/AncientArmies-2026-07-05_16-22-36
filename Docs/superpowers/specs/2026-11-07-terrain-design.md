# Terrain: design

Status: approved by the owner (2026-11-07). Plan and build in progress.

## Why

Battles are two armies on one flat sand-coloured plane, so ground never
matters and the field looks plain. The owner wants terrain to genuinely shape
fights over time: units route around things, hold good ground, avoid bad
ground, cross water at chokepoints. This first project builds the foundation
and a modest first set of terrain, in the KayKit visual style, so later work
(fog of war, weather, rivers as a tactical centrepiece, custom props) has
something real to sit on.

## Goals

- A **terrain map**: a grid over the battlefield saying what each spot is.
  Everything else (speed, blocking, concealment, visuals) reads from it.
- New ground and props in the KayKit style, replacing the sand plane.
- Terrain types in this first version: grass, road, mud, shallow ford,
  deep water (river), rock, forest (zone), plus bridges.
- Formations move at different speeds on different ground (mud and ford slow,
  road faster, forest slow).
- Deep water and rocks are not walkable. Bridges and fords are the crossings.
  The player's routes and the AI's routes both go around blocked ground.
- Forests are a zone: units walk through at reduced speed. Trees are scenery
  only, never individual obstacles. Forest cells carry a "concealed" flag that
  fog of war can use later.
- Maps are authored by hand as small painted images (one colour per terrain
  type). Two or three playable maps to start.

## Non-goals (this project)

- Fog of war (the concealed flag is the only hook).
- Hills and height. Cliffs (rock) are the stand-in.
- Weather, sunbeams, new lighting, water shaders or animated rivers. Lighting
  stays as it is.
- Random map generation. Maps are hand-painted for now. Random dressing
  (tree and rock scatter) on top of a painted layout is allowed but optional.
- Custom props. Use KayKit pieces and simple primitives.
- Any backend or stat saving.

## Anti-goals (a failure even if it technically works)

- Units walking through water or rocks, or the AI marching into a river.
- Terrain that makes outcomes unreadable or dominated by invisible effects.
- A player unable to see or select their own units inside a forest.
- A framerate drop on a phone, or planning stutter when orders are given.
- Rewriting the formation, soldier or command systems. Extend at the seams.

## Decisions the owner made

- Forests: visual plus concealment only, no per-tree navigation.
- Rivers and bridges: wanted, accepted as the hardest part.
- Trees: some, "nothing too crazy".
- Maps: authored by hand for now; hybrid (authored layout, generated dressing)
  later; historical recreations are a future wish.
- Style target: match the KayKit look; Bad North is the closest reference.

## Constraints from the codebase

- Field is 410 x 340 m (`fieldHalfX` 205, `fieldHalfZ` 170), currently a
  Unity Plane made in `BattleSetup` and coloured by `BattlefieldDecor`.
- `RoutePlanner` plans one route per formation, treating other formations as
  circles (visibility graph, Dijkstra, string-pull, corner rounding). The
  enemy AI plans through it too.
- Formation speed is `moveSpeed * SpeedMultiplier` in `Formation`. Soldiers
  follow formation slots and use `BattleGrid` for separation. Whether a
  soldier can be pushed through a blocked cell by its slot has not been
  checked and must be.
- `CLAUDE.md`: formations and soldiers have fixed owners; commanders only call
  `Issue*`; visuals are presentation only; morale tunables live in a
  ScriptableObject; scene-serialized values override C# defaults; mobile
  performance matters.
- Camera is a fixed isometric orthographic view, so trees can hide units.
  Banners and unit labels are already drawn on screen, not on the units.

## Approach (the owner agreed to this shape)

1. **Terrain map** data and loader: a painted image becomes a grid. Queries:
   what is at this position, speed multiplier, blocked or not, concealed or not.
2. **Visuals**: ground and props built from the map, KayKit style.
3. **Speed**: formations scale movement by the terrain under their anchor.
4. **Blocking and routing**: a grid route planner (A* with per-type costs,
   blocked cells for deep water and rock, bridges and fords as the gaps),
   straightened, with the existing planner still handling other formations on
   top. Applies to player routes, AI routes and route lines.
5. **Forests**: trees fade or thin near the player's own units so they stay
   visible and selectable; enemy concealment waits for fog of war.

Build in checkpoints, each of which must run and be testable on its own:
ground and visuals, then speed, then blocking and routing, then forests.

## Left to the builder

- Grid cell size and the exact image format.
- Exact speed multipliers and costs (start with the table below and tune).
- How soldiers behave near blocked cells, once checked.
- Tree and prop placement, KayKit pieces used, how trees fade.
- Where terrain data lives (a ScriptableObject, per `CLAUDE.md`, is preferred).

Starting speed multipliers (tune in play): road 1.15, grass 1.0, forest 0.7,
mud 0.6, ford 0.5, deep water and rock blocked.

## Separate deliverable (not part of the build)

A reference board and a set of image prompts so the owner can generate mood
images of the target terrain style. Other games' screenshots cannot be
bundled, so this would be links plus generated mood images, requested after
approval.

## Open questions

- Should the player be able to see terrain type while giving orders (a
  highlighted overlay), or is the look alone enough? *(builder's call, small)*
- Should a move order onto blocked ground snap to the nearest walkable spot or
  be refused? *(builder's call, small)*

## Calls made by the author (not yet confirmed by the owner)

- **Maps:** each map is a `TerrainMap` ScriptableObject (painted image plus
  size). Field stays 410 x 340 m on every map for now. The active map is a NEW
  field on `BattleSetup` (so the scene's pinned values cannot override it), set
  in the scene or by the test harness. No map-select UI yet.
- **Deployment zones:** both armies' start areas must be walkable and clear.
  A validation check flags a map that breaks this.
- **Blocked ground and footprints:** routing inflates blocked cells by the
  formation's half-width, and bridges and fords are painted at least one
  formation wide. Chokepoints therefore delay a line but never trap it.
- **Planner layering:** the grid planner owns the final waypoint list. The
  existing `RoutePlanner` is wrapped, not rewritten: it plans around other
  formations, and any leg it produces that crosses a blocked cell is rejected
  in favour of the grid route. Planning is synchronous on order; the grid is
  about 2 m cells (roughly 205 x 170), which is cheap for A*.
- **Orders onto blocked ground:** the player's order snaps to the nearest
  reachable walkable spot and the ground marker shows where it really went.
  The AI never targets blocked or unreachable ground.
- **Speed:** multiplies with the existing `SpeedMultiplier` for marching and
  charging, smoothed across cell borders; routing formations (fleeing) ignore
  terrain.
- **Forest tapping:** trees have no colliders, so taps still reach banners and
  units. The fade is a zone mask or shader, not per-tree logic.
- **Performance:** static or combined meshes and instancing for props; no
  regression from the current numbers in `Docs/DECISIONS.md` (about 49 fps at
  mid zoom in the editor).
- **Acceptance per checkpoint:** the owner plays the map in Play mode, plus
  AI-vs-AI runs through the file-trigger harness on each new map to confirm
  nobody walks into water or rocks and no battle stalls.

## Owner decisions after review (2026-11-07)

- Design approved as written, including the calls above.
- Tree and rock art comes from the free KayKit Forest Nature Pack (CC0), imported
  under `Assets/ThirdParty/KayKit/Forest`. Bridge, ford stones and water are made
  in-house as simple placeholders. Lychee Studio is a backup for fancier one-offs.
- Target look: `ArtSource/References/terrain_mockups/` (four mockups made by
  editing a real screenshot). Match their grass, road, mud, river, forest and rock
  look and their soft low-sun lighting, within the limits of the KayKit pieces.
  Pack greens are brighter than the mockups, so tint materials toward the muted
  warm look.
- Maps are hand-made. Hills and height are out.

## Update (2026-11-08)

The KayKit forest pack was tried and removed; terrain art is 2d painted sprites and ground textures made with Higgsfield from the approved mockups. See `Docs/DECISIONS.md`.
