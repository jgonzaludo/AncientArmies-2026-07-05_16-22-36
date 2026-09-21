# Decisions

Dated record of decisions that shape the project. Newest first. A decision
stays in force until a later entry supersedes it.

---

## 2026-09-21 — Morale, routing, automatic recovery, and the win condition

**Morale is approved.** This supersedes the "No morale" clause in the standing
Scope convention below, which is updated to match. Terrain, progression,
multiplayer, and advanced enemy tactics still require explicit approval.

**Every formation has a Morale value from 0 to 100.** Casualties lower it.
Morale recovers with time out of melee, up to a **rest ceiling** set by the
formation's cumulative losses — a century that has lost more men cannot
recover as high. While in melee, a stricter **contact ceiling** applies, so a
slow, steady bleed eventually routs a unit even when no single moment of
losses would.

**All morale tunables live in a new `MoraleConfig` ScriptableObject, never as
fields on `BattleSetup`.** `BattleSetup` fields are pinned by `Battle.unity`,
so a changed C# default can silently do nothing; a ScriptableObject asset
avoids that. This is the project's first ScriptableObject.

**States follow the original four-state design.**

- **Ordered** and **Engaged** — unchanged.
- **Disordered** — the design name for the live `BrokenRanks` state. Loose,
  agent-native combat; soldiers keep fighting. This is what a century becomes
  after a charge. Behavior unchanged.
- **Routing** — a new state, entered when morale falls below 15. Soldiers flee
  and stop acquiring targets, the formation's badge disappears, and the
  formation cannot be ordered. It rallies automatically once morale is above
  35 and it is not in melee. Arrow fire does not block rallying directly, but
  casualties from it still lower morale. A routing formation with pursuers
  within melee range therefore cannot rally.

`Attacking`, `Withdrawing`, `Reforming`, and `Charging` are not changed by
this decision.

**The Reform button is removed; all recovery is automatic.** Disordered
formations reform on their own after disengaging (see *Automatic reform*
below). Routing formations rally on their own when morale recovers. Reforming
still requires disengagement from melee — the game decides when, not the
player.

**Automatic reform.** A Disordered formation reforms automatically whenever
it is **not in melee**. Only active melee blocks it: enemies nearby do not,
and arrow fire does not. The soldiers run to their slots and reform even with
enemies close. One shared rule serves both sides — it replaces the
`reformSafeRadius` check in `EnemyCommander.TryReformBroken`, and the AI gets
no rule of its own.

"In melee" uses the code's existing engagement check: a soldier is engaged
when an enemy is within `personalEngageRadius` (3 m), scanned every 0.25 s.
A formation is in melee while at least one soldier is engaged. A reform starts
when none are; a reform already under way keeps its existing abort rule (12 %
of survivors engaged, `reformAbortEngagedFraction`).

*Consequence, accepted:* a Disordered century after a charge cannot be
controlled until it is out of melee and has reformed. A charge is a
commitment.

**`MoraleConfig` wiring.** `BattleSetup` holds a serialized reference to the
`MoraleConfig` asset, assigned in `Battle.unity`. That reference is the only
morale-related field on `BattleSetup`. If it is null, the game logs a loud
error — it never silently falls back to default values.

**Win condition.** A side collapses when 50 % of its formations are Routing or
destroyed. The count is read at runtime from that side's actual formations —
never a hardcoded 8 — so a different army size works unchanged. **Collapse
latches:** once a side reaches it, the battle is decided, even if units rally
afterwards. This replaces the current rule, which ends the battle only when one
side has no living soldiers.

**Time limit.** A visible 10:00 countdown. When it expires, the side with more
intact formations wins. Draws are allowed.

**Not yet decided.** Deliberately left open, to settle before or during
implementation:

- Recovery rates, and the exact shape of the rest and contact ceilings.
- Whether a reform that aborts on melee contact retries immediately or after
  a delay. Soldiers running to their slots can pass within 3 m of an enemy,
  abort, and restart the moment contact clears.
- Where a routing formation flees to, and where it re-forms after rallying.
- Whether "50 %" means at least half, and how odd formation counts round.
- What counts as "intact" at the time limit — for example, whether a
  Disordered formation counts.
- Whether both sides collapsing on the same check is a draw.

---

## 2026-07-24 — Art pipeline hard reset: Tripo Studio Pro + Blender

**Generator.** **Tripo Studio Pro is selected as the first 3D generator.** Pro
was purchased and the Tripo → Blender DCC Bridge transfer was verified working.

**Removed from the pipeline.**

- **AssetHub is removed from the current pipeline.**
- **Meshy is removed from the current pipeline.**

Neither may be revived as the active pipeline without a new decision recorded
here.

**Blender owns production.** **Blender owns the canonical rigs and production
assets** — one canonical humanoid skeleton, skin weights, sockets, materials,
and the `.blend` source files. `.blend` is the editable source of truth; FBX is
the Unity handoff.

**Mixamo is optional.** Mixamo is an optional source for **generic motion
only**. Equipment-specific animation and all final cleanup happen in Blender.

**Tripo Unity Bridge is preview and testing only.** Installed as a local Unity
package at `ThirdPartyPackages/TripoUnityBridge/`, opened from
`Tools → Tripo Bridge`. Raw Tripo → Unity transfer is for verifying the bridge
and for quick previews. It is not a production path, and generated assets may
not enter production runtime folders without explicit approval.

**Production assets require manual approval.** Two human gates: Tripo output
approval before Blender, and model/animation approval before FBX export into
Unity. Automation handles validation and plumbing, never subjective creative
approval.

**Old documentation and art experiments were intentionally removed.** Every
first-party Markdown document, the entire `docs/` tree (reference sheets,
turnarounds, review renders, production logs, patch notes, art specifications),
the previous `ArtSource/` folder organization, all Meshy-generated assets and
Meshy skill installs, the `meshy_output/` directory, and the unreferenced
`Assets/_SpikeHumanoid` retarget spike were deleted rather than archived.
Documentation was rewritten from scratch against the actual repository.

**Git history preserves the removed work.** No `Archive/`, `Legacy/`, `Old/`,
or `_Deprecated/` directory exists or should be created. The tag
`pre-tripo-phase-1-hard-reset` marks the tree immediately before cleanup.

*Caveat, recorded honestly:* `ArtSource/` and `meshy_output/` were gitignored
and untracked, so git history does **not** contain them. Their deletion was
permanent and was confirmed as intentional. Roughly 1.5 GB of superseded
Blender masters and Meshy candidate exports are gone for good.

**Runtime placeholders retained.** The existing `Assets/Art/` legionary and
archer models, materials, animator controllers, banner sprites, and battlefield
textures were kept. They are visually superseded but every one of them is
referenced by `Battle.unity`, a prefab, or a runtime `Resources.Load` call —
the game does not run without them. They are temporary placeholders pending
Tripo/Blender production.

---

## Standing conventions

**Git.** Work on `dev`; `main` receives only owner-tested bundles. Versions are
`0.MINOR.PATCH` git tags on `main`. Commits follow Conventional Commits with
game scopes.

**The test gate.** Never commit, push, merge, or tag a gameplay change until
the owner has manually tested it in Play mode and explicitly approved it.

**Scope.** No terrain, progression, multiplayer, or advanced enemy tactics
without explicit approval. (Morale was approved on 2026-09-21.) Prototype
before polish; prefer the smallest implementation that proves the gameplay
thesis.

**Git LFS is not configured** and was deliberately not introduced during the
reset. See the note in `README.md`/`Docs/ART_PIPELINE.md` about file sizes —
this will need revisiting once real `.blend` masters land in `ArtSource/`.
