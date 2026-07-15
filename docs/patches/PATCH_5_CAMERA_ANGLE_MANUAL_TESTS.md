# Patch 5 Manual Tests — Camera Angle and Readability

Play Mode, both `Battle` and `TestSkirmish`. None marked passed until
performed.

## Standard view

- [ ] Noticeably less top-down: torsos, faces, shields, and bows readable
- [ ] Battlefield still readable for tactics; no horizon/skybox visible

## Closest zoom (5.5)

- [ ] Whole characters visible; helmets/weapons don't clip the near plane
- [ ] Formation controls usable; labels readable, not dominating

## Maximum zoom-out (36)

- [ ] Whole battlefield visible; formations and labels readable
- [ ] Overview parity with the old angle

## Pan bounds

Pan hard N/S/E/W and into all four corners at close AND far zoom.

- [ ] Every battlefield edge reachable
- [ ] No excessive off-field emptiness; no clamp snapping while zooming

## Selection & commands

- [ ] Tap-select works at screen center, top, bottom, left, right
- [ ] Move/attack drags land where the finger points (ray-plane ground)
- [ ] Rotate previews and final facing match the drag direction
- [ ] Patch 4 wheels/about-faces behave identically at the new angle

## Gestures & UI

- [ ] Pinch zoom, pan glide, command drag, UI touch filtering, safe area all
      unchanged

## Labels & indicators

- [ ] Labels stay with their formations and above heads (billboarded)
- [ ] Selection discs and facing arrows stay flat on the ground

## Projectiles & rendering

- [ ] Arrow arcs remain visible and readable; no early culling
- [ ] Units keep shadows across the field (shadow distance 90)
- [ ] No z-fighting, no near-plane slicing, no vanishing ground

## Full battle

- [ ] Full 5v5 with no repeated errors, stable camera, acceptable performance
