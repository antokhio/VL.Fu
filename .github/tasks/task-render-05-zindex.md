# Task 05: Sibling z-index

Source: summary §4.1, §7 row 4; proposal §5 Z1. Depends on: 04 (shares the child walk).

## Goal

Change the draw order among siblings without changing the layout. A higher `ZIndex` draws later, so on top; equal values keep tree order. The scope is siblings only: a child never draws below its parent's layer or above its parent's next sibling. That needs the overlay (task 06).

## Design

* Record `ZIndexStyle(IReadOnlyList<IStyle> Inputs, int ZIndex) : Style<IFuNode>`; node `SetZIndex` with `Optional<int>`; default 0.
* Staged on `Interactive` rather than `Renderable`, since hit-testing will walk children in reverse draw order. `BeginStyle` resets it to 0 and `EndStyle` commits it.
  * When the committed value changes, the node marks its parent's draw order dirty.
* The parent keeps a cached draw order, returned by `Interactive.OrderedChildren` (added in [task-interaction-01](task-interaction-01-plumbing.md)). That accessor is the only change point:
  * Fast path: every child is 0, so it returns `Children` directly, with no allocation (the common case).
  * Otherwise, a cached `TSelf[]` stably sorted by `(ZIndex, tree index)`, rebuilt only when dirty. It is marked dirty on `OnChildrenChanged` and on a child's z change.
* `Render` and `ChildrenLayer` (task 04) walk the draw order forward. `Notify` walks it backward, once interaction lands.
* Yoga children stay in tree order (`SetChildren` untouched), so flex order never changes with z.

## Decisions

1. **Placement.** On `Interactive` (recommended, shared with hit-testing) or on `Renderable`?
2. **Negative values.** They sort before zero-z siblings but stay above the parent's own layer, unlike CSS, where they can go below the parent's background. Confirm that this restriction is acceptable.

## Tests

* A higher z draws on top of a later sibling; ties keep tree order; negative values draw below zero-z siblings.
* The layout is identical with and without z.
* Removing `SetZIndex` restores tree order on the next pass.
* Reordering `Children` with z set re-sorts.
* With all z at 0, the children draw in tree order.
