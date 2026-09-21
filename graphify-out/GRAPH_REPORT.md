# Graph Report - AncientArmies  (2026-09-21)

## Corpus Check
- 40 files · ~49,107 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 727 nodes · 1340 edges · 34 communities (27 shown, 7 thin omitted)
- Extraction: 95% EXTRACTED · 5% INFERRED · 0% AMBIGUOUS · INFERRED: 68 edges (avg confidence: 0.82)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `1caa2cdc`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Player Input & Camera Control
- Soldier Combat Simulation
- Formation State Machine
- Adaptive Banner System
- Unity Package Manifest
- Unity Editor Packages
- Destination Preview & Arrows
- Battle HUD
- Release Contract Docs
- Archer Visual Controller
- Imposter LOD Renderer
- Enemy Commander AI
- Legionary Visual Controller
- Unity Web/Asset Modules
- Render Pipeline Packages
- URP Template Readme
- Battle Setup & Spawning
- Unity Media Modules
- Roadmap Patch Designs
- Unity Capture Modules
- Unity Physics Modules
- Unity UI Toolkit Modules
- Unity IMGUI/uGUI Modules
- Unity Terrain Modules
- VS IDE Package
- Input System Package
- Blue Melee Badge Art
- Blue Ranged Badge Art
- Red Melee Badge Art
- Red Ranged Badge Art
- Banner Sprite Import Tool
- Collections Package
- Bool Type
- Unity Editor Version 6000.4.8f1

## God Nodes (most connected - your core abstractions)
1. `Formation` - 113 edges
2. `Soldier` - 74 edges
3. `PlayerCommander` - 56 edges
4. `BattleHUD` - 35 edges
5. `FormationBannerController` - 33 edges
6. `BattleSetup` - 30 edges
7. `FormationImposterRenderer` - 30 edges
8. `BattleRecorder` - 23 edges
9. `EnemyCommander` - 23 edges
10. `FormationDestinationPreview` - 20 edges

## Surprising Connections (you probably didn't know these)
- `TeamGrid` --references--> `Soldier`  [EXTRACTED]
  Assets/Scripts/BattleGrid.cs → Assets/Scripts/Soldier.cs
- `BattleHUD` --references--> `PlayerCommander`  [EXTRACTED]
  Assets/Scripts/BattleHUD.cs → Assets/Scripts/PlayerCommander.cs
- `BattleRecorder` --references--> `BattleSetup`  [EXTRACTED]
  Assets/Scripts/BattleRecorder.cs → Assets/Scripts/BattleSetup.cs
- `BattleSetup` --references--> `MoraleConfig`  [EXTRACTED]
  Assets/Scripts/BattleSetup.cs → Assets/Scripts/MoraleConfig.cs
- `BattleSetup` --references--> `OfficerRanks`  [EXTRACTED]
  Assets/Scripts/BattleSetup.cs → Assets/Scripts/OfficerRanks.cs

## Import Cycles
- None detected.

## Communities (34 total, 7 thin omitted)

### Community 0 - "Player Input & Camera Control"
Cohesion: 0.06
Nodes (19): MenuItem, Vector3, FormationManeuverValidation, ChargeBlock, bool, float, HashSet, int (+11 more)

### Community 1 - "Soldier Combat Simulation"
Cohesion: 0.08
Nodes (19): bool, string, ScenarioFileTrigger, Dictionary, IEnumerator, List, string, Preset (+11 more)

### Community 2 - "Formation State Machine"
Cohesion: 0.14
Nodes (11): BattleRecorder, bool, Dictionary, float, HashSet, List, LoadSceneMode, RuntimeInitializeOnLoadMethod (+3 more)

### Community 3 - "Adaptive Banner System"
Cohesion: 0.07
Nodes (19): BattleCamera, Camera, float, Vector3, bool, Camera, Dictionary, float (+11 more)

### Community 4 - "Unity Package Manifest"
Cohesion: 0.33
Nodes (5): Color, Material, Vector3, SoldierFactory, PhysicsMaterial

