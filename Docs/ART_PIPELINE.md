# Art pipeline

The active production pipeline. Adopted 2026-07-24 — see
[DECISIONS.md](DECISIONS.md).

## The chain

```
ChatGPT reference images
→ Tripo Studio Pro multi-view generation
→ manual Tripo inspection
→ Tripo Generate in Parts or separate component generation
→ Tripo Smart Mesh / low-poly preparation
→ Tripo Blender DCC Bridge
→ Blender assembly and cleanup
→ one Blender-owned canonical humanoid skeleton
→ Blender-owned skin weights, sockets, materials, and source files
→ Mixamo as an optional source for generic motion
→ Blender for equipment-specific animation and final animation cleanup
→ manual model and animation approval
→ FBX export
→ Unity runtime validation
→ Claude automation only after approval gates
```

## Tool status

- **Tripo Studio Pro is the selected initial generator.**
- **AssetHub is currently out of the production pipeline.**
- **Meshy is currently out of the production pipeline.**
- **Mixamo** is an optional source for generic motion only. Mixamo output is
  raw material for retargeting and cleanup, never a shipped animation.

## Hard rules

- **Raw Tripo → Unity transfer is for bridge testing and quick previews
  only.** It is never a production path.
- **Production character assets must pass through Blender.** No exceptions.
- **`.blend` is the editable character source of truth.** Unity assets are
  exports; if the two disagree, the `.blend` wins.
- **FBX is the standard Unity handoff** for rigged and animated characters.
- One **canonical humanoid skeleton**, owned in Blender. Skin weights, sockets,
  and materials are authored against it and live in the Blender source.
- **Claude automates validation and plumbing, not subjective creative
  approval.** Model and animation approval is a human decision, always.

## File formats

| Purpose | Format |
| --- | --- |
| Reference imagery | PNG or WebP |
| Textures | PNG and standard normal-map formats |
| Character source | `.blend` |
| Unity handoff | FBX |
| Visual review | MP4 or image sequences |
| Approval manifests and machine-readable reports | JSON |

## Where things live

Source production lives at the repository root, **never inside `Assets/`**:

```
ArtSource/
  References/
    Approved/          approved source reference images only
    Working/           unapproved or actively edited references
  Tripo/
    RawExports/        unmodified Tripo exports and bridge captures
    ApprovedExports/   Tripo results that passed manual visual approval
                       and may enter Blender production
  Blender/
    Characters/        authoritative character .blend files
    Rigs/              canonical skeleton and rig source files
    AnimationSources/  Mixamo downloads and other motion awaiting cleanup
    Reviews/           turntables, preview renders, review files, reports
  Textures/            editable source textures and working files
  Manifests/           JSON approval manifests and validation reports
```

Approved runtime assets land in Unity under:

```
Assets/AncientArmies/Art/Characters/Romans/BasicLegionary/
  Models/  Materials/  Textures/  Animations/  Controllers/  Prefabs/
```

Never place raw Tripo exports, reference imagery, Blender working files,
Mixamo source downloads, or review renders under `Assets/`.

## The Tripo Unity DCC Bridge

Installed as a local Unity package at
`ThirdPartyPackages/TripoUnityBridge/`, wired into `Packages/manifest.json` as
`com.tripo3d.unitybridge` via a repository-relative `file:` dependency.

Open it from **`Tools → Tripo Bridge`**.

The bridge exists for previewing and for verifying that transfer works. Assets
it drops into the project are **not** production-ready and must not be treated
as approved. Generated assets may not enter production runtime folders without
explicit approval.

## Approval gates

Two gates, both human:

1. **Tripo output approval** — before a generation moves from
   `Tripo/RawExports` to `Tripo/ApprovedExports` and into Blender.
2. **Model and animation approval** — before FBX export into `Assets/`.

Automation may prepare, validate, convert, and report at any point. It may not
pass either gate.
