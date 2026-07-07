# Ancient Armies

Mobile-first real-time tactical game built in Unity 6 using URP.

The player commands formations of individually simulated ancient soldiers.

## Before Substantial Work

Read:

* `docs/GAME_VISION.md`
* `docs/V0_PROTOTYPE.md` (historical V0 contract)
* `docs/V1_RELEASE.md` (current release contract and manual test suite)

Do not expand the project beyond the current release requirements without explicit approval.

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

## Current State

V0 (formation systems sandbox) and V1 (first real battle vs. an autonomous enemy:
larger formations, enemy AI, archer preferred range, directional combat) are complete.
See `docs/V1_RELEASE.md` for the shipped V1 scope and its manual test suite.

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
* Do not add morale, terrain, progression, multiplayer, or advanced enemy tactics without approval.
* Inspect the current project before proposing architecture.
* Make changes in runnable checkpoints.
* Verify behavior in Unity rather than assuming code works.
