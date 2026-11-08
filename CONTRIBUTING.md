# Contributing

How we name things and write commits in this repo. These rules apply to old
history too, not just new work.

## Commits

Commits and PR titles use Conventional Commits 1.0:

    <type>(<scope>): <short summary, imperative, lowercase, no period>

    <optional body: what and why, wrapped at about 72 columns>

Types:

| type     | use it for                                   |
|----------|----------------------------------------------|
| feat     | a new user-visible capability                |
| fix      | a bug fix                                    |
| docs     | documentation only                           |
| test     | tests and the scenario harness               |
| refactor | code change that is not a fix or a feature   |
| perf     | a performance improvement                    |
| build    | build, packages, project settings            |
| ci       | automation                                   |
| chore    | everything else                              |

The scope is the area you touched, for example `morale`, `formation`, `ai`,
`controls`, `camera`, `hud`, `visuals`, `deploy`, `docs`. Leave it off when a
change spans several areas.

Breaking changes use `!` after the type or scope and a `BREAKING CHANGE:`
line in the body.

Keep commits small and logical: one idea per commit, and the project should
compile at every commit.

## Naming

Work is named by the feature, never by milestone or phase numbers. No "M0",
"phase 2", "chunk A", "step 3" or "patch 6" in commit subjects, branch names,
tags, file names, docs or code comments. Say what the change does instead.

Branches are `<type>/<short-name>`, for example `feat/plan-check` or
`fix/card-overlap`. Tags that mark a safe point are named for what they
protect, for example `pre-art-reset`.

## Workflow

Work happens on short-lived branches and merges into `main` by pull request.
PR titles follow the same format as commits. Merge with a merge commit, not a
squash, so the small logical commits stay in history.

## Style

No em dashes anywhere (docs, comments, UI copy). Use a comma, a colon or a
new sentence. Code comments are casual and lowercase.