### Community 5 - "Unity Editor Packages"
Cohesion: 0.11
Nodes (18): Battle lifecycle and spawning — `BattleSetup`, Camera — `BattleCamera`, Data structures, Enemy AI — `EnemyCommander`, Formation harness — `Formation`, Individual simulation — `Soldier`, Input — `PlayerCommander`, Major systems (+10 more)

### Community 6 - "Destination Preview & Arrows"
Cohesion: 0.05
Nodes (25): SafeAreaFitter, BattleVisuals, Color, float, GameObject, LineRenderer, Material, Mesh (+17 more)

### Community 7 - "Battle HUD"
Cohesion: 0.07
Nodes (20): Animator, DamageZone, Rigidbody, float, Material, Vector3, Projectile, bool (+12 more)

### Community 8 - "Release Contract Docs"
Cohesion: 0.07
Nodes (21): FormationState, bool, Camera, Color, Dictionary, float, int, Sprite (+13 more)

### Community 9 - "Archer Visual Controller"
Cohesion: 0.13
Nodes (13): BattleHUD, Color, float, GameObject, IReadOnlyList, Sprite, string, Transform (+5 more)

### Community 10 - "Imposter LOD Renderer"
Cohesion: 0.17
Nodes (9): Camera, float, GUIStyle, int, LoadSceneMode, RuntimeInitializeOnLoadMethod, Scene, Vector3 (+1 more)

### Community 11 - "Enemy Commander AI"
Cohesion: 0.13
Nodes (13): Approval gates, Art pipeline, File formats, Hard rules, The chain, The Tripo Unity DCC Bridge, Tool status, Where things live (+5 more)

### Community 12 - "Legionary Visual Controller"
Cohesion: 0.10
Nodes (15): bool, Camera, Color, Dictionary, float, GameObject, int, List (+7 more)

### Community 13 - "Unity Web/Asset Modules"
Cohesion: 0.14
Nodes (9): CommanderPhase, bool, Dictionary, float, List, Vector3, EnemyCommander, Role (+1 more)

### Community 14 - "Render Pipeline Packages"
Cohesion: 0.13
Nodes (14): Century roles, Combat, Controls (locked), Current game specification, Deliberately not in this build, Enemy AI, Formation cohesion behaviors, Formation states (+6 more)

### Community 15 - "URP Template Readme"
Cohesion: 0.15
Nodes (12): Ancient Armies — agent working rules, Art pipeline rules, Core invariants, Do not modify casually, Gameplay code boundaries, Git and the test gate, Knowledge graph, Naming conventions (+4 more)

### Community 16 - "Battle Setup & Spawning"
Cohesion: 0.25
Nodes (7): Art direction, Built for formations, Faction color, Modularity, Proportions, Silhouette and equipment, The look

### Community 18 - "Roadmap Patch Designs"
Cohesion: 0.07
Nodes (20): float, int, FormationMorale, bool, float, MoraleConfig, bool, float (+12 more)

### Community 20 - "Unity Physics Modules"
Cohesion: 0.06
Nodes (22): BattlefieldDecor, Color, GameObject, BattleGrid, Dictionary, float, int, List (+14 more)

### Community 34 - "Unity Terrain Modules"
Cohesion: 0.22
Nodes (6): ClusterValidation, float, List, MenuItem, Vector3, StringBuilder

### Community 45 - "Input System Package"
Cohesion: 0.09
Nodes (17): Camera, float, GUIStyle, int, LoadSceneMode, RuntimeInitializeOnLoadMethod, Scene, Texture2D (+9 more)

### Community 48 - "Blue Melee Badge Art"
Cohesion: 0.60
Nodes (5): UI_Banner_Blue_Melee.png — blue melee formation banner badge: gold-framed shield/pennant-shaped banner with a blue field and a white upward-pointing sword icon, tapering to a gold chevron point with a downward gold arrow pointer beneath; stylized mobile-game UI sprite, Blue faction identity — royal-blue banner field marking the formation as belonging to the blue (player) side, Formation banner badge — UI marker identifying a formation's faction and unit class in the tactical battle view, Gold banner frame and pointer — ornamental gold border with chevron-notched bottom edge and a downward arrow pointer for anchoring the badge above a formation in-world, White sword icon — upward-pointed broad sword glyph signifying the melee soldier class

