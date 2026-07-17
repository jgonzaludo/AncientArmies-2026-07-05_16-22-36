# Patch 6 — Manual Test Suite (Facing, Banners, Melee Attacks)

Run in Play Mode on the Battle scene unless noted. Automated columns record
the scripted Play Mode sweep; eyeball columns need a human pass at device
zoom levels.

## A. Facing and arrow

| # | Step | Expect |
|---|---|---|
| A1 | Select an idle formation, ROTATE, drag a small angle | soldiers pivot and the ground arrow turns with them (same direction) |
| A2 | Rotate again with a large drag (wheel) | arrow tracks AnchorForward continuously through the wheel |
| A3 | Order an attack on an enemy to the side | formation turns toward the enemy; arrow tracks the turn; ROTATE greys out |
| A4 | While auto-facing, tap ROTATE | button is non-interactable; no rotate preview appears |
| A5 | Enter rotate mode, then drag the formation onto an enemy (order attack) | rotate preview cancels itself; no queued manual facing fights the auto turn |
| A6 | Let the enemy circle the formation (or move it with a second front) | facing and arrow keep tracking the contact centroid while Engaged |
| A7 | Kill the target | ROTATE re-enables; formation holds its final facing (no snap back) |
| A8 | BREAK RANKS while targeting | arrow disappears entirely; ROTATE greyed |
| A9 | REFORM clear of enemies | arrow returns pointing the reformed facing; ROTATE available |
| A10 | REFORM while an enemy is nearby and re-engage | auto-facing takes control again; arrow tracks; ROTATE greyed |
| A11 | Formation destroyed | no arrow, no banner; selection clears |
| A12 | `Ancient Armies / Validate Facing Pipeline` in Play Mode | no BAD BINDING / MISSING MOTION; per-formation forward report sane |

## B. Banners

| # | Step | Expect |
|---|---|---|
| B1 | Battle start | every living formation shows one banner: red/blue pennant, sword icon for melee, bow icon for archers |
| B2 | March a formation across the field | banner glides with it (no jitter, no lag spikes) |
| B3 | Break a 50-melee formation; lure ~40 one way, ~10 the other | banner stays over the ~40 group; never floats in the empty middle |
| B4 | Shuffle a few soldiers between groups (26/24-ish) | banner does not flip-flop between groups |
| B5 | REFORM after B3 | slots form around the 40-group's position; the 10 walk back to it; banner recenters on the reformed block |
| B6 | Kill the dominant cluster during B3 | banner transfers to the surviving group (after its size clearly wins) |
| B7 | Reduce a formation to one soldier | banner floats over that soldier |
| B8 | Wipe a formation | banner disappears immediately; nothing remains at the death site |
| B9 | RESTART mid-battle | fresh banners, no duplicates, colors/icons match the re-rolled composition |
| B10 | Tap a formation through/near its banner | selection works — the banner never blocks input |
| B11 | Zoom min/max | icon readable at max zoom; banners don't balloon at min zoom |
| B12 | `Ancient Armies / Validate Dominant Group` | 8/8 PASS |

## C. Legionary attacks

View at normal zoom and max zoom, formations fighting in several world
directions (north/south/east/west engagements).

| # | Check | Expect |
|---|---|---|
| C1 | Thrust | straight forward stab through a narrow shield lane; no rising punch; retracts on the same line |
| C2 | Over-shield | sword raised above the upper-right of the planted shield, chops forward-down; never starts under the shield |
| C3 | Diagonal slash | high-right prep, compact cut to lower-left across the enemy; no spin, no huge lateral swing |
| C4 | Guard return | all three recover to the same engaged guard (shield front, sword right); no pose snap |
| C5 | Variety | repeated strikes mix all three (thrust most common); never the same clip 3+ times in a row |
| C6 | Damage timing | hit flash on the defender lands at visible blade contact, not at swing start or after recovery |
| C7 | Cooldown | attack rate unchanged (~1.4 s); no faster attacks from shorter clips |
| C8 | Formation discipline | soldiers stay planted in slots while striking; no foot drift after repeated attacks |
| C9 | Neighbors | side-by-side legionaries don't produce overlapping giant swings |
| C10 | Archers | archer draw/release/knife animations identical to v1.3.8 (untouched) |
| C11 | Deaths/hits | death and hit reactions unchanged |

## D. Camera

| # | Check | Expect |
|---|---|---|
| D1 | Battle start framing | pitch 40°, same center ground focus as before; pan clamps still reach both lines |
