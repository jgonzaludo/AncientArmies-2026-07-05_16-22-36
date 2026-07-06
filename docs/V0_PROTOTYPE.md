# Ancient Armies — V0 Prototype Contract

## Purpose

Build the smallest playable prototype that proves the core gameplay thesis:

Can the player control formations made of individual soldiers, watch those formations degrade into local disorder during combat, then disengage and reform into organized units?

The core transition is:

**Ordered formation → movement → engagement → disorder → disengagement → reform**

## Battlefield

* Flat 3D battlefield
* Fixed isometric-style camera
* Landscape mobile-first presentation
* Three player formations versus three enemy formations
* Player side is controllable
* Enemy formations remain strategically stationary but automatically fight back

## Required Commands

### Select

Select one player formation.

### Move

Move an ordered formation to a destination.

When already engaged, movement away from combat acts as an attempt to withdraw and disengage.

### Attack

Order a formation to approach and attack a specific enemy formation.

### Break Ranks

Intentionally weaken the formation harness.

Soldiers are allowed to fight and move more freely, producing a noticeably more chaotic engagement.

### Reform

Order surviving soldiers to rebuild an organized rectangular formation.

A formation cannot cleanly reform while actively engaged in melee. It must first create sufficient separation from enemies.

## Formation States

### Ordered

* Soldiers occupy individual positions in a rectangular formation.
* The formation moves coherently.
* Individual soldiers remain separate simulated entities.

### Attacking

* The formation approaches its target while remaining organized.
* Soldiers do not immediately become unrestricted independent agents.

### Engaged

* Disorder develops primarily near actual enemy contact.
* Soldiers involved in combat may leave their ideal positions.
* Unengaged soldiers should remain more organized.
* Soldiers automatically fight nearby enemies.

### Broken Ranks

* The formation harness is intentionally weakened.
* Soldiers may move and fight more freely.
* Combat becomes noticeably more chaotic.
* Regaining organized control requires disengagement and reforming.

### Reforming

* Soldiers stop seeking new engagements.
* Surviving soldiers move toward newly generated formation positions.
* Dead soldiers leave no permanent gaps.
* Survivors form the best practical smaller rectangle.

## Combat Requirements

* Individual soldiers have health.
* Individual soldiers take damage.
* Individual soldiers can die.
* Both sides automatically fight back.
* Melee combat is mandatory.
* Exact individual outcomes may vary.
* Overall engagement outcomes should remain understandable.

## Ranged Combat

Ranged combat is part of V0 only after the complete melee loop works.

It requires:

* one ranged formation
* individual projectiles
* individual targets
* individual damage and death

Ranged work must not delay proving the melee loop.

## Facing

* Every formation has a canonical forward direction.
* Formation rotation must be represented.
* Facing is visually real but has no combat bonus in V0.
* The final mobile gesture for controlling facing is not yet decided.

## Visual Requirements

Use extremely simple 3D placeholder soldiers.

They only need to communicate:

* team
* position
* facing
* movement
* engagement
* taking damage
* death
* ordered versus disordered behavior

Simple geometry and materials are acceptable.

Polished models and advanced animation are not required.

## Explicit Non-Goals

V0 does not include:

* morale
* autonomous enemy strategy
* terrain effects
* flanking bonuses
* progression
* unit levels
* campaign systems
* multiple formation shapes
* individual soldier control
* polished art
* advanced animation
* multiplayer

## Success Sequence

V0 succeeds if this sequence feels understandable and satisfying:

1. Select a blue formation.
2. Move it across the battlefield while it remains organized.
3. Order it to attack a red formation.
4. Watch front-line soldiers enter individual combat while unengaged soldiers remain more coherent.
5. Use Break Ranks and see the fight become noticeably more chaotic.
6. Watch individual soldiers on both sides take damage and die.
7. Pull the surviving formation away from combat.
8. Once sufficiently disengaged, issue Reform.
9. Watch survivors rebuild into a smaller organized rectangular formation.

If this sequence works and makes us want to continue experimenting, V0 succeeds.