### Community 49 - "Blue Ranged Badge Art"
Cohesion: 0.50
Nodes (5): UI_Banner_Blue_Ranged.png — blue faction ranged-unit banner badge sprite: a vertical shield/banner shape with a rounded top and chevron-notched bottom, ornate golden metallic border and a small gold arrowhead pendant hanging below, deep blue field bearing a white recurve bow with a nocked arrow crossing it diagonally, Blue faction identity — deep blue banner field signalling the blue (player) side, distinguishing team ownership at a glance, Bow-and-arrow icon — white stylized recurve bow crossed by an arrow, the iconography identifying the formation as a ranged/archer unit, Formation banner badge — UI marker floating over a formation on the battlefield to show its team and unit class (here: blue archers), Gold banner frame — ornate golden border with chevron base and arrowhead pendant, the shared heraldic banner silhouette used across formation badges

### Community 50 - "Red Melee Badge Art"
Cohesion: 0.60
Nodes (5): UI_Banner_Red_Melee badge sprite, Formation banner badge UI (per-formation class/faction marker), Gold pennant frame with swallowtail bottom and pointer tip, Red banner field (red/enemy faction color), White upward sword icon (melee class glyph)

### Community 51 - "Red Ranged Badge Art"
Cohesion: 0.50
Nodes (5): UI_Banner_Red_Ranged.png — red faction ranged formation banner badge: gold-bordered heraldic banner sprite with swallow-tail chevron bottom edge and downward gold arrow pointer, red field bearing a white bow-and-arrow icon, Bow-and-arrow icon — white stylized recurve bow crossed by a fletched arrow, the class glyph signifying ranged/archer units, Formation banner badge — in-world UI marker floating above a formation identifying its faction and unit class at a glance, Gold banner frame — beveled cartoon-style gold border with chevron swallow-tail base and pendant arrow tip pointing down at the formation it labels, Red faction identity — deep red banner field marking the formation's team allegiance (enemy/red side)

### Community 52 - "Banner Sprite Import Tool"
Cohesion: 0.40
Nodes (3): BannerSpriteImport, MenuItem, string

### Community 60 - "Bool Type"
Cohesion: 0.67
Nodes (4): URP Template Icon (SRP badge), Scriptable Render Pipeline (SRP), Unity URP Template Readme (TutorialInfo), Universal Render Pipeline (URP)

## Knowledge Gaps
- **72 isolated node(s):** `CommanderPhase`, `Role`, `RotateBlock`, `ChargeBlock`, `PointerMode` (+67 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **7 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Formation` connect `Player Input & Camera Control` to `Formation State Machine`, `Adaptive Banner System`, `Unity Package Manifest`, `Destination Preview & Arrows`, `Battle HUD`, `Release Contract Docs`, `Archer Visual Controller`, `Legionary Visual Controller`, `Unity Web/Asset Modules`, `Input System Package`, `Roadmap Patch Designs`, `Unity Physics Modules`?**
  _High betweenness centrality (0.340) - this node is a cross-community bridge._
- **Why does `Soldier` connect `Battle HUD` to `Player Input & Camera Control`, `Unity Package Manifest`, `Destination Preview & Arrows`, `Legionary Visual Controller`, `Input System Package`, `Unity Physics Modules`?**
  _High betweenness centrality (0.148) - this node is a cross-community bridge._
- **Why does `PlayerCommander` connect `Adaptive Banner System` to `Player Input & Camera Control`, `Destination Preview & Arrows`, `Release Contract Docs`, `Archer Visual Controller`, `Input System Package`?**
  _High betweenness centrality (0.126) - this node is a cross-community bridge._
- **What connects `CommanderPhase`, `Role`, `RotateBlock` to the rest of the system?**
  _75 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Player Input & Camera Control` be split into smaller, more focused modules?**
  _Cohesion score 0.05985915492957746 - nodes in this community are weakly interconnected._
- **Should `Soldier Combat Simulation` be split into smaller, more focused modules?**
  _Cohesion score 0.0784313725490196 - nodes in this community are weakly interconnected._
- **Should `Formation State Machine` be split into smaller, more focused modules?**
  _Cohesion score 0.14130434782608695 - nodes in this community are weakly interconnected._