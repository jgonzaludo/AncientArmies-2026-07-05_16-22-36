# Graph Report - AncientArmies  (2026-11-08)

## Corpus Check
- 67 files · ~100,188 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 960 nodes · 1755 edges · 72 communities (33 shown, 39 thin omitted)
- Extraction: 97% EXTRACTED · 3% INFERRED · 0% AMBIGUOUS · INFERRED: 50 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `91c039b7`
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
- BattlefieldDecor
- Roadmap Patch Designs
- Team
- Unity Physics Modules
- SoldierFactory
- .Update
- .CreateFormation
- IReadOnlyList
- .CreateFormation
- .CreateFormation
- BattleVisuals
- .CreateFormation
- .UpdateStateMachine
- Vector3
- .Init
- FormationArrow
- Unity Terrain Modules
- Scene
- Camera
- .Die
- BattlefieldDecor
- IReadOnlyList
- Material
- Animator
- Color
- IEnumerator
- VS IDE Package
- Input System Package
- MaterialPropertyBlock
- EnemyCommander.cs
- BuildCheckTasks
- Color
- Dictionary
- GameObject
- Banner Sprite Import Tool
- IReadOnlyList
- Sprite
- Transform
- Vector2
- Camera
- float
- GUIStyle
- int
- LoadSceneMode
- RuntimeInitializeOnLoadMethod
- Scene
- Texture2D
- Vector3
- Dictionary
- IEnumerator
- List
- Font
- Text
- TextAnchor

## God Nodes (most connected - your core abstractions)
1. `Formation` - 127 edges
2. `Soldier` - 77 edges
3. `PlayerCommander` - 55 edges
4. `BattleHUD` - 42 edges
5. `BattleSetup` - 40 edges
6. `FormationBannerController` - 34 edges
7. `ScenarioRunner` - 32 edges
8. `BattleRecorder` - 31 edges
9. `FormationImposterRenderer` - 30 edges
10. `EnemyCommander` - 26 edges

## Surprising Connections (you probably didn't know these)
- `BattleRecorder` --references--> `BattleSetup`  [EXTRACTED]
  Assets/Scripts/BattleRecorder.cs → Assets/Scripts/BattleSetup.cs
- `BattleSetup` --references--> `CombatConfig`  [EXTRACTED]
  Assets/Scripts/BattleSetup.cs → Assets/Scripts/CombatConfig.cs
- `BattleSetup` --references--> `MoraleConfig`  [EXTRACTED]
  Assets/Scripts/BattleSetup.cs → Assets/Scripts/MoraleConfig.cs
- `FormationDestinationPreview` --references--> `OrderType`  [EXTRACTED]
  Assets/Scripts/FormationDestinationPreview.cs → Assets/Scripts/Formation.cs
- `Formation` --references--> `MoraleConfig`  [EXTRACTED]
  Assets/Scripts/Formation.cs → Assets/Scripts/MoraleConfig.cs

## Import Cycles
- None detected.

## Communities (72 total, 39 thin omitted)

### Community 0 - "Player Input & Camera Control"
Cohesion: 0.10
Nodes (15): bool, Dictionary, float, HashSet, int, OfficerRanks, Quaternion, Rigidbody (+7 more)

### Community 2 - "Formation State Machine"
Cohesion: 0.13
Nodes (12): BattleRecorder, bool, Dictionary, float, HashSet, int, List, string (+4 more)

### Community 3 - "Adaptive Banner System"
Cohesion: 0.09
Nodes (16): bool, Dictionary, float, GameObject, LineRenderer, List, Quaternion, Transform (+8 more)

### Community 4 - "Unity Package Manifest"
Cohesion: 0.37
Nodes (7): float, int, List, Vector3, Obstacle, RoutePlanner, Obstacle

### Community 5 - "Unity Editor Packages"
Cohesion: 0.10
Nodes (20): Battle lifecycle and spawning — `BattleSetup`, Camera — `BattleCamera`, Data structures, Enemy AI — `EnemyCommander`, Formation harness — `Formation`, Individual simulation — `Soldier`, Input — `PlayerCommander`, Knowledge graph workflow (+12 more)

### Community 6 - "Destination Preview & Arrows"
Cohesion: 0.06
Nodes (25): SafeAreaFitter, BattleVisuals, Color, float, GameObject, LineRenderer, Material, Mesh (+17 more)

### Community 7 - "Battle HUD"
Cohesion: 0.14
Nodes (3): MenuItem, Vector3, FormationManeuverValidation

### Community 8 - "Release Contract Docs"
Cohesion: 0.07
Nodes (22): FormationState, bool, Camera, Color, Dictionary, float, int, Material (+14 more)

### Community 9 - "Archer Visual Controller"
Cohesion: 0.08
Nodes (24): BattleHUD, Btn, BuildAdminPanel(), bool, float, Formation, string, bool (+16 more)

### Community 11 - "Enemy Commander AI"
Cohesion: 0.12
Nodes (15): Approval gates, Art pipeline, File formats, Hard rules, The chain, The Tripo Unity DCC Bridge, Tool status, Where things live (+7 more)

### Community 12 - "Legionary Visual Controller"
Cohesion: 0.10
Nodes (15): bool, Camera, Color, Dictionary, float, GameObject, int, List (+7 more)

### Community 13 - "Unity Web/Asset Modules"
Cohesion: 0.15
Nodes (8): AiPlan, bool, Dictionary, float, List, Vector3, EnemyCommander, CommanderPhase

