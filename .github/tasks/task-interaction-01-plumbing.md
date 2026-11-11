# Task interaction-01: Plumbing

Source: [summary-interaction-v2.md](../summaries/summary-interaction-v2.md) §2, §3.2, §5.4, §6.1, §14 N1. Depends on: none.

## Goal

Prepare the node chain for notification-native interaction without changing what happens at runtime. After this task:
- the interaction layer is called `Interactive`;
- a node is a VL layer, not a behavior;
- nodes take behaviors through a pin group;
- the draw order has one accessor that rendering and hit testing will share;
- the `PointerEvents` style exists and is committed.

`Notify` stays a stub that returns false; delivery is N2.

## Design

### 1. Rename `Interactable` → `Interactive`

* `VL.Fu\src\Core\Interaction\Interactable.cs` → `Interactive.cs`.
  * `IInteractable` → `IInteractive`.
  * `Interactable<TSelf>` → `Interactive<TSelf>`, with the generic constraint `where TSelf : class, ITraversable<TSelf>, IInteractive`.
* `Renderable.cs`: `IRenderable : IInteractive, ILayer`, and `Renderable<TSelf> : Interactive<TSelf>`.
* Use the IDE rename, so the XML doc references follow.

### 2. A node is a layer, not a behavior

* `IInteractive : ILayoutable`. It drops the explicit `IBehavior` (v2 §2: nodes forward like layers, and behaviors are attached to them).
* `Interactive<TSelf>` keeps `public bool Notify(INotification, CallerInfo)` returning false. `IRenderable : ILayer` binds to it. Its doc says delivery comes with N2.
* VL's `ILayer` extends `IBehavior`, so a node is still an `IBehavior` by type. `SetBehaviors` therefore rejects Fu nodes with a warning (§6).

### 3. Delete `Gesture.cs`

* `VL.Fu\src\Core\Interaction\Gesture.cs`: an empty `abstract record Gesture` with no references. Typed gesture states replace it (v1 F1, N5).

### 4. `OrderedChildren`

* On `Interactive<TSelf>`: `protected IReadOnlyList<TSelf> OrderedChildren => Children;`.
* Doc: the children in draw order, back to front. Rendering walks it forward, and the hit test (N2) walks it backward. Task 05 (z-index) changes only this accessor.
* `Renderable.Render` iterates `OrderedChildren` instead of `Children`. No behavior change.

### 5. `PointerEvents` style

Mirrors `TransformOrigin`: a facade getter and setter, staged by a style, committed at the end of the pass only when the styled value changed.

* `VL.Fu\src\Core\Styles\PointerEventsStyle.cs`:
  * `public enum PointerEvents { Auto = 0, None = 1 }`. Each value has a multi-line doc:
    * `Auto`: the node and its subtree take part in hit testing;
    * `None`: hit testing skips the node and its subtree, so the pointer reaches what is behind them. Control notifications still reach them (v2 §3.2).
  * `public sealed record PointerEventsStyle(IReadOnlyList<IStyle> Inputs, PointerEvents PointerEvents) : Style<IFuNode>(Inputs)`.
  * `internal static bool IsSupported(PointerEvents)`.
  * `Build` throws `ArgumentOutOfRangeException` for unsupported values, like `TransformOriginStyle`. Otherwise it calls `((Interactive<IFuNode>)node).StagePointerEvents(...)`.
* On `IInteractive` and `Interactive<TSelf>`:
  * `PointerEvents PointerEvents { get; }` and `void SetPointerEvents(PointerEvents pointerEvents)`. This is the `X => _x; SetX(...)` pattern.
  * The setter logs a warning and ignores values that aren't defined. An equal value is a no-op.
* Staging on `Interactive<TSelf>`, as in `Layoutable`:
  * `internal void StagePointerEvents(PointerEvents)`;
  * `_styledPointerEvents` and `_committedPointerEvents`;
  * `BeginStyle` resets the staged value to `Auto`;
  * `EndStyle` commits through `SetPointerEvents` when the staged value differs from the last committed one, then calls `base.EndStyle()`. A value set directly is therefore kept until the style changes.
