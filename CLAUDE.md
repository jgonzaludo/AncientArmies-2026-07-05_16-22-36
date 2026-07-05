# Ancient Armies

Mobile-first real-time tactical game built in Unity 6 using URP.

The player commands formations of individually simulated ancient soldiers.

## Before Substantial Work

Read:

* `docs/GAME_VISION.md`
* `docs/V0_PROTOTYPE.md`

Do not expand the project beyond the current V0 requirements without explicit approval.

## Core Invariants

* Player control is formation-level.
* Soldiers are real individual simulated entities.
* Formations act as a behavioral and tactical harness.
* Ordered formations should be predictable.
* Combat can create local disorder.
* Breaking ranks intentionally increases individual freedom and chaos.
* Reforming requires sufficient disengagement from melee.
* Dead soldiers are removed and survivors close ranks when reforming.
* Tactical outcomes should be understandable rather than dominated by invisible randomness.

## Current Prototype Priorities

1. Ordered formation movement
2. Individual melee combat and death
3. Local disorder during engagement
4. Break ranks
5. Disengagement
6. Reforming survivors
7. Ranged combat only after the complete melee loop works

## Technical Direction

* Unity 6
* Universal Render Pipeline
* Real 3D world
* Fixed isometric-style orthographic camera
* Mobile-first landscape presentation
* Simple placeholder 3D characters for V0
* Use the Unity MCP when editor inspection or manipulation is useful

## Engineering Rules

* Prototype before polish.
* Prefer the smallest implementation that proves the gameplay thesis.
* Do not introduce systems solely for hypothetical future needs.
* Do not add morale, terrain, progression, multiplayer, or enemy strategy in V0.
* Inspect the current project before proposing architecture.
* Make changes in runnable checkpoints.
* Verify behavior in Unity rather than assuming code works.
