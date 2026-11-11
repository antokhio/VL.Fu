# Interaction Pipeline Research & Synthesis (`VL.Fu`)

This document compares four existing interaction implementations across our projects and synthesizes the most reasonable architecture for `VL.Fu`.

---

## 1. Audited Codebases

| Project | Path | Core Pattern |
|---|---|---|
| **1. `VL.Fu5` (`VL.Fu` + `VL.Fu.Core`)** | `D:\VVVV\VL.Fu5\VL.Fu\src\Interaction` | Central `NotificationsService` + `InteractionService` (Rx `Scan` pointer dictionary, hit-test discovery, priority conflict resolution) + `ChannelProperty<T>` (`ChannelBase<T>` merged with user `IChannel<T>`). |
| **2. `VL.Fu5\Fu.Interaction` (Prototype)** | `D:\VVVV\VL.Fu5\Fu.Interaction` | Lightweight `IInteractionBehavior : IBehavior`, `Behavior`, and `abstract record class Gesture` with `GestureState { Started, Updated, Completed, Canceled }`. |
| **3. `VL.Hronomapper.Canvas`** | `D:\VVVV\__hronomapper\Hronomapper\src\VL.Hronomapper.Canvas\src` | Composable `GestureHandler<T> : IObservable<T>` (backed by `BehaviorSubject<T>`) + `Behavior<T> : IObservable<T>` using Rx `Merge` + `Scan` state machines, priority-sorted flat dispatch with explicit `Cancel()` propagation to lower-priority behaviors. |
| **4. `VL.LidarToolkit.Calibrator`** | `D:\VVVV\___rmi\rmi-lidars\VL.LidarToolkit.Calibrator\src\Behaviors` | Pure `VL.Skia.IBehavior.Notify(INotification, CallerInfo)` tree-walking with zero services; freezes `caller.Transformation.TryInvert(out _dragInverse)` on capture; uses custom `BehaviourNotification` (`GestureCancelledNotification`, `HitTestNotification`, `SelectionDragNotification`) for tree broadcasts. |

---

## 2. Detailed Comparison of the Three Complete Approaches

### A. `VL.Fu5` (Service-Driven + Global `FuPointer` Stream + `ChannelProperty<T>`)

**How it works:**
* `FuRoot` requires a separate root node (`IContextProvider`) that registers `ViewportService`, `NotificationsService`, and `InteractionService`.
* `NotificationsService` converts `MouseNotification`, `TouchNotification`, and `KeyNotification` into Rx streams (`PointersHandler` builds `IReadOnlyDictionary<int, FuPointer>` using `Scan` and `Scheduler.Default.Schedule` for cleanup).
* `InteractionService` runs on every input tick: traverses the tree for hit tests, evaluates `GestureBase` state machines, resolves conflicts by `Priority` (skipping `IsTransient` behaviors like `Hoverable`), and calls `OnStart` / `OnUpdate` / `OnFinish` / `OnCancel` on `BehaviourBase`.
* Outputs use **`ChannelProperty<T> : ChannelBase<T>, IChannel<T>`**, which binds to an external `IChannel<T>` via `this.Merge(_boundChannel, ChannelMergeInitialization.UseB, ChannelSelection.Both)`.

**What worked well (Keep):**
* **`ChannelProperty<T>` two-way channel merging:** A behavior has its own internal `ChannelBase<T>` so `Value` always works as a getter even when no external `IChannel<T>` pin is connected. When the user connects an `IChannel<T>`, `Merge(..., UseB, Both)` synchronizes them bidirectionally!
* **`IsTransient` concept:** Distinguishing non-blocking/passive behaviors (`Hoverable`) from exclusive/blocking behaviors (`Clickable`, `Draggable`) so hovering never cancels or steals clicks/drags.
* **Unified `Pointer` abstraction:** Treating primary mouse and touch through a common pointer model so `Clickable` / `Draggable` work with both mouse and touch.

**What was problematic (Avoid):**
* **Requires `FuRoot` / `IContextProvider` DI container:** In the new `VL.Fu`, there is **no `FuRoot` node**—every `FuNode` is self-sufficient and its root is discovered automatically via `Traversable` (`Parent == null`).
* **`Scheduler.Default.Schedule` in `PointersHandler`:** Asynchronous cleanup of `TouchUp` pointers introduced threading/frame-timing complexity and required workarounds (`_lastProcessedPointer`) in `DraggableBase` and `Movable`.
* **Disconnected from `CallerInfo.Transformation`:** Relied on a separate `ViewportService` rather than `VL.Skia`'s native `CallerInfo.Transformation` matrix stack.

---

### B. `VL.Hronomapper.Canvas` (Rx `GestureHandler<T>` + `Behavior<T> : IObservable<T>` + Cancel Propagation)

**How it works:**
* Two-tier reactive composition:
  1. **Low-level `GestureHandler<T> : IObservable<T>`** (`MouseDragHandler`, `MouseClickHandler`, `TouchTapHandler`, `KeyGestureHandler`, `PanZoomHandler`, `MouseWheelHandler`) — each wraps a `BehaviorSubject<T>` where `T : struct, IGestureState` (`Idle`, `Possible`, `Started`, `Active`, `Ended`, `Canceled`).
  2. **High-level `Behavior<T> : IBehavior<T>, IObservable<T>`** (`HoverableBehavior`, `MovableBehavior`, `SelectableBehavior`) — merges one or more `GestureHandler` streams (e.g., `MouseDrag` + `ShiftKey` + `CtrlKey` + `ArrowKeys` in `MovableBehavior`), reduces them via `.Scan(...)`, and pushes to `Subject`.
* **Dispatch & Cancel Loop:**
  ```csharp
  bool isHandled = false;
  foreach (var behavior in Behaviors)
  {
      if (!isHandled)
      {
          if (behavior.Notify(notification))
              isHandled = true; // Stop propagation, but DO NOT break!
      }
      else
      {
          // Higher-priority behavior claimed the event -> cancel pending state in lower-priority ones!
          behavior.Cancel();
      }
  }
  ```

**What worked well (Keep):**
* **Continue-and-`Cancel()` loop:** When a higher-priority behavior (or a child on top, or a `Drag` crossing its movement threshold) claims an event (`returns true`), continuing the loop to call `Cancel()` on remaining behaviors cleanly aborts pending `Possible` clicks or hovers without ghost triggers!
* **`GestureState` lifecycle (`Idle` → `Possible` → `Started` → `Active` → `Ended` / `Canceled`):** Having `Possible` (mouse down, waiting for drag threshold or click release) vs `Started`/`Active` allows both `Click` and `Drag` to coexist on the same node: `MouseDown` puts both into `Possible` (returning `false` so neither blocks the other yet); if the mouse moves past `Threshold`, `Drag` enters `Started` (returns `true`) and `Cancel()`s the pending `Click`!
* **Self-healing on missed `MouseUp`:** In `MouseDragHandler.HandleMouseDown`, if `!state.IsIdle()` when a new `MouseDown` arrives, it calls `Cancel()` first so a lost release never bricks the gesture.
* **`PositionInWorldSpace` helper (`notification.GetPosition()`):** Relies directly on `NotificationWithPosition.PositionInWorldSpace`.

**What was problematic (Adapt for `VL.Fu`):**
* **Synchronous pulse trap in `SelectableBehavior`:** `MouseClickHandler` emitted `Started` → `Ended` → `default(Idle)` synchronously inside a single `MouseUp` call, which meant `Subject.Value.IsClicked` was already `false` when `Notify` returned, requiring a temporary `using var probe = Subject.Subscribe(...)` allocation on every single notification!
  * *Fix for `VL.Fu`:* Keep `GestureHandler` allocation-free during `Notify` by returning the gesture transition directly (or holding `Ended` until the next event / mainloop read) instead of allocating a temporary Rx subscription per event.
* **Heavy Rx chains per behavior instance:** Creating 7 `GestureHandler`s + `.Select` + `Observable.Merge` + `.Scan` per node is fine for a few canvas items, but in a general UI library (`VL.Fu`), a simple state-machine `Gesture` struct/class inside `Behavior<TState>` achieves the exact same `Idle/Possible/Started/Updated/Completed/Canceled` transitions with zero per-event heap allocations.

---

### C. `VL.LidarToolkit.Calibrator` (Pure `VL.Skia.IBehavior` + Frozen Capture Matrix + Notification Broadcasts)

**How it works:**
* Zero services, zero global state. Every behavior is a `[ProcessNode]` inheriting `BehaviorBase : IBehavior`.
* `CompositeBehaviourNode` holds `IBehavior[]`, deduplicates by `ReferenceEqualityComparer.Instance`, and sends `new GestureCancelledNotification(this)` immediately to any behavior removed from the pin group or disabled.
* **Matrix Freeze on Drag Start (`DraggableNode.StartDrag`):**
  ```csharp
  // Freeze the space at capture: the object's transform may change as it moves!
  if (!caller.Transformation.TryInvert(out _dragInverse))
      return false;
  ```
  During `MouseMove` and `MouseUp`, it maps `mouseMove.Position` (window pixels) using `_dragInverse` rather than the moving node's current local transform.
* **In-Band Custom `BehaviourNotification`s:** Uses custom `INotification` subclasses (`GestureCancelledNotification`, `HitTestNotification`, `MarqueeSelectionNotification`, `SelectionDragNotification`) piped through the standard `Notify(INotification, CallerInfo)` tree!

**What worked well (Keep):**
* **Freezing the reference space on Drag Start (`_dragInverse`):** Crucial insight! If a `Draggable` behavior modifies a node's position/translation channel, the node's `LocalTransformation` changes on the next frame. If you map `MouseMove` using the *moving* node's local space, the delta feedbacks/oscillates! Freezing the parent/reference inverse matrix at `StartDrag` (or computing `PositionAtStart + (currentInParentSpace - startInParentSpace)`) makes dragging rock-solid.
* **Lifecycle hygiene on `SetBehaviors` & `SetEnabled` & `Dispose`:**
  * Filtering `null`s and deduplicating by `ReferenceEqualityComparer.Instance` when reading the behaviors input list.
  * Immediately cancelling any behavior that is removed from `SetBehaviors`, disabled via `SetEnabled(false)`, or disposed, and cancelling an ongoing drag if its bound `IChannel<T>` is swapped out!
* **Pure `Notify(INotification, CallerInfo)` compatibility:** Works anywhere in `VL.Skia` without needing a special `FuRoot` wrapper.

---

## 3. Recommended Synthesis for `VL.Fu` ("Best of All Four")

Combining the strengths of all four codebases gives `VL.Fu` a clean, self-contained, allocation-light architecture that matches the rest of `VL.Fu`:

### 1. Tree-Native `Notify` (from *Calibrator* + `VL.Core.Skia`) — No `FuRoot` Service Required
* `Interactable<TSelf>` implements `VL.Skia.IBehavior.Notify(INotification, CallerInfo)` directly on the node hierarchy (mirroring `Renderable<TSelf>.Render` in reverse).
* At each node:
  1. Check `HasLayout && IsDisplayed`.
  2. Concatenate `LocalTransformation` onto `caller.Transformation` and wrap the notification with `LocalSpaceNotificationSender` so `notification.PositionInWorldSpace` is in the node's local `(0, 0, Layout.Width, Layout.Height)` pixel space.
  3. Gate children with `ClipArea` (`Overflow.Hidden` / `Scroll`), then route to `Children` in reverse order (`Count - 1` down to `0`).
  4. Route to `NotifyContent` (`Layer?.Notify` in `Renderable`).
  5. Route to attached `_behaviors` (sorted by priority or in order, with **Hronomapper's continue-and-`Cancel()` rule** when a behavior claims the notification).

### 2. `Behavior` & `Behavior<TState>` Hierarchy (from *Hronomapper* + *Fu.Interaction* + *Calibrator*)
* **`Behavior : NodeBase, IBehavior`**
  * `[ProcessNode(FragmentSelection = FragmentSelection.Explicit)]`
  * `[Fragment] public void SetIsEnabled(bool isEnabled = true)` — calls `Cancel()` when disabled.
  * `public virtual bool IsTransient => false;` (from *Fu5*: `Hoverable` is `IsTransient = true`, so it never consumes notifications or cancels clicks/drags, and is not cancelled when a click/drag starts on the same node).
  * `public abstract void Cancel();` — called when disabled, detached from an `Interactable`, disposed, or preempted by a higher-priority behavior/node.
* **`Behavior<TState> : Behavior, IObservable<TState>`**
  * Holds `protected TState _state;` and exposes `[Fragment] public TState State => _state;`.
  * Implements `IObservable<TState>` (via `Subject<TState>` / `BehaviorSubject<TState>`), so every behavior is directly usable as an `IObservable<TState>` in VL patches!
  * Uses **`ChannelProperty<T>`** (from *Fu5*) or direct `IChannel<T>` sink helpers (`_channel?.EnsureValue(...)` / `SetChannel(IChannel<T>? channel)`) with *Calibrator*'s safety rule: if a bound channel reference changes mid-gesture, call `Cancel()` first.

### 3. `Gesture` State Machine & Threshold Disambiguation (from *Hronomapper* + *Fu.Interaction*)
* Keep `GestureState` enum with 6 states:
  `Idle`, `Possible`, `Started`, `Updated` (or `Active`), `Completed` (or `Ended`), `Canceled`.
* Why `Possible` is essential:
  * On `MouseDown` / `TouchDown` inside bounds, both `ClickBehavior` and `DragBehavior` enter `Possible` and **return `false`** (do not consume yet).
  * If `MouseUp` arrives before movement exceeds `Threshold`, `ClickBehavior` transitions `Possible → Completed` and consumes `MouseUp` (`return true`), while `DragBehavior` resets `Possible → Idle`.
  * If `MouseMove` exceeds `Threshold`, `DragBehavior` transitions `Possible → Started`, captures the pointer, consumes the event (`return true`), and triggers `Cancel()` on `ClickBehavior` (`Possible → Canceled`).

### 4. Pointer Capture & Matrix Freeze on Drag (from *Calibrator* + *Fu5*)
* **Root Pointer Capture:** The root `Interactable` (`Root` from `Traversable`) tracks the currently capturing `(IInteractable Node, Behavior Behavior)` when a gesture enters `Possible` / `Started`.
  * This guarantees that if the user drags the cursor outside the node's `(0, 0, Width, Height)` bounds (or outside a parent's `ClipArea`), `MouseMove` and `MouseUp` are still delivered to that behavior until release!