* `VL.Fu\src\Styles\PointerEventsNodes.cs`: `[ProcessNode(Name = "SetPointerEvents")] SetPointerEventsNode : StyleNode`.
  * `SetPointerEvents(Optional<PointerEvents> pointerEvents = default)`.
  * Validates with `ValidateEnum(pointerEvents, "PointerEvents", PointerEventsStyle.IsSupported)`.
  * `BuildStyle` returns null without a value.
* In N1 the committed value is only stored. N2 reads it in the hit test; N4 reads it in the lifetime rules (v2 §5.4 #5).

### 6. Behaviors pin group

Mirrors children: `IReadOnlyList` on the facade, `Spread` only on the VL pin group.

* On `IInteractive`:
  * `IReadOnlyList<IBehavior> Behaviors { get; }`, the attached behaviors in pin order;
  * `void SetBehaviors(IReadOnlyList<IBehavior>? behaviors)`.
* `Interactive<TSelf>`:
  * The field starts as `Spread<IBehavior>.Empty`. Null means empty.
  * When the sequence is reference-equal element by element to the current one, the call is a no-op.
  * Otherwise it is sanitized like children, and the sanitized list is stored:
    * an empty entry is ignored: "Ignored an empty behavior.";
    * a Fu node is ignored: it is a layer, so it goes into the children;
    * a repeated instance keeps only its first occurrence: "Ignored '{b}': it is already attached to this node.";
    * one warning lists the distinct problems.
* `FuNode`: `[Fragment(Order = PinOrder.Behavior)] public void SetBehaviors([Pin(PinGroupKind = PinGroupKind.Collection, PinGroupDefaultCount = 1)] Spread<IBehavior> behaviors)` calls the base. This is the same shape as `SetChildren`. `FuNodeSpectral` inherits it.
* No diff and no cancel on removal in N1. The removed-behavior cancel (v2 §5.4 #6) comes with N4, where `PointerCancelNotification` exists.
* Behaviors aren't disposed by the node: the patch that created them owns them.

### 7. Docs

* Rename `Interactable` to `Interactive` in the live docs:
  * `summary-core-inheritance.md`;
  * `summary-render-composition.md`;
  * `tasks-render-composition.md`;
  * `task-render-05-zindex.md`. Task 05 also points at `OrderedChildren` as its single change point.
* Proposals and reviews stay as written; they are history.

## Decisions (settled)

1. **`IInteractive` drops the explicit `IBehavior`.** Nodes are layers (v2 §2). VL layers are behaviors by type, so `SetBehaviors` rejects Fu nodes.
2. **`PointerEvents` has two values,** `Auto` and `None`, as in CSS for HTML. Fu has no SVG-style variants.
3. **`OrderedChildren` is not virtual.** Task 05 changes its body, not its overrides.
4. **Empty and repeated behaviors warn,** like children.

## Control

* `SetPointerEvents` (style node) or `SetPointerEvents` on the node. Removing the style setter brings back `Auto` on the next pass, unless the value was set directly.
* The `Behaviors` pin group on `FuNode`, pin order 25.

## Tests

* `VL.Fu.Tests\Core\Interaction\InteractiveTests.cs` (new):
  * `PointerEvents` defaults to `Auto`. A direct set is stored; an undefined value is ignored with a warning.
  * A style pass commits `None`. Removing the style returns to `Auto`. A direct value survives passes that don't change the style.
  * `SetBehaviors` stores in pin order. Null means empty. Empty entries and repeated instances are dropped.
  * Setting an equal sequence twice keeps the same list instance.
  * A Fu node passed as a behavior is ignored with a warning.
* `StyleNodeTests.cs`: `SetPointerEventsNode` builds a style with a value and none without one; an undefined enum value is ignored with a warning.
* `RenderableTests.cs`: the render order is unchanged. The existing tests cover it; no new test.
* The whole suite still passes (246 tests at the last run).

## Deferred

* `Notify` delivery, `FuNotificationSender`, hit test, and using `PointerEvents` there: N2.
* The removed-behavior cancel: N4.
* The z-index sort inside `OrderedChildren`: task 05.

## Risk

* Patches that refer to `IInteractable` or `Interactable` by type name need re-binding in vvvv after the rename. `.vl` files are out of scope here; the user fixes them in the IDE.
