# Task 06: Overlay / portal (deferred)

Source: summary §4.2; proposal §5 Z3. Depends on: 05, interaction (`Notify`).

## Goal

Let dropdowns, tooltips and popups escape their ancestors' clip, effects and sibling order, and draw above the whole tree.

## Outline

* A node marked as an overlay, by a style or a dedicated node, is skipped in the normal walk. Instead it registers itself with the root, together with its matrix relative to the root at that moment.
* After the main walk, the root draws the registered overlays in registration order, or by z.
* `Notify` visits overlays before the main tree, so the topmost element gets the pointer first.
* The layout is unchanged: the overlay is still placed by its parent's Yoga layout, and only drawing and input move.

## Open, to settle when interaction exists

* Should overlays inherit their ancestors' effects (opacity) or not? CSS portals don't.
* Is the registration list rebuilt every frame, or kept by the root?
* Where should one-frame-late placement cause trouble (an overlay anchored to a node that moved this frame)?
