# Ancient Armies

Mobile-first real-time tactical game built in Unity 6 using URP.

The player commands formations of individually simulated ancient soldiers.

## Before Substantial Work

Read:

* `docs/GAME_VISION.md`
* `docs/V0_PROTOTYPE.md` (historical V0 contract)
* `docs/V1_RELEASE.md` (V1 contract and manual test suite)
* `docs/V1_2_COMBAT_CONTROL.md` (current release contract and manual test suite)
* `docs/NEXT_RELEASE.md` (deferred future scope — do not implement without approval)

Do not expand the project beyond the current release requirements without explicit approval.

## Codebase Knowledge Graph (RAG)

`graphify-out/` (project root, local-only, not committed) holds a prebuilt
knowledge graph of this codebase and its docs. When searching for how systems
relate, what calls what, or where a concept lives, query it before grepping
broadly:

* `graphify query "<question>"` — traversal answer from the graph
* `graphify-out/graph.json` — raw nodes/edges (GraphRAG-ready)
* `graphify-out/GRAPH_REPORT.md` — communities, god nodes, audit trail
* `graphify-out/graph.html` — interactive visualization (open in a browser)
* `graphify-out/obsidian/` — Obsidian vault of the same graph

After substantial code changes, refresh it with `/graphify . --update`.

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

V0 (formation systems sandbox), V1 (first real battle vs. an autonomous enemy:
larger formations, enemy AI, archer preferred range, directional combat),
V1.1 (coordinated commander AI with target scoring; 5v5 battles with a random
melee/archer mix each round; wider battlefield), and V1.2 (exclusive tap
selection with auto-deselect after orders; forgiving formation touch targets;
dense melee spacing; auto-close and rear-rank pressure; edge engagement;
pivot-in-place rotation; farther archer range; defeated-label fix; mid-match
restart; UI tidy-up) are complete.
See `docs/V1_RELEASE.md`, `docs/V1_1_CHANGES.md`, and
`docs/V1_2_COMBAT_CONTROL.md` for shipped scope and manual test suites.

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
