# Versioning, Branching, and Commit Conventions

Agreed 2026-07-18. These rules are binding for every session and every agent
working in this repo. When in doubt, this file wins.

## Branches

| Branch | Rule |
|---|---|
| `main` | Always playable. Only receives bundles the OWNER has manually tested and approved. Every tag lives here. |
| `dev` | Permanent working branch. All day-to-day work lands here first. |
| `feature/<name>` | Optional, off `dev`, only for risky experiments (e.g. `feature/morale`). Merge back to `dev` when stable, then delete. |
| `v0-prototype`, `v1-prototype` | Frozen archives of the old flow. Never commit to them. |

## Version numbers — `0.MINOR.PATCH`

- **MINOR** — a feature bundle ("patch wave" with new systems). Example: the
  morale/rout patch bumps `0.9.x → 0.10.0`.
- **PATCH** — fixes/tuning-only bundle. Example: `0.9.0 → 0.9.1`.
- **`1.0.0` is reserved for actual ship.** Do not use 1.x before that.
- Design milestones (V0, V1, V2 in `docs/`) are PHASE NAMES, not versions.
  Never derive a version number from them.
- Versions exist as **git tags on `main`** (`v0.9.0`) plus a matching
  **CHANGELOG.md** entry. When cutting a device build, set Unity
  `PlayerSettings.bundleVersion` to the tag (build-time ritual only).

## The per-patch ritual (THE test gate is mandatory)

1. Implement on `dev`. Compile + self-checks.
2. **STOP. Give the owner concrete Play Mode test steps and WAIT for their
   explicit green light.** Never commit, push, merge, or tag before it.
3. On green light: commit on `dev` (conventions below) → merge `dev → main`
   with `--no-ff` → tag `vX.Y.Z` on `main` → CHANGELOG entry rides the merge
   → push `dev`, `main`, and tags.
4. Docs-only changes still ask before committing, but need no Play Mode test.

## Commit messages — Conventional Commits

`<type>(<scope>): <imperative subject ≤72 chars>`

- Types: `feat`, `fix`, `tune`, `refactor`, `docs`, `chore`, `perf`, `test`.
- Scopes (extend as systems grow): `combat`, `formation`, `banner`, `ui`,
  `input`, `ai`, `camera`, `deploy`, `anim`, `pacing`, `scene`.
- Body explains WHY (constraints, root causes), not a diff narration.
- Keep the existing `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`
  trailer on agent-authored commits.
- One logical change per commit within a bundle when practical.

## CHANGELOG.md

Keep-a-Changelog style: one section per tag, newest first, with
`Added / Changed / Fixed / Tuned` subsections. The changelog is the single
answer to "what is in this build".

## Scene-serialization rule (repo-specific, easy to violate)

Before changing any tunable's C# default, grep `Assets/Scenes/Battle.unity`
for the field. If serialized there, the scene value WINS — stamp the scene
component in-editor (set field → SetDirty → SaveScene) as part of the patch,
and remember the in-memory component keeps LOAD-TIME values across domain
reloads: explicitly re-stamp every field whose default changed before saving.

## Migration state

- [x] Conventions agreed and documented (this file).
- [x] `dev` branch created from `v1-prototype` tip.
- [x] Retroactive anchor tag `v0.8.0` on the overhaul merge (`b63de53`).
- [x] Bundle landed as `v0.9.0` — first tag under the new scheme (owner
      green-lit 2026-07-18).
- [x] `CHANGELOG.md` created with the `v0.9.0` entry.
