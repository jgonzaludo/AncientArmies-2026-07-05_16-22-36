# Ancient Armies

A mobile-first real-time tactical game built in Unity 6 with the Universal
Render Pipeline. The player commands formations of ancient soldiers; every
soldier in a formation is an individually simulated agent, and the formation
is the tactical harness that keeps them coherent.

Control is formation-level. Ordered formations are predictable, combat
creates local disorder, and reforming requires genuine disengagement.

## Requirements

- **Unity 6000.4.8f1** (see `ProjectSettings/ProjectVersion.txt`)
- Universal Render Pipeline 17.4.0
- Input System 1.19.0 (the project runs on the new Input System only)

## Opening the project

1. Open the repository root as a Unity project in Unity Hub.
2. Unity resolves packages from `Packages/manifest.json`. One dependency is a
   local package in this repository:
   `com.tripo3d.unitybridge` → `file:../ThirdPartyPackages/TripoUnityBridge`.
   It is repository-relative, so it resolves on any clone with no per-machine
   setup.

There are no build or install commands — this is a plain Unity project.

## Running the game

Open **`Assets/Scenes/Battle.unity`** and enter Play mode. It is the only
scene in Build Settings and the normal way to run the game.

The battlefield, both armies, the camera rig, and the HUD are all created at
runtime by `BattleSetup`; the scene itself is close to empty by design. Press
**START** in the HUD to begin the battle.

Other scenes: `Assets/Scenes/TestSkirmish.unity` (smaller sandbox) and
`Assets/Scenes/SampleScene.unity` (URP template leftover).

## Repository structure

```
Assets/                     Unity project assets
  Scenes/                   Battle.unity (main), TestSkirmish, SampleScene
  Scripts/                  All runtime gameplay code
  Editor/                   Editor-only validation and integration tools
  Art/                      Current runtime art (models, materials, banners)
  Settings/                 URP render pipeline assets and volume profiles
  AncientArmies/            Clean destination tree for new production work
Packages/                   Unity package manifest and lock file
ProjectSettings/            Unity project configuration
ThirdPartyPackages/         Local Unity packages (Tripo Unity DCC Bridge)
ArtSource/                  Art production source — NEVER inside Assets/
Docs/                       Authoritative project documentation
```

`ArtSource/` holds references, Tripo exports, Blender masters, animation
sources, and review renders. None of it ships in the game; only approved FBX
and texture exports enter `Assets/`.

## Documentation

| Document | Authority on |
| --- | --- |
| [Docs/CURRENT_GAME_SPEC.md](Docs/CURRENT_GAME_SPEC.md) | What the game currently does |
| [Docs/TECHNICAL_ARCHITECTURE.md](Docs/TECHNICAL_ARCHITECTURE.md) | How the code is structured |
| [Docs/ART_DIRECTION.md](Docs/ART_DIRECTION.md) | How the game should look |
| [Docs/ART_PIPELINE.md](Docs/ART_PIPELINE.md) | How art gets made and imported |
| [Docs/DECISIONS.md](Docs/DECISIONS.md) | Why things are the way they are |
| [CLAUDE.md](CLAUDE.md) | Working rules for coding agents |