* **Frozen Transform Inverse (`_captureInverse`):** When a drag starts, capture the inverse of the **parent's** (or initial) `caller.Transformation` (just like *Calibrator*'s `DraggableNode`), so that if the drag mutates the node's own transform/layout, the drag delta never feedbacks or jitters.

### 5. Clean Detach / Cancel Hygiene (from *Calibrator*'s `CompositeBehaviourNode`)
* In `Interactable<TSelf>.SetBehaviors(IReadOnlyList<Behavior>? behaviors)`:
  * Filter out `null`s and deduplicate by `ReferenceEqualityComparer.Instance`.
  * Diff against previous `_behaviors`: call `removed.Cancel()` on any behavior that was unplugged so it never stays stuck in `IsHovered = true` or `IsDragging = true`.

---

## 4. Side-by-Side Summary Matrix

| Feature / Concern | Source of Best Pattern | How We Apply It in `VL.Fu` |
|---|---|---|
| **Entry point & Space transform** | `VL.Core.Skia` + *Calibrator* | `Interactable.Notify` pushes `LocalTransformation` and wraps notification with `LocalSpaceNotificationSender` (`PositionInWorldSpace`). |
| **`Behavior : IObservable<TState>`** | *Hronomapper* + *Fu.Interaction* | `Behavior<TState> : Behavior, IObservable<TState>` emits `TState` (a `Gesture` record or state struct) on state transitions. |
| **`IChannel<T>` Sinks** | *VL.Fu5* (`ChannelProperty<T>`) + *Calibrator* | `ChannelProperty<T>` (or `IChannel<T>` sink pins) with `Cancel()` when channel reference is swapped. |
| **Click vs. Drag on same node** | *Hronomapper* (`Possible` state + `Cancel()`) | `MouseDown` enters `Possible` (`returns false`); crossing `Threshold` enters `Started` (`returns true`) and `Cancel()`s lower-priority behaviors. |
| **Hover alongside Click/Drag** | *VL.Fu5* (`IsTransient`) + *Hronomapper* | `HoverBehavior` has `IsTransient = true`: updates `IsHovered` without consuming events or being cancelled by clicks. |
| **Dragging a moving element** | *Calibrator* (`_dragInverse`) | Freeze `caller.Transformation.TryInvert(out _captureInverse)` at press/drag start so moving the node doesn't feedback into local coordinates. |
| **Unplugging / Disabling a Behavior** | *Calibrator* (`CompositeBehaviourNode`) | `SetBehaviors` diffs old vs. new `Spread<Behavior>` by reference and calls `Cancel()` on removed behaviors. |
| **Unified Mouse + Touch** | *VL.Fu5* + *Hronomapper* | Pointer behaviors accept both primary `MouseNotification` and `TouchNotification` (tracking `PointerId`). |