### Community 14 - "Render Pipeline Packages"
Cohesion: 0.13
Nodes (14): Century roles, Combat, Controls (locked), Current game specification, Deliberately not in this build, Enemy AI, Formation cohesion behaviors, Formation states (+6 more)

### Community 15 - "URP Template Readme"
Cohesion: 0.14
Nodes (13): Ancient Armies — agent working rules, Art pipeline rules, Context recovery (new session or after compaction), Core invariants, Do not modify casually, Gameplay code boundaries, Git and the test gate, graphify (+5 more)

### Community 16 - "Battle Setup & Spawning"
Cohesion: 0.25
Nodes (7): Art direction, Built for formations, Faction color, Modularity, Proportions, Silhouette and equipment, The look

### Community 17 - "BattlefieldDecor"
Cohesion: 0.10
Nodes (23): AnimationClip, bool, Color, Dictionary, float, GameObject, HashSet, List (+15 more)

### Community 18 - "Roadmap Patch Designs"
Cohesion: 0.06
Nodes (25): CombatConfig, bool, float, float, int, FormationMorale, ShockSource, bool (+17 more)

### Community 19 - "Team"
Cohesion: 0.05
Nodes (26): Animator, BattleGrid, Dictionary, float, int, List, Vector3, TeamGrid (+18 more)

### Community 20 - "Unity Physics Modules"
Cohesion: 0.11
Nodes (11): AnimationCurve, bool, string, ScenarioFileTrigger, float, LineRenderer, List, RouteLines (+3 more)

### Community 21 - "SoldierFactory"
Cohesion: 0.13
Nodes (13): bool, float, GameObject, Material, Vector3, Projectile, bool, Color (+5 more)

### Community 22 - ".Update"
Cohesion: 0.07
Nodes (18): int, KayKitCrowdLod, Animator, bool, Dictionary, float, GameObject, int (+10 more)

### Community 23 - ".CreateFormation"
Cohesion: 0.09
Nodes (9): BattlePhase, BattleSetup, float, int, List, OfficerRanks, Team, UnitStats (+1 more)

### Community 26 - ".CreateFormation"
Cohesion: 0.08
Nodes (15): bool, string, KayKitAnimSheet, KayKitApiProbe, KayKitApiProbe2, KayKitBrows, KayKitHeadUV, KayKitIso (+7 more)

### Community 27 - ".CreateFormation"
Cohesion: 0.23
Nodes (4): BattleCamera, Camera, float, Vector3

### Community 29 - ".CreateFormation"
Cohesion: 0.33
Nodes (4): ChargeBlock, FormationManeuverState, OrderType, RotateBlock

### Community 30 - ".UpdateStateMachine"
Cohesion: 0.29
Nodes (6): Combat stats and time to kill, Damage per hit, Inputs, Measured in battle, Time to kill: ONE attacker, average skill, from the first hit to death, What this says

### Community 32 - ".Init"
Cohesion: 0.15
Nodes (13): Camera, Formation, string, Preset, ScenarioRunner, BattleSetup, CombatConfig, Dictionary (+5 more)

### Community 34 - "Unity Terrain Modules"
Cohesion: 0.22
Nodes (6): ClusterValidation, float, List, MenuItem, StringBuilder, Vector3

### Community 38 - "BattlefieldDecor"
Cohesion: 0.40
Nodes (3): BattlefieldDecor, Color, GameObject

### Community 45 - "Input System Package"
Cohesion: 0.08
Nodes (21): Camera, float, GUIStyle, int, LoadSceneMode, RuntimeInitializeOnLoadMethod, Scene, Texture2D (+13 more)

### Community 47 - "EnemyCommander.cs"
Cohesion: 0.50
Nodes (3): AiPlan, CommanderPhase, Role

### Community 52 - "Banner Sprite Import Tool"
Cohesion: 0.40
Nodes (3): BannerSpriteImport, MenuItem, string

## Knowledge Gaps
- **72 isolated node(s):** `CommanderPhase`, `Role`, `AiPlan`, `RotateBlock`, `ChargeBlock` (+67 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **39 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Formation` connect `Player Input & Camera Control` to `Formation State Machine`, `Adaptive Banner System`, `Unity Package Manifest`, `Destination Preview & Arrows`, `Battle HUD`, `Release Contract Docs`, `Legionary Visual Controller`, `Unity Web/Asset Modules`, `Input System Package`, `Roadmap Patch Designs`, `Team`, `Unity Physics Modules`, `SoldierFactory`, `.CreateFormation`, `IReadOnlyList`, `Vector3`, `BattleVisuals`, `.CreateFormation`?**
  _High betweenness centrality (0.257) - this node is a cross-community bridge._
- **Why does `Soldier` connect `Team` to `Player Input & Camera Control`, `Destination Preview & Arrows`, `Legionary Visual Controller`, `SoldierFactory`, `.Update`, `.CreateFormation`?**
  _High betweenness centrality (0.126) - this node is a cross-community bridge._
- **Why does `BattleHUD` connect `Archer Visual Controller` to `Destination Preview & Arrows`?**
  _High betweenness centrality (0.075) - this node is a cross-community bridge._
- **What connects `CommanderPhase`, `Role`, `AiPlan` to the rest of the system?**
  _72 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Player Input & Camera Control` be split into smaller, more focused modules?**
  _Cohesion score 0.09523809523809523 - nodes in this community are weakly interconnected._
- **Should `Formation State Machine` be split into smaller, more focused modules?**
  _Cohesion score 0.1282051282051282 - nodes in this community are weakly interconnected._
- **Should `Adaptive Banner System` be split into smaller, more focused modules?**
  _Cohesion score 0.0942684766214178 - nodes in this community are weakly interconnected._