# Ancient Armies — Mobile Controls Contract

## Core Principle

Ancient Armies is a mobile-first game.

Every interaction must feel intentionally designed for touch and thumbs rather than copied from a mouse-and-keyboard RTS.

The primary control grammar is:

**Tap = select or inspect**

**Drag = issue orders**

The control system should remain fast, readable, and low-clutter.

Desktop mouse controls may exist as editor equivalents for testing, but they must directly mirror the intended touch interactions rather than define the design.

---

## Camera Controls

### One-Finger Drag on Empty Battlefield

Pan the camera.

The drag must begin on empty battlefield space.

### Pinch

Zoom the camera in or out.

The camera remains locked to its isometric-style viewing angle.

Do not add free rotation of the camera.

---

## Formation Interaction Area

The formation is always the interaction target.

The player is never expected to accurately tap one individual soldier.

Tapping or dragging from any of the following should count as interacting with the formation:

* any living soldier belonging to the formation
* the formation's selection area
* its visible banner or formation marker, if present

Individual soldiers remain simulated entities, but player interaction is formation-level.

---

## Friendly Formation Selection

### Tap Friendly Formation

Select that formation.

When selected:

* every living soldier in the formation receives a clean white selection circle on the ground beneath them
* the circles should be visible but not visually overwhelming
* one clear directional arrow appears for the formation
* the arrow shows the formation's canonical forward direction
* when the formation has a movement destination, the directional indicator should also make the intended heading or travel direction understandable

Do not create one arrow per soldier.

The white circles communicate:

**These soldiers belong to the current selection.**

The formation arrow communicates:

**This is the front of the formation and/or the direction this formation is currently heading.**

The selected formation also opens the bottom information panel.

---

## Friendly Formation Information Panel

When one friendly formation is selected, show a bottom information panel containing the currently available V0 information:

* unit name or unit type
* current formation health
* current strength or surviving soldier count
* current formation state

Future information such as morale may be added later but should not be implemented as part of this control correction unless already required elsewhere.

Friendly formations also receive relevant command buttons.

At minimum:

* Break Ranks
* Reform
* Rotate

Only show or enable commands that are valid for the current formation state.

---

## Enemy Formation Inspection

### Tap Enemy Formation With No Friendly Formation Selected

Inspect the enemy.

Open a read-only bottom information panel.

Display available information such as:

* unit type
* health
* surviving strength
* current visible state

Do not show friendly control buttons.

The player is inspecting the enemy, not selecting it for direct control.

---

## Multiple Formation Selection

The control architecture should support multi-selection.

### Tap Unselected Friendly Formation

Add it to the current selection.

### Tap Already Selected Friendly Formation

Remove it from the current selection.

### Tap Empty Ground

Deselect all selected formations.

When several formations are selected:

* all selected soldiers retain their white ground circles
* each selected formation should retain a readable formation-level facing/heading indicator
* the bottom panel should switch to an appropriate multi-selection summary rather than pretending one unit is selected

Do not overbuild advanced multi-selection behavior if it threatens the core V0, but do not architect the selection system in a way that assumes only one formation can ever be selected.

---

## Move Command

### Drag From a Selected Friendly Formation to Empty Ground

Issue a move order.

The drag must begin on the selected formation or its interaction area.

While dragging:

* show a lightweight preview of the intended destination
* show a thin command line or arrow from the formation toward the destination
* make the destination visually clear

On release:

* confirm the move order
* show a brief destination marker or pulse
* update the formation's visible heading/travel indicator appropriately

If the formation is already engaged in melee, moving away from combat should act as a withdrawal or disengagement attempt rather than allowing the formation to casually slide through combat.

---

## Attack Command

### Drag From a Selected Friendly Formation Onto an Enemy Formation

Issue an attack order against that enemy formation.

While dragging:

* show a command line or attack arrow
* clearly highlight the prospective enemy target

On release over a valid enemy:

* confirm the attack order
* briefly pulse or outline the enemy target
* update the formation's heading/command indicator

Do not require a separate permanent Attack button for ordinary attacks.

The battlefield interaction itself should issue the order.

---

## Camera and Command Drag Conflict

Use the drag origin to determine intent.

### Drag Begins on Empty Ground

Camera pan.

### Drag Begins on a Selected Formation

Command drag.

This distinction is fundamental.

Do not allow the same drag gesture to ambiguously pan the camera and command a formation.

---

## Break Ranks

Break Ranks is a contextual command button.

When pressed:

* weaken the formation-wide harness
* allow soldiers to pursue and fight more freely
* produce visibly more chaotic behavior
* preserve permanent formation membership

Provide immediate visual confirmation that the command was accepted.

The formation should visibly feel less ordered.

---

## Reform

Reform is a contextual command button.

It should only be available when the formation is sufficiently disengaged from active melee.

When Reform is unavailable:

* disable the button
* provide a short contextual reason when practical

Example:

**Too close to enemy**

Do not open a modal or large popup.

When Reform begins:

* show the intended formation area or destination rectangle when useful
* surviving soldiers return to newly generated slots
* dead soldiers leave no permanent gaps
* survivors create a smaller practical rectangle

---

## Rotation and Facing

Every formation has a canonical forward direction.

Facing is visually real in V0 even though it does not yet provide combat bonuses.

### Rotate Button

Pressing Rotate enters a temporary rotation mode.

In rotation mode:

* show a clear formation facing arrow
* allow the player to drag around the formation to preview a new direction
* update the arrow live during the preview
* release to confirm the new facing

Allow rotation mode to be cancelled by:

* pressing Rotate again
* pressing a small contextual cancel control

Do not require permanent left/right rotation buttons.

Do not implement a large PC-style rotation interface.

The eventual long-term system may combine movement destination and final facing into one continuous gesture, but that is not required now.

---

## Selection Visuals

The required V0 selection language is:

### Selected Soldiers

A clean white circle on the ground beneath every living soldier belonging to the selected formation.

The circle:

* follows the soldier
* sits flat on the battlefield
* remains readable from the isometric camera
* does not block the soldier model
* is removed immediately when the formation is deselected

### Formation Direction

One clear arrow per selected formation.

The arrow communicates:

* the formation's canonical front
* its current or commanded heading when moving

Do not create a direction arrow beneath every soldier.

The selection visuals should resemble the clarity of a mobile strategy game rather than a desktop debug visualization.

---

## Command Confirmation

Commands should be confirmed primarily through lightweight battlefield feedback.

### Move

* destination marker
* command line or arrow
* subtle visual pulse

### Attack

* target highlight
* attack line or arrow
* brief confirmation pulse

### Break Ranks

* immediate button-state feedback
* visible loosening of the formation behavior

### Reform

* visible reform destination or intended structure
* soldiers visibly returning to ordered positions

Avoid confirmation dialogs.

Avoid modal windows.

Avoid forcing the player to tap an additional Confirm button for ordinary actions.

---

## Cancelling and Replacing Orders

Do not add a permanent Cancel button for ordinary movement and attack commands.

Use these rules:

### Before Releasing a Command Drag

Returning the drag toward the originating formation may cancel the preview.

### After an Order Has Been Issued

A new valid order replaces the current order.

### Rotate Mode

Press Rotate again or use the contextual cancel control.

### Tap Empty Ground

Deselect all formations.

Deselecting a formation must not cancel the order it is already carrying out.

---

## Explicit V0 Input Non-Goals

Do not implement:

* keyboard-first gameplay
* right-click commands as the primary interaction model
* permanent Move and Attack toolbar buttons
* command confirmation dialogs
* individual soldier selection
* free camera rotation
* complex command wheels
* advanced control customization
* double-tap behavior unless a clear need emerges later

---

## Required Desktop Testing Equivalents

The Unity Editor may use mouse input to simulate touch behavior.

Map mouse behavior directly to the mobile interaction model:

* click = tap
* click-drag on empty ground = one-finger camera pan
* click-drag from selected formation = command drag
* scroll wheel may simulate pinch zoom for editor convenience

These are testing equivalents only.

Do not let desktop conventions redefine the mobile control system.

---

## Control Summary

**Drag empty battlefield**
→ Pan camera

**Pinch**
→ Zoom camera

**Tap friendly formation**
→ Select or add to selection

**Tap selected friendly formation**
→ Remove from selection

**Tap enemy with no friendly formation selected**
→ Inspect enemy

**Tap empty ground**
→ Deselect all

**Drag selected formation to ground**
→ Move or withdraw

**Drag selected formation to enemy**
→ Attack

**Break Ranks button**
→ Intentionally weaken the formation harness

**Reform button**
→ Rebuild organized formation after sufficient disengagement

**Rotate button, then directional drag**
→ Set formation facing
