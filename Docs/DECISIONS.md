# Decisions

Dated record of decisions that shape the project. Newest first. A decision
stays in force until a later entry supersedes it.

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

**Scope.** No morale, terrain, progression, multiplayer, or advanced enemy
tactics without explicit approval. Prototype before polish; prefer the smallest
implementation that proves the gameplay thesis.

**Git LFS is not configured** and was deliberately not introduced during the
reset. See the note in `README.md`/`Docs/ART_PIPELINE.md` about file sizes —
this will need revisiting once real `.blend` masters land in `ArtSource/`.
