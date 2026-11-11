# Proposal: Interaction 01 — General Handling (Routing, Hit Path, Arbitration & Hover)

## 1. Scope

### In Scope
1. **`PointerEvent` & Notification Conversion:**
   * Converting `MouseDownNotification`, `MouseMoveNotification`, `MouseUpNotification`, `MouseClickNotification`, `MouseWheelNotification`, `MouseHorizontalWheelNotification`, `MouseLostNotification`, `LostFocusNotification`, and `TouchNotification` into a single allocation-free `PointerEvent` value.
   * Pointer identification (`MousePointerId = -1` for mouse; `TouchNotification.Id` for touch), DIP scaling via `CallerInfo.Scaling`, and touch-to-mouse promotion filtering.
2. **Tree Walk, Hit Path & `PointerEvents` Style in `Interactable<TSelf>`:**
   * Shared `OrderedChildren` accessor used forward by `Renderable<TSelf>.Render` and in reverse by the pointer hit-test walk (so task 05 z-index only changes one place).
   * Mapping coordinates into local node space `(0, 0, Layout.Width, Layout.Height)` by inverting `LocalTransformation` without `INotification.WithSender` allocations.
   * Gating by `HasLayout`, `IsDisplayed`, `PointerEvents` (`Auto` / `None`), `ClipArea`, and half-open `[0, Width) × [0, Height)` hit testing.
   * `PointerEvents` enum, `PointerEventsStyle` record, and `SetPointerEventsNode` process node following `style-instructions.md`.
   * Zero-allocation hit-path collection from root to front-most hit node.
3. **`PointerArbiter` (Capture, Kind Arbitration & Lifecycle Cancellation):**
   * Per-pointer capture to a node (`G5`).
   * Candidate collection along the hit path on `Down`, ordered front-most node first.
   * Arbitration by `GestureKind` (`Pinch > Drag > Click`, with `Wheel` separate):
     * A claim cancels lower kinds on the same node, and equal-or-lower kinds on other nodes.
     * Equal kinds on the same node all proceed (`Clickable` + `Selectable` both fire).
   * Freezing `windowToParent` (`SKMatrix` inverse of the **parent's** `CallerInfo.Transformation`, before the node's own `LocalTransformation`) and `windowToNode` at press time (`A1`).
   * Automatic cancellation when a node is hidden (`Display == None` or `PointerEvents == None`), detached (`OnParentChanged`), on `Keys.Escape`, or on `MouseLost` / `LostFocus`.
4. **Hover Path (`G3`) & `IHoverListener` Contract:**
   * CSS-like hover tracking along the current hit path (`IsHovered` is true while the node or any of its descendants is on the hit path).
   * Freezing hover to the captured node's ancestor path while any pointer is captured.
5. **Contracts for Proposal 02 (`IGesture`, `Behavior`, `IHoverListener`):**
   * `IGesture`, `GestureKind`, `GestureResult` (`Ignore`, `Possible`, `Claim`), and how `Behavior` exposes its gesture and receives transition notifications.
6. **Root Entry Point & Frame Order:**
   * How the root node owns and drives `PointerArbiter`, and what `Notify(INotification, CallerInfo)` returns to `VL.Skia`.

### Out of Scope (Covered Later)
* Concrete Tier 0 gestures (`ClickGesture`, `DragGesture`, `PinchGesture`, `WheelGesture`) and Tier 1 headless behaviors (`Clickable`, `Draggable`, `Pinchable`, `Hoverable`) — **Proposal 02**.
* Tier 2 scopes (`SelectionProvider`, `CanvasProvider`, `IGroupManipulable`) and Tier 3 manipulators (`Selectable`, `Movable`, `Transformable`) — **Proposal 03**.
* Keyboard focus model and `KeyPressable` (`G4`).
* Sibling z-index sorting implementation (task 05; only the shared `OrderedChildren` hook is defined here) and Overlay (task 06).

---

### Resolution of Review Correctness Bugs (`A1`–`A10`)

| Bug | Review Issue | Where & How Fixed in This Proposal |
|---|---|---|
| **A1** | Drag froze the node's own space (`innerCaller`), causing scaled/rotated nodes to move by the wrong amount or direction. | `HitPathEntry` and `PointerArbiter` record both `windowToParent` (inverse of `caller.Transformation` **before** `LocalTransformation`) and `windowToNode` (inverse **after** `LocalTransformation`). `PointerEvent` carries `PositionInParent` and `PositionInNode` in those frozen spaces (§3.3, §4.1, §4.4). |
| **A2** | Inconsistent gesture end states and synchronous pulse traps (`State`, `IObservable`, `Completed` lingering or vanishing). | `State`, `IObservable<TGesture>`, and `BehaviorSubject` are removed (`Q4`). `Behavior.OnGestureProcessed` writes transitions synchronously into patch-owned `IChannel<bool>`, `IChannel<Vector2>`, and `IChannel<Unit>` (via `SetValueAndAuthor`) (§4.2, §4.4). |
| **A3** | When an ancestor claimed an event, descendants above it kept their `Possible` state and fired on release. | `PointerArbiter` owns a flat `_candidates` list across the entire hit path. When any gesture returns `Claim`, `PointerArbiter.ApplyClaim` cancels all other candidates in `_candidates` according to the kind rules, including descendants in front of the claiming ancestor (§3.3, §4.4). |
| **A4** | Sibling cancellation only cancelled direct siblings, leaving grandchildren stuck in `Possible`, and re-walked subtrees on every move. | Only nodes on the single CSS-like hit path (`G1`) receive `Down`, and all armed gestures live in `PointerArbiter._candidates`. Cancellation iterates `_candidates` in $O(k)$ with zero subtree walks (§3.3, §4.4). |
| **A5** | `HasCapturedDescendant` was not real capture; higher siblings saw events first and `Hoverable` on other nodes still fired during drag. | `PointerArbiter._pointers` holds per-pointer capture slots on the root. Captured `Move`/`Up` events route directly to the captured node's gestures using the frozen matrices, bypassing the tree walk and freezing hover (`G3`, `G5`, §3.3, §4.4). |
| **A6** | Gestures got stuck when a node was hidden (`Display = None`) or detached from its parent mid-gesture. | `Interactable.OnParentChanged`, `EndStyle` (`PointerEvents = None` or `Display = None`), and `Dispose` call `CancelSubtreeInteractions()`, which cancels behaviors and releases any active capture/candidate/hover on the root arbiter; `PointerArbiter` also validates reachability before dispatch (§3.2, §4.3, §4.4). |
| **A7** | Keyboard notifications had no position and fired every node in the tree. | General keyboard routing is deferred until a focus model exists (`G4`). Only `KeyDownNotification(Keys.Escape)` is intercepted at the root to cancel active captures and candidates (§4.3, §4.4). |
| **A8** | `LocalSpaceNotificationSender` divided by `size - 1` in projection space and corrupted `PositionInProjectionSpace`. | `LocalSpaceNotificationSender` and `WithSender` are completely removed (`Q3`). All coordinate mapping is done directly via `SKMatrix.MapPoint` (§4.3). |
| **A9** | Ancestors' transient hover behaviors missed events when a child consumed the notification. | Hover is no longer a gesture or an `INotification` consumer (`G3`). `PointerArbiter` maintains `_hoveredPath` directly from the hit path (and freezes it during capture), notifying `IHoverListener`s on every ancestor along the path (§3.3, §4.4). |
| **A10** | Plain `VL.Skia.IBehavior`s only received events while `isInside`, stranding their release state outside bounds. | `SetBehaviors` only accepts `VL.Fu`'s `Behavior` (`Q3`), and `IInteractable` drops `IBehavior` (`B7`). Plain `VL.Skia.IBehavior`s are not hosted on `FuNode` (§4.2, §4.3). |

---

## 2. Types

| Type | Kind | Tier | File | Single Responsibility |
|---|---|---|---|---|
| `PointerEventKind` | `enum` | 0 | `VL.Fu\src\Core\Interaction\PointerEvent.cs` | Identifies the lifecycle phase of a pointer notification (`Down`, `Move`, `Up`, `Wheel`, `Cancel`). |
| `PointerEvent` | `readonly struct` | 0 | `VL.Fu\src\Core\Interaction\PointerEvent.cs` | Carries one normalized pointer notification with positions in window, parent, and node spaces, plus DIP scaling. |
| `GestureKind` | `enum` | 0 | `VL.Fu\src\Core\Interaction\Gestures\IGesture.cs` | Ranks gesture families (`Click = 10`, `Drag = 20`, `Pinch = 30`, `Wheel = 40`) for arbitration. |
| `GestureResult` | `enum` | 0 | `VL.Fu\src\Core\Interaction\Gestures\IGesture.cs` | Reports a gesture's arbitration response to an event (`Ignore`, `Possible`, `Claim`). |
| `IGesture` | `interface` | 0 | `VL.Fu\src\Core\Interaction\Gestures\IGesture.cs` | Defines the contract for a pure C# gesture state machine driven by `PointerArbiter`. |
| `IHoverListener` | `interface` | 1 | `VL.Fu\src\Core\Interaction\Behaviors\IHoverListener.cs` | Notifies a hover-aware behavior when its owner node enters or leaves the arbiter's hovered path. |
| `Behavior` | `abstract class` (VL `[ProcessNode]`) | 1 | `VL.Fu\src\Core\Interaction\Behaviors\Behavior.cs` | Base VL process node for all behaviors: tracks its single `IInteractable` owner, `Enabled` state, and channel helpers. |
| `PointerEvents` | `enum` | Style | `VL.Fu\src\Core\Styles\PointerEventsStyle.cs` | Specifies whether a node and its subtree participate in pointer hit testing (`Auto`, `None`). |
| `PointerEventsStyle` | `sealed record` | Style | `VL.Fu\src\Core\Styles\PointerEventsStyle.cs` | Immutable style instruction that stages `PointerEvents` on an `IFuNode`. |
| `SetPointerEventsNode` | `sealed class` (VL `[ProcessNode]`) | Style | `VL.Fu\src\Styles\PointerEventsNode.cs` | VL style node that outputs a cached `PointerEventsStyle` when its `Optional<PointerEvents>` input is set. |
| `IInteractable` | `interface` | Core | `VL.Fu\src\Core\Interaction\Interactable.cs` | Public contract for an interactable layout node exposing its `Behaviors`, `PointerEvents`, and `HitTest`. |
| `Interactable<TSelf>` | `abstract class` (VL `[ProcessNode]`) | Core | `VL.Fu\src\Core\Interaction\Interactable.cs` | Walks the tree in reverse draw order to build the hit path, manages the `Behaviors` list, and routes notifications at the root. |
| `PointerArbiter` | `sealed class` | Core | `VL.Fu\src\Core\Interaction\PointerArbiter.cs` | Arbitrates candidate gestures, owns per-pointer node capture and frozen reference matrices, and updates the hovered path. |

---

## 3. State and Lifecycle

### 3.0 Frame Order & Threading (Verified in `VL.Skia`)

Verified in `D:\VVVV\__MCP\VL.StandardLibs\VL.Skia\src\SkiaRenderer.cs` (lines 58–74, 211–218, 251–295) and `D:\VVVV\__MCP\VL.StandardLibs\VL.Core.Skia.Windows\TouchInput.cs` (lines 17–58):

1. **Main Thread Execution:** Both the VL frame `Update()` (`SetStyle`, `SetChildren`, `SetBehaviors`, behavior `SetX` setters, `FuNode.Update` / `CalculateRootLayout`) and WinForms message pumping (`OnPaint` → `Input.Render(callerInfo)` and `WndProc` / mouse / key / touch events → `OnNotification` → `Input.Notify(n, renderer.CallerInfo)`) run synchronously on the **main UI thread**.
2. **Relative Order Within a Tick:**
   * During the VL mainloop step, all node and behavior `SetX(...)` setters run first, followed by `FuNode.Update()` (`CalculateRootLayout()`) and `SkiaRenderer.Update()` (`Invalidate()`).
   * Between mainloop steps (inside the WinForms message pump), input messages invoke `SkiaRenderer.OnNotification`, which calls `root.Notify(notification, renderer.CallerInfo)` synchronously, and `WM_PAINT` invokes `root.Render(callerInfo)`.
   * Because `Notify` writes directly into upstream `IChannel<T>` instances (via `EnsureValue` / `SetValueAndAuthor`), reactive channel subscribers fire immediately and mainloop readers see the updated channel values on the very next frame `Update()`.

---

### 3.1 `Behavior` (Base Class)

#### Fields & Idle Values
| Field | Type | Idle Value | Purpose |
|---|---|---|---|
| `_enabled` | `bool` | `true` | Whether this behavior participates in interaction. |
| `_owner` | `IInteractable?` | `null` | The single node this behavior is currently attached to (`G7`). |

#### Lifecycle & Reset / Cancel Callers
* `SetEnabled(false)` calls `Cancel()`.
* `Interactable<TSelf>.SetBehaviors` calls `DetachOwner(this)` (which calls `Cancel()` and clears `_owner`) when a behavior is removed from the node's behavior list.
* `Interactable<TSelf>.Dispose` and subtree cancellation call `Cancel()`.
* `Behavior.Dispose(bool)` calls `Cancel()` and clears `_owner`.

#### Invariants
* If `_enabled == false`, the behavior has no active or `Possible` gesture and `IsHovered` (for `Hoverable`) is `false`.
* A `Behavior` instance has at most one `_owner` at a time. Calling `TryAttachOwner(otherNode)` while `_owner != null && !ReferenceEquals(_owner, otherNode)` logs a warning on the behavior and returns `false` (`G7`).

---

### 3.2 `Interactable<TSelf>`

#### Fields & Idle Values
| Field | Type | Idle Value | Purpose |
|---|---|---|---|
| `_behaviors` | `Spread<Behavior>` | `Spread<Behavior>.Empty` | Sanitized, deduplicated list of behaviors owned by this node. |
| `_pointerEvents` | `PointerEvents` | `PointerEvents.Auto` | Committed `PointerEvents` style value. |
| `_stagedPointerEvents` | `PointerEvents` | `PointerEvents.Auto` | Staged value during `SetStyle` pass. |
| `_committedPointerEvents` | `PointerEvents` | `PointerEvents.Auto` | Last style-committed `PointerEvents` value. |
| `_wasDisplayed` | `bool` | `true` | Tracks `IsDisplayed` across style passes to detect hiding (`A6`). |
| `_arbiter` | `PointerArbiter<TSelf>?` | `null` | Lazily created when this node acts as a root (`Parent is null`) and receives a notification. |
| `_lastScaling` | `float` | `1f` | Last `CallerInfo.Scaling` seen in `Render` or `Notify`. |

#### Who Calls Cancel
* `SetBehaviors`: calls `DetachOwner(this)` on any behavior removed from `_behaviors`, and calls `RootArbiter?.CancelBehavior(removed)`.
* `EndStyle`: if `!IsDisplayed` (when `_wasDisplayed` was `true`) or `_pointerEvents == PointerEvents.None` (when it was `Auto`), calls `CancelSubtreeInteractions()`.
* `OnParentChanged(oldParent, newParent)`:
  * When `oldParent is not null`, finds `oldParent.Root()._arbiter` and calls `oldArbiter.CancelSubtree(Self)` so any capture, candidate, or hover inside the detached/reparented subtree is immediately cancelled (`A6`, `D1`).
  * When `newParent is not null` and `_arbiter is not null` (this node was a root and just became a child), calls `_arbiter.CancelAll()` so no stale root state remains on the former root.
* `Dispose(bool)`: calls `_arbiter?.CancelAll()` and detaches all `_behaviors`.

#### Invariants
* Every `b` in `_behaviors` has `ReferenceEquals(b.Owner, this)`.
* `_behaviors` contains no `null` entries and no duplicate references.
* When `!IsDisplayed || _pointerEvents == PointerEvents.None || Parent is null (detached)`, no node in this subtree is captured, hovered, or holding a `Possible` candidate in a former root's `_arbiter`.

---

### 3.3 `PointerArbiter<TNode>`

`PointerArbiter<TNode>` is a plain C# class owned by the root node. It manages three pieces of state:
1. **`_pointers`** (active pointer slots, capacity 4, no heap allocations in steady state): tracks each depressed pointer (`-1` for mouse, `Id` for touch), its candidate gestures from `Down`, and—once claimed—its captured node and frozen matrices.
2. **`_hitPath`** (reusable `List<HitPathEntry<TNode>>`): scratch buffer populated during the walk from root to front-most node.
3. **`_hoveredPath` & `_nextHoveredPath`** (reusable `List<TNode>`): tracks the currently hovered ancestor chain for `MousePointerId` (or primary touch) so enter/leave notifications are $O(\text{depth})$ and zero-allocation.

#### Pointer Slot State Enum (`PointerSlotPhase`)

```csharp
internal enum PointerSlotPhase
{
    Idle,       // Unused slot
    Pending,    // Down received; 1..N candidates returned Possible, none has Claimed yet
    Captured,   // A gesture returned Claim; pointer is captured to CapturedNode
}
```

#### Transition Table for a Pointer Slot

| Current State | Input / Event | Condition | Next State | Effect |
|---|---|---|---|---|
| *(no slot)* | `Down` | Walk finds candidates returning `Possible` (no `Claim`) | `Pending` | Store candidates with each node's frozen `windowToParent` and `windowToNode`. |
| *(no slot)* | `Down` | Walk finds a gesture returning `Claim` | `Captured` | Cancel lower kinds on same node & equal-or-lower kinds on other nodes; if highest kind, capture to `node` with frozen matrices. |
| `Pending` | `Move` | Candidate returns `Claim` | `Captured` | Apply claim rules (cancel lower kinds on same node and equal-or-lower on other nodes); set `CapturedNode = candidate.Node` and freeze its matrices. |
| `Pending` | `Move` | All candidates return `Ignore` | `Idle` | Remove slot. |
| `Pending` | `Up` | Candidate returns `Claim` (e.g. `ClickGesture` on release) | `Idle` | Apply claim rules (cancels lower kinds / other nodes); immediately release slot since pointer is now up. |
| `Pending` | `Up` | No candidate claims | `Idle` | Cancel remaining `Possible` candidates for this pointer and remove slot. |
| `Captured` | `Move` | Captured node still reachable & displayed | `Captured` | Deliver `Move` (mapped with frozen `windowToParent` & `windowToNode`) to gestures on `CapturedNode`; apply claim rules if a higher kind (e.g. `Pinch`) claims. |
| `Captured` | `Up` | Last active pointer on `CapturedNode` released | `Idle` | Deliver `Up` to gestures on `CapturedNode`; remove slot; recompute hover path. |
| `Pending` / `Captured` | `Cancel` (`MouseLost`, `LostFocus`, `Esc`, node hidden/detached) | Always | `Idle` | Call `gesture.Cancel()` + `behavior.OnGestureCanceled(gesture)` on all active/possible gestures in slot; remove slot; clear/recompute hover. |

#### Invariants
* While a slot is `Captured`, `CapturedNode` is non-null, `CapturedNode.Root()` is the arbiter's root, `CapturedNode.IsDisplayed` is `true`, and `CapturedNode.PointerEvents == PointerEvents.Auto`.
* While any pointer slot is `Captured`, `_hoveredPath` is frozen to the ancestor chain of `CapturedNode` (`G3`).
* A gesture in `_candidates` has `Kind != GestureKind.Wheel` (`Wheel` is instantaneous and never enters `_candidates` or captures a pointer).
* If a second pointer goes `Down` on a node that is already `Captured` by a first pointer, both pointer slots reference the same `CapturedNode` and share its initial frozen `windowToParent` and `windowToNode` matrices, so `PinchGesture` on that node sees both pointers in one consistent coordinate space (`G5`).

---

## 4. Code

### 4.1 `PointerEvent.cs` — Event Struct & Notification Conversion

#### Touch Promotion Verification in `VL.Skia` (Cover Item 1)

We inspected `D:\VVVV\__MCP\VL.StandardLibs\VL.Skia\src\SkiaRenderer.Win32.cs` (lines 18–29) and `D:\VVVV\__MCP\VL.StandardLibs\VL.Core.Skia.Windows\TouchInput.cs` (lines 17–58):

```csharp
// D:\VVVV\__MCP\VL.StandardLibs\VL.Skia\src\SkiaRenderer.Win32.cs:18-22
protected override void WndProc(ref Message m)
{
    if (OperatingSystem.IsWindowsVersionAtLeast(8) && TouchMessageProcessor.TryHandle(ref m, this, touchNotifications))
        return;
```

```csharp
// D:\VVVV\__MCP\VL.StandardLibs\VL.Core.Skia.Windows\TouchInput.cs:46-57
var notification = new TouchNotification(kind, pos, client, (int)pi.pointerId, primary, contactArea, pi.sourceDevice, vlModifiers, target);
sink.OnNext(notification);
if (notification.Handled)
{
    m.Result = new IntPtr(1);
    return true; // handled and consumed
}
// ...
return false; // not handled (or not consumed)
```

**What this means for Touch Promotion:**
1. In Win32, `WM_POINTERDOWN` / `WM_POINTERUPDATE` / `WM_POINTERUP` promotes untouched pointer messages into synthesized `WM_LBUTTONDOWN` / `WM_MOUSEMOVE` / `WM_LBUTTONUP` inside `DefWindowProc` (`base.WndProc(ref m)`) **whenever `TouchMessageProcessor.TryHandle` returns `false`** (i.e., when `notification.Handled` is `false`!).
2. Even more subtly: on `TouchDown`, a `ClickGesture` only enters `Possible` (it does not claim until `TouchUp`). If `Notify` returned `false` on `TouchDown` because no gesture claimed yet, `notification.Handled` would be `false`, `TryHandle` would return `false`, `base.WndProc` would run `DefWindowProc`, and Windows would immediately synthesize a `WM_LBUTTONDOWN` (`MouseDownNotification`) for the same tap—causing **every tap to fire twice** (once as Touch `Id`, once as Mouse `-1`)!
3. Furthermore, `VL.Lib.IO.Mouse.InjectMouseClicks` (`D:\VVVV\__MCP\VL.StandardLibs\VL.Core\src\IO\Mouse.cs:54–108`) synthesizes a separate `MouseClickNotification` (with `ClickCount = 1, 2, ...`) *immediately after* `MouseUpNotification` (on single click) or *immediately after* `MouseDownNotification` (on double-click!).
4. **Two-part defense in `VL.Fu`:**
   * **Defense 1 (Suppress Win32 promotion at the source):** `Interactable.Notify` returns `true` for any `TouchNotification` that hits the Fu tree (or while any touch is active/pending), setting `notification.Handled = true` so `TouchMessageProcessor.TryHandle` returns `true` and skips `base.WndProc(ref m)`.
   * **Defense 2 (Guard against external/unhandled touch promotion in `PointerArbiter`):** While any touch pointer (`PointerId != MousePointerId`) is active in `PointerArbiter`, or within `TouchMousePromotionWindowTicks` after `TouchUp`, incoming `MouseNotification`s (pointer `-1`) are ignored.
   * **ClickCount handling (`MouseClickNotification`):** In `VL.Core`, `MouseDownNotification` and `MouseUpNotification` do *not* have a `ClickCount` field—only `MouseClickNotification` has `ClickCount` (`D:\VVVV\__MCP\VL.StandardLibs\VL.Core\src\IO\Notifications\MouseNotification.cs:117`). And `Mouse.InjectMouseClicks` emits `MouseClickNotification(ClickCount >= 2)` synchronously right after `MouseDownNotification`! Therefore, `PointerArbiter` tracks the latest `ClickCount` from `MouseClickNotification` (or computes multi-click timing on `Down`/`Up`) rather than treating `MouseClickNotification` as a second `Up`.

```csharp
using Stride.Core.Mathematics;
using VL.Lib.IO;
using VL.Lib.IO.Notifications;
using VL.Skia;

namespace VL.Fu.Core.Interaction
{
    /// <summary>
    /// Identifies the phase of a normalized <see cref="PointerEvent"/>.
    /// </summary>
    public enum PointerEventKind
    {
        Down,
        Move,
        Up,
        Wheel,
        Cancel,
    }

    /// <summary>
    /// A single normalized pointer notification (mouse or touch) with positions in window pixels,
    /// the target node's parent space, and the target node's own local space.
    /// </summary>
    /// <remarks>
    /// Built once at the root from a VL.Skia <see cref="INotification"/> and re-projected with
    /// <see cref="WithSpaces"/> as the walk or arbiter hands it to each node.
    /// Thresholds are compared in DIP by dividing pixel distances by <see cref="Scaling"/>.
    /// </remarks>
    public readonly struct PointerEvent
    {
        /// <summary>
        /// The pointer identifier used for the mouse. Touch pointers use <see cref="TouchNotification.Id"/>.
        /// </summary>
        public const int MousePointerId = -1;

        public PointerEventKind Kind { get; init; }

        public int PointerId { get; init; }

        public MouseButtons Buttons { get; init; }

        public Keys Modifiers { get; init; }

        public int ClickCount { get; init; }

        /// <summary>
        /// Pointer position in window pixel space (<see cref="INotificationWithPosition.Position"/>).
        /// </summary>
        public Vector2 PositionInWindow { get; init; }

        /// <summary>
        /// Pointer position in the target node's parent space (frozen at press time while captured).
        /// </summary>
        public Vector2 PositionInParent { get; init; }

        /// <summary>
        /// Pointer position in the target node's local space <c>(0, 0, Layout.Width, Layout.Height)</c>
        /// (frozen at press time while captured).
        /// </summary>
        public Vector2 PositionInNode { get; init; }

        /// <summary>
        /// Wheel step in notches (1.0 per standard 120-delta wheel detent; X for horizontal, Y for vertical).
        /// </summary>
        public Vector2 WheelDelta { get; init; }

        /// <summary>
        /// DPI scaling factor from <see cref="CallerInfo.Scaling"/> (always &gt; 0; defaults to 1).
        /// Divide window pixel distances by this value to obtain distances in DIP.
        /// </summary>
        public float Scaling { get; init; }

        public bool IsMouse => PointerId == MousePointerId;

        public bool IsTouch => PointerId != MousePointerId;

        /// <summary>
        /// Converts a window-pixel distance into device-independent pixels (DIP).
        /// </summary>
        public float PixelsToDip(float distanceInWindowPixels) =>
            distanceInWindowPixels / (Scaling > 0f ? Scaling : 1f);

        /// <summary>
        /// Returns a copy of this event with <see cref="PositionInParent"/> and <see cref="PositionInNode"/>
        /// set for the current target node.
        /// </summary>
        public PointerEvent WithSpaces(Vector2 positionInParent, Vector2 positionInNode) =>
            this with
            {
                PositionInParent = positionInParent,
                PositionInNode = positionInNode,
            };

        /// <summary>
        /// Converts a VL <see cref="INotification"/> into a <see cref="PointerEvent"/> at the root.
        /// Returns false for notifications that are not part of the pointer pipeline
        /// (e.g. <see cref="MouseClickNotification"/>, which is handled separately for double-click count,
        /// or non-Escape keyboard notifications).
        /// </summary>
        public static bool TryFromNotification(
            INotification notification,
            float scaling,
            int clickCount,
            out PointerEvent pointerEvent
        )
        {
            var safeScaling = float.IsFinite(scaling) && scaling > 0f ? scaling : 1f;

            switch (notification)
            {
                case MouseDownNotification down:
                    pointerEvent = new PointerEvent
                    {
                        Kind = PointerEventKind.Down,
                        PointerId = MousePointerId,
                        Buttons = down.Buttons,
                        Modifiers = down.ModifierKeys,
                        ClickCount = Math.Max(1, clickCount),
                        PositionInWindow = down.Position,
                        Scaling = safeScaling,
                    };
                    return IsFinite(down.Position);

                case MouseMoveNotification move:
                    pointerEvent = new PointerEvent
                    {
                        Kind = PointerEventKind.Move,
                        PointerId = MousePointerId,
                        Buttons = MouseButtons.None,
                        Modifiers = move.ModifierKeys,
                        ClickCount = 0,
                        PositionInWindow = move.Position,
                        Scaling = safeScaling,
                    };
                    return IsFinite(move.Position);

                case MouseUpNotification up:
                    pointerEvent = new PointerEvent
                    {
                        Kind = PointerEventKind.Up,
                        PointerId = MousePointerId,
                        Buttons = up.Buttons,
                        Modifiers = up.ModifierKeys,
                        ClickCount = Math.Max(1, clickCount),
                        PositionInWindow = up.Position,
                        Scaling = safeScaling,
                    };
                    return IsFinite(up.Position);

                case MouseWheelNotification wheel:
                    pointerEvent = new PointerEvent
                    {
                        Kind = PointerEventKind.Wheel,
                        PointerId = MousePointerId,
                        Buttons = MouseButtons.None,
                        Modifiers = wheel.ModifierKeys,
                        PositionInWindow = wheel.Position,
                        WheelDelta = new Vector2(0f, wheel.WheelDelta / 120f),
                        Scaling = safeScaling,
                    };
                    return IsFinite(wheel.Position);

                case MouseHorizontalWheelNotification hWheel:
                    pointerEvent = new PointerEvent
                    {
                        Kind = PointerEventKind.Wheel,
                        PointerId = MousePointerId,
                        Buttons = MouseButtons.None,
                        Modifiers = hWheel.ModifierKeys,
                        PositionInWindow = hWheel.Position,
                        WheelDelta = new Vector2(hWheel.WheelDelta / 120f, 0f),
                        Scaling = safeScaling,
                    };
                    return IsFinite(hWheel.Position);

                case TouchNotification touch:
                    var kind = touch.Kind switch
                    {
                        TouchNotificationKind.TouchDown => PointerEventKind.Down,
                        TouchNotificationKind.TouchUp => PointerEventKind.Up,
                        _ => PointerEventKind.Move,
                    };
                    pointerEvent = new PointerEvent
                    {
                        Kind = kind,
                        PointerId = touch.Id,
                        Buttons = MouseButtons.Left,
                        Modifiers = touch.ModifierKeys,
                        ClickCount = 1,
                        PositionInWindow = touch.Position,
                        Scaling = safeScaling,
                    };
                    return IsFinite(touch.Position);

                case MouseLostNotification:
                case LostFocusNotification:
                case KeyDownNotification { KeyCode: Keys.Escape }:
                    pointerEvent = new PointerEvent
                    {
                        Kind = PointerEventKind.Cancel,
                        PointerId = MousePointerId,
                        Scaling = safeScaling,
                    };
                    return true;

                default:
                    pointerEvent = default;
                    return false;
            }
        }

        private static bool IsFinite(Vector2 v) => float.IsFinite(v.X) && float.IsFinite(v.Y);
    }
}
```

---

### 4.2 Contracts Used by Proposal 02 (`IGesture`, `IHoverListener`, `Behavior`)

```csharp
using VL.Lib.IO;

namespace VL.Fu.Core.Interaction.Gestures
{
    /// <summary>
    /// Ranks gesture families for arbitration. Higher values win over lower values on the same node,
    /// and over equal-or-lower values on ancestor nodes (<c>Pinch &gt; Drag &gt; Click</c>).
    /// <see cref="Wheel"/> is arbitrated separately on wheel events only.
    /// </summary>
    public enum GestureKind
    {
        Click = 10,
        Drag = 20,
        Pinch = 30,
        Wheel = 40,
    }

    /// <summary>
    /// The response of an <see cref="IGesture"/> after inspecting a <see cref="PointerEvent"/>.
    /// </summary>
    public enum GestureResult
    {
        /// <summary>
        /// The gesture does not care about this event (or just reset to idle).
        /// </summary>
        Ignore,

        /// <summary>
        /// The gesture is armed and tracking the pointer, waiting for a movement threshold or release.
        /// Does not cancel other candidates yet.
        /// </summary>
        Possible,

        /// <summary>
        /// The gesture recognized its interaction and claims the pointer.
        /// Cancels lower kinds on the same node and equal-or-lower kinds on other nodes.
        /// </summary>
        Claim,
    }

    /// <summary>
    /// Contract for a pure C# gesture recognizer driven by <see cref="PointerArbiter{TNode}"/>.
    /// Holds no reference to VL nodes or channels, so it can be unit-tested standalone.
    /// </summary>
    public interface IGesture
    {
        /// <summary>
        /// The arbitration rank of this gesture (<c>Pinch &gt; Drag &gt; Click</c>, or <c>Wheel</c>).
        /// </summary>
        GestureKind Kind { get; }

        /// <summary>
        /// The mouse buttons this gesture responds to on <see cref="PointerEventKind.Down"/>.
        /// Touch pointers always present <see cref="MouseButtons.Left"/>.
        /// </summary>
        MouseButtons Buttons { get; }

        /// <summary>
        /// Advances the gesture state machine with <paramref name="ev"/> and returns its arbitration decision.
        /// </summary>
        GestureResult Process(in PointerEvent ev);

        /// <summary>
        /// Immediately resets the gesture back to its idle initial state.
        /// </summary>
        void Cancel();
    }
}
```

```csharp
namespace VL.Fu.Core.Interaction.Behaviors
{
    /// <summary>
    /// Implemented by behaviors (such as <c>Hoverable</c>) that track whether their owner node
    /// is on the arbiter's current hovered path (<c>G3</c>).
    /// </summary>
    public interface IHoverListener
    {
        /// <summary>
        /// Called by <see cref="PointerArbiter{TNode}"/> when the owner node enters or leaves the hovered path.
        /// </summary>
        void OnHoverChanged(bool isHovered);
    }
}
```

```csharp
using System.Reactive;
using VL.Core;
using VL.Core.Import;
using VL.Fu.Core.Base;
using VL.Fu.Core.Constants;
using VL.Fu.Core.Interaction.Gestures;
using VL.Lib.Reactive;
using VL.Model;

namespace VL.Fu.Core.Interaction.Behaviors
{
    /// <summary>
    /// Base process node for every Fu interaction behavior.
    /// Binds one or more <see cref="IGesture"/> state machines (or <see cref="IHoverListener"/>)
    /// to channels supplied by the patch.
    /// </summary>
    /// <remarks>
    /// A behavior instance belongs to at most one <see cref="IInteractable"/> node at a time.
    /// Attaching the same instance to a second node logs a warning and is ignored on that second node.
    /// Disabling or detaching the behavior immediately cancels its in-flight gestures.
    /// </remarks>
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public abstract class Behavior : NodeBase
    {
        private bool _enabled = true;
        private IInteractable? _owner;

        protected Behavior([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        /// <summary>
        /// Whether this behavior is active. Setting it to false cancels any in-flight gesture immediately.
        /// </summary>
        public bool Enabled => _enabled;

        /// <summary>
        /// Enables or disables this behavior. Disabling an active behavior cancels its gesture
        /// and resets its state channels.
        /// </summary>
        [Fragment(Order = PinOrder.Enabled)]
        public void SetEnabled(bool enabled = true)
        {
            if (_enabled == enabled)
                return;

            _enabled = enabled;
            if (!_enabled)
                Cancel();
        }

        /// <summary>
        /// The node this behavior is currently attached to, or null when unattached.
        /// </summary>
        internal IInteractable? Owner => _owner;

        /// <summary>
        /// Exposes this behavior's gestures to <see cref="PointerArbiter{TNode}"/> without allocating.
        /// Most behaviors have a single gesture; behaviors with none (like <c>Hoverable</c>) return 0.
        /// </summary>
        internal virtual int GestureCount => 0;

        /// <summary>
        /// Returns the gesture at <paramref name="index"/> (<c>0 .. GestureCount - 1</c>).
        /// </summary>
        internal virtual IGesture GetGesture(int index) =>
            throw new ArgumentOutOfRangeException(nameof(index));

        /// <summary>
        /// Called by <see cref="PointerArbiter{TNode}"/> immediately after <paramref name="gesture"/> processed
        /// <paramref name="ev"/> and returned <paramref name="result"/>.
        /// Concrete behaviors read the gesture's current properties here and write their upstream channels.
        /// </summary>
        internal virtual void OnGestureProcessed(
            IGesture gesture,
            in PointerEvent ev,
            GestureResult result
        ) { }

        /// <summary>
        /// Called by <see cref="PointerArbiter{TNode}"/> when <paramref name="gesture"/> is cancelled by arbitration,
        /// Escape, focus loss, or subtree hide/detach.
        /// Concrete behaviors restore press-start values and clear active flags here.
        /// </summary>
        internal virtual void OnGestureCanceled(IGesture gesture) { }

        /// <summary>
        /// Cancels all gestures owned by this behavior and resets any active state channels.
        /// </summary>
        public virtual void Cancel()
        {
            var count = GestureCount;
            for (var i = 0; i < count; i++)
            {
                var gesture = GetGesture(i);
                gesture.Cancel();
                OnGestureCanceled(gesture);
            }

            if (this is IHoverListener hoverListener)
                hoverListener.OnHoverChanged(false);
        }

        internal bool TryAttachOwner(IInteractable node)
        {
            if (ReferenceEquals(_owner, node))
                return true;

            if (_owner is not null)
            {
                this.LogWarning(
                    $"{GetType().Name} is already attached to '{_owner}'. A behavior can only be attached to one node at a time; it is ignored on '{node}'."
                );
                return false;
            }

            _owner = node;
            return true;
        }

        internal void DetachOwner(IInteractable node)
        {
            if (!ReferenceEquals(_owner, node))
                return;

            Cancel();
            _owner = null;
        }

        /// <summary>
        /// Updates a channel reference from a setter. If the channel changed while a gesture is active,
        /// cancels the behavior first so the old channel is not left stuck.
        /// </summary>
        protected static bool ReplaceChannel<T>(Behavior self, ref IChannel<T>? field, IChannel<T>? next)
        {
            var valid = next.IsValid() ? next : null;
            if (ReferenceEquals(field, valid))
                return false;

            self.Cancel();
            field = valid;
            return true;
        }

        /// <summary>
        /// Writes a value channel only when connected and the value differs (<see cref="ChannelExtensions.EnsureValue{T}"/>).
        /// </summary>
        protected static void WriteValue<T>(IChannel<T>? channel, T value)
        {
            if (channel is not null)
                channel.EnsureValue(value);
        }

        /// <summary>
        /// Fires a <see cref="Unit"/> event channel using <see cref="IChannel{T}.SetValueAndAuthor"/>,
        /// because <c>EnsureValue</c> compares equal <see cref="Unit.Default"/> values and would swallow events.
        /// </summary>
        protected void FireEvent(IChannel<Unit>? channel)
        {
            channel?.SetValueAndAuthor(Unit.Default, GetType().Name);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Cancel();
                _owner = null;
            }

            base.Dispose(disposing);
        }
    }
}
```

---

### 4.3 `PointerEventsStyle.cs`, `PointerEventsNode.cs` & `Interactable.cs`

#### `PointerEventsStyle.cs` (`VL.Fu\src\Core\Styles\PointerEventsStyle.cs`)

```csharp
using VL.Fu.Core.Interaction;
using VL.Fu.Core.Styling;

namespace VL.Fu.Core.Styles
{
    /// <summary>
    /// Controls whether a node and its subtree can be targeted by pointer events.
    /// </summary>
    public enum PointerEvents
    {
        /// <summary>
        /// The node and its children participate in hit testing as normal.
        /// </summary>
        Auto = 0,

        /// <summary>
        /// The node and its entire subtree are transparent to pointer hit testing,
        /// so pointer events pass through to siblings behind them.
        /// </summary>
        None = 1,
    }

    /// <summary>
    /// Sets whether the node and its subtree participate in pointer hit testing.
    /// <see cref="PointerEvents.Auto"/>, the default, hit-tests the node and its children.
    /// <see cref="PointerEvents.None"/> makes the node and its subtree transparent to the pointer.
    /// </summary>
    public sealed record PointerEventsStyle(
        IReadOnlyList<IStyle> Inputs,
        PointerEvents PointerEvents
    ) : Style<IFuNode>(Inputs)
    {
        internal static bool IsSupported(PointerEvents value) =>
            value is PointerEvents.Auto or PointerEvents.None;

        public override void Build(IFuNode node)
        {
            if (!IsSupported(PointerEvents))
                throw new ArgumentOutOfRangeException(
                    nameof(PointerEvents),
                    PointerEvents,
                    $"PointerEvents can't be {PointerEvents}."
                );

            ((Interactable<IFuNode>)node).StagePointerEvents(PointerEvents);
        }
    }
}
```

#### `PointerEventsNode.cs` (`VL.Fu\src\Styles\PointerEventsNode.cs`)

```csharp
using VL.Core;
using VL.Core.Import;
using VL.Fu.Core.Styles;
using VL.Fu.Core.Styling;
using VL.Model;

namespace VL.Fu.Styles
{
    /// <summary>
    /// Sets whether the node and its subtree receive pointer events.
    /// None makes the node and its subtree transparent to hit testing, so pointers pass through to nodes behind them.
    /// Without a value it is not set here, so an earlier setter or the default, Auto, holds.
    /// </summary>
    [ProcessNode(Name = "SetPointerEvents")]
    public sealed class SetPointerEventsNode : StyleNode
    {
        private Optional<PointerEvents> _pointerEvents;

        public SetPointerEventsNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        public void SetPointerEvents(Optional<PointerEvents> pointerEvents = default)
        {
            if (
                SetValue(
                    ref _pointerEvents,
                    ValidateEnum(pointerEvents, "PointerEvents", PointerEventsStyle.IsSupported)
                )
            )
            {
                RebuildStyle();
            }
        }

        protected override IStyle? BuildStyle(IReadOnlyList<IStyle> inputs) =>
            _pointerEvents.HasValue
                ? new PointerEventsStyle(inputs, _pointerEvents.Value)
                : null;
    }
}
```

#### `Interactable.cs` (`VL.Fu\src\Core\Interaction\Interactable.cs`)

```csharp
using SkiaSharp;
using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Fu.Core.Constants;
using VL.Fu.Core.Interaction.Behaviors;
using VL.Fu.Core.Layout;
using VL.Fu.Core.Styles;
using VL.Fu.Core.Tree;
using VL.Lib.Collections;
using VL.Lib.IO.Notifications;
using VL.Model;
using VL.Skia;

namespace VL.Fu.Core.Interaction
{
    /// <summary>
    /// A layout node that participates in pointer hit testing and hosts Fu <see cref="Behavior"/>s.
    /// </summary>
    /// <remarks>
    /// Does not inherit <see cref="IBehavior"/> directly; <c>Notify</c> is exposed on the root through
    /// <see cref="Rendering.IRenderable"/> (<see cref="ILayer"/>), since only a rendered root connects to a VL.Skia renderer.
    /// </remarks>
    public interface IInteractable : ILayoutable
    {
        /// <summary>
        /// The behaviors currently attached to this node, in spread order.
        /// </summary>
        IReadOnlyList<Behavior> Behaviors { get; }

        /// <summary>
        /// Whether this node and its subtree participate in pointer hit testing (<see cref="PointerEvents.Auto"/>)
        /// or are transparent to the pointer (<see cref="PointerEvents.None"/>).
        /// </summary>
        PointerEvents PointerEvents { get; }

        /// <summary>
        /// Tests whether <paramref name="pointInNode"/> (in this node's own local space, with (0, 0) at top-left)
        /// lies inside the node's layout box <c>[0, Width) × [0, Height)</c>.
        /// </summary>
        bool HitTest(Vector2 pointInNode);
    }

    internal readonly struct HitPathEntry<TNode>
        where TNode : class, ITraversable<TNode>, IInteractable
    {
        public readonly TNode Node;
        public readonly SKMatrix WindowToParent;
        public readonly SKMatrix WindowToNode;

        public HitPathEntry(TNode node, SKMatrix windowToParent, SKMatrix windowToNode)
        {
            Node = node;
            WindowToParent = windowToParent;
            WindowToNode = windowToNode;
        }
    }

    /// <summary>
    /// Adds pointer hit-testing, the <see cref="PointerEvents"/> style, the <see cref="Behaviors"/> list,
    /// and root-level notification arbitration to the node chain.
    /// </summary>
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public abstract class Interactable<TSelf> : Layoutable<TSelf>, IInteractable
        where TSelf : class, ITraversable<TSelf>, IInteractable
    {
        private Spread<Behavior> _behaviors = Spread<Behavior>.Empty;
        private PointerEvents _pointerEvents = PointerEvents.Auto;
        private PointerEvents _stagedPointerEvents = PointerEvents.Auto;
        private PointerEvents _committedPointerEvents = PointerEvents.Auto;
        private bool _wasDisplayed = true;
        private PointerArbiter<TSelf>? _arbiter;

        protected Interactable([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        private TSelf Self => (TSelf)(object)this;

        public IReadOnlyList<Behavior> Behaviors => _behaviors;

        public PointerEvents PointerEvents => _pointerEvents;

        /// <summary>
        /// The children of this node in draw order (back to front).
        /// Shared by <c>Renderable.Render</c> (forward) and <see cref="TryBuildHitPath"/> (reverse),
        /// so sibling z-index (task 05) only changes this one property.
        /// </summary>
        protected virtual IReadOnlyList<TSelf> OrderedChildren => Children;

        /// <summary>
        /// Whether this node is currently eligible to be visited by the pointer walk.
        /// </summary>
        internal bool IsHitTestable =>
            HasLayout && IsDisplayed && _pointerEvents == PointerEvents.Auto;

        /// <summary>
        /// Replaces the behaviors attached to this node.
        /// Null entries, duplicate references, and behaviors already owned by another node are ignored with a warning.
        /// Removed behaviors are immediately cancelled and detached.
        /// </summary>
        [Fragment(Order = PinOrder.Behavior)]
        public void SetBehaviors(
            [Pin(Visibility = PinVisibility.Optional)] IReadOnlyList<Behavior>? behaviors
        )
        {
            var incoming = behaviors ?? Spread<Behavior>.Empty;
            if (SameBehaviors(_behaviors, incoming))
                return;

            var next = SanitizeBehaviors(ToBehaviorSpread(behaviors));
            if (SameBehaviors(_behaviors, next))
                return;

            var previous = _behaviors;
            var rootArbiter = RootArbiter;

            for (var i = 0; i < previous.Count; i++)
            {
                var old = previous[i];
                if (!ContainsReference(next, old))
                {
                    rootArbiter?.CancelBehavior(old);
                    old.DetachOwner(this);
                }
            }

            _behaviors = next;
        }

        /// <summary>
        /// Tests whether <paramref name="pointInNode"/> lies within the half-open layout box
        /// <c>[0, Layout.Width) × [0, Layout.Height)</c>.
        /// </summary>
        /// <remarks>
        /// Half-open bounds ensure two adjacent siblings sharing an edge never both match the same boundary pixel.
        /// </remarks>
        public virtual bool HitTest(Vector2 pointInNode)
        {
            if (!IsHitTestable)
                return false;

            var layout = Layout;
            return pointInNode.X >= 0f
                && pointInNode.Y >= 0f
                && pointInNode.X < layout.Width
                && pointInNode.Y < layout.Height;
        }

        /// <summary>
        /// Routes a VL.Skia notification at the root of a Fu tree through its <see cref="PointerArbiter{TSelf}"/>.
        /// Returns false when called on a non-root node, or when the notification does not hit any interactive target.
        /// </summary>
        public bool Notify(INotification notification, CallerInfo caller)
        {
            if (Parent is not null || !IsHitTestable)
                return false;

            _arbiter ??= new PointerArbiter<TSelf>(Self);
            return _arbiter.Notify(notification, caller);
        }

        /// <summary>
        /// Walks the subtree in reverse draw order (front-most child first) without allocating,
        /// and appends the winning root-to-target chain into <paramref name="path"/>.
        /// </summary>
        internal bool TryBuildHitPath(
            Vector2 pointInWindow,
            in SKMatrix parentToWindow,
            List<HitPathEntry<TSelf>> path
        )
        {
            if (!IsHitTestable)
                return false;

            if (!parentToWindow.TryInvert(out var windowToParent))
                return false;

            var local = LocalTransformation;
            var nodeToWindow = parentToWindow.PreConcat(local);
            if (!nodeToWindow.TryInvert(out var windowToNode))
                return false;

            var skPointInNode = windowToNode.MapPoint(pointInWindow.X, pointInWindow.Y);
            var pointInNode = new Vector2(skPointInNode.X, skPointInNode.Y);

            var entryIndex = path.Count;
            path.Add(new HitPathEntry<TSelf>(Self, windowToParent, windowToNode));

            // 1. Children in reverse draw order (front-most first), gated by ClipArea.
            var ordered = OrderedChildren;
            if (ordered.Count > 0 && IsInsideClip(pointInNode))
            {
                for (var i = ordered.Count - 1; i >= 0; i--)
                {
                    var childNode = AsInteractable(ordered[i]);
                    if (childNode.TryBuildHitPath(pointInWindow, in nodeToWindow, path))
                        return true;
                }
            }

            // 2. Self box hit test.
            if (HitTest(pointInNode))
                return true;

            // Missed both children and self: pop our speculative entry.
            path.RemoveAt(entryIndex);
            return false;
        }

        private bool IsInsideClip(Vector2 pointInNode)
        {
            if (ClipArea is not { } clip)
                return true;

            return clip.Width > 0f
                && clip.Height > 0f
                && pointInNode.X >= clip.Left
                && pointInNode.Y >= clip.Top
                && pointInNode.X < clip.Right
                && pointInNode.Y < clip.Bottom;
        }

        internal void StagePointerEvents(PointerEvents pointerEvents) =>
            _stagedPointerEvents = pointerEvents;

        protected override void BeginStyle()
        {
            base.BeginStyle();
            _wasDisplayed = IsDisplayed;
            _stagedPointerEvents = PointerEvents.Auto;
        }

        protected override void EndStyle()
        {
            base.EndStyle();

            if (_stagedPointerEvents != _committedPointerEvents)
            {
                _committedPointerEvents = _stagedPointerEvents;
                _pointerEvents = _committedPointerEvents;
            }

            var isDisplayedNow = IsDisplayed;
            if ((_wasDisplayed && !isDisplayedNow) || _pointerEvents == PointerEvents.None)
                CancelSubtreeInteractions();

            _wasDisplayed = isDisplayedNow;
        }

        protected override void OnParentChanged(TSelf? oldParent, TSelf? newParent)
        {
            base.OnParentChanged(oldParent, newParent);

            if (oldParent is not null)
            {
                var oldRoot = AsInteractable(oldParent.Root());
                oldRoot._arbiter?.CancelSubtree(Self);
            }

            if (newParent is not null && _arbiter is not null)
            {
                _arbiter.CancelAll();
                _arbiter = null;
            }
        }

        internal void CancelSubtreeInteractions()
        {
            RootArbiter?.CancelSubtree(Self);
            CancelSubtreeBehaviorsRecursive(Self);
        }

        private static void CancelSubtreeBehaviorsRecursive(TSelf node)
        {
            var interactable = AsInteractable(node);
            var behaviors = interactable._behaviors;
            for (var i = 0; i < behaviors.Count; i++)
                behaviors[i].Cancel();

            var children = node.Children;
            for (var i = 0; i < children.Count; i++)
                CancelSubtreeBehaviorsRecursive(children[i]);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _arbiter?.CancelAll();
                _arbiter = null;

                for (var i = 0; i < _behaviors.Count; i++)
                    _behaviors[i].DetachOwner(this);

                _behaviors = Spread<Behavior>.Empty;
            }

            base.Dispose(disposing);
        }

        private PointerArbiter<TSelf>? RootArbiter => AsInteractable(Self.Root())._arbiter;

        internal static Interactable<TSelf> AsInteractable(TSelf node) =>
            (Interactable<TSelf>)(object)node;

        // SanitizeBehaviors, SameBehaviors, ToBehaviorSpread, ContainsReference follow Traversable.cs ...
    }
}
```

And in `Renderable<TSelf>.Render` (`VL.Fu\src\Core\Rendering\Renderable.cs`), the child loop uses `OrderedChildren` instead of `Children`:
```csharp
var children = OrderedChildren;
if (children.Count == 0)
    return;
// ...
for (var i = 0; i < children.Count; i++)
    children[i].Render(inner);
```

---

### 4.4 `PointerArbiter.cs` — Capture, Arbitration by `GestureKind`, and Hover Path

```csharp
using System.Diagnostics;
using SkiaSharp;
using Stride.Core.Mathematics;
using VL.Fu.Core.Interaction.Behaviors;
using VL.Fu.Core.Interaction.Gestures;
using VL.Fu.Core.Tree;
using VL.Lib.IO;
using VL.Lib.IO.Notifications;
using VL.Skia;

namespace VL.Fu.Core.Interaction
{
    /// <summary>
    /// Owns pointer capture, candidate gesture arbitration, and hover-path tracking for one root Fu tree.
    /// </summary>
    internal sealed class PointerArbiter<TNode>
        where TNode : class, ITraversable<TNode>, IInteractable
    {
        // Suppresses Win32 touch-to-mouse promoted mouse events for 100 ms after a touch release
        // if an external host failed to honour TouchNotification.Handled.
        private static readonly long TouchPromotionGuardTicks = Stopwatch.Frequency / 10;

        private readonly TNode _root;
        private readonly List<HitPathEntry<TNode>> _hitPath = new(16);
        private readonly List<PointerSlot> _pointers = new(4);
        private readonly List<CandidateEntry> _candidates = new(16);
        private readonly List<TNode> _hoveredPath = new(16);
        private readonly List<TNode> _nextHoveredPath = new(16);

        private int _pendingMouseClickCount = 1;
        private long _lastTouchTimestamp;

        public PointerArbiter(TNode root)
        {
            _root = root;
        }

        private struct PointerSlot
        {
            public int PointerId;
            public PointerSlotPhase Phase;
            public TNode? CapturedNode;
            public SKMatrix FrozenWindowToParent;
            public SKMatrix FrozenWindowToNode;
        }

        private struct CandidateEntry
        {
            public int PointerId;
            public TNode Node;
            public int PathDepth; // Higher = closer to front-most leaf
            public Behavior Behavior;
            public IGesture Gesture;
            public SKMatrix FrozenWindowToParent;
            public SKMatrix FrozenWindowToNode;
        }

        public bool Notify(INotification notification, CallerInfo caller)
        {
            // VL's Mouse.InjectMouseClicks emits MouseClickNotification with ClickCount >= 2
            // synchronously right after MouseDownNotification. Record its ClickCount for the next Up.
            if (notification is MouseClickNotification clickNotif)
            {
                _pendingMouseClickCount = Math.Max(1, clickNotif.ClickCount);
                return HasActivePointer(PointerEvent.MousePointerId);
            }

            if (
                !PointerEvent.TryFromNotification(
                    notification,
                    caller.Scaling,
                    _pendingMouseClickCount,
                    out var rawEvent
                )
            )
            {
                return false;
            }

            if (rawEvent.Kind == PointerEventKind.Cancel)
            {
                var hadActive = _pointers.Count > 0 || _candidates.Count > 0;
                CancelAll();
                return hadActive;
            }

            // Touch promotion guard: ignore synthesized mouse events while touch is active or just ended.
            if (rawEvent.IsTouch)
            {
                _lastTouchTimestamp = Stopwatch.GetTimestamp();
            }
            else if (IsMousePromotedFromTouch())
            {
                return false;
            }

            // Prune any captured/candidate nodes that became hidden or detached since the last event.
            ValidateActiveTargets();

            return rawEvent.Kind switch
            {
                PointerEventKind.Down => HandleDown(in rawEvent, in caller.Transformation),
                PointerEventKind.Move => HandleMove(in rawEvent, in caller.Transformation),
                PointerEventKind.Up => HandleUp(in rawEvent, in caller.Transformation),
                PointerEventKind.Wheel => HandleWheel(in rawEvent, in caller.Transformation),
                _ => false,
            };
        }

        private bool HandleDown(in PointerEvent rawEvent, in SKMatrix rootParentToWindow)
        {
            if (rawEvent.IsMouse)
                _pendingMouseClickCount = 1;

            // Self-heal if a previous press with the same PointerId never received its Up.
            if (FindSlotIndex(rawEvent.PointerId) >= 0)
                CancelPointer(rawEvent.PointerId);

            _hitPath.Clear();
            var rootInteractable = Interactable<TNode>.AsInteractable(_root);
            var hitAny = rootInteractable.TryBuildHitPath(
                rawEvent.PositionInWindow,
                in rootParentToWindow,
                _hitPath
            );

            UpdateHoverFromHitPath();

            if (!hitAny)
                return false;

            // If another pointer already captured a node on this path (e.g. 2nd finger for Pinch),
            // attach this new pointer to that same captured node and reuse its frozen spaces.
            if (TryJoinExistingCapture(in rawEvent, out var joinedClaimed))
                return true;

            // Walk front-most to root (end of _hitPath down to 0) collecting candidate gestures.
            var claimed = false;
            var claimingNode = default(TNode);
            var claimingKind = default(GestureKind);
            var claimingWindowToParent = SKMatrix.Identity;
            var claimingWindowToNode = SKMatrix.Identity;

            for (var pathIdx = _hitPath.Count - 1; pathIdx >= 0; pathIdx--)
            {
                var entry = _hitPath[pathIdx];
                var ev = MapEvent(in rawEvent, in entry.WindowToParent, in entry.WindowToNode);
                var behaviors = entry.Node.Behaviors;

                for (var bIdx = 0; bIdx < behaviors.Count; bIdx++)
                {
                    var behavior = behaviors[bIdx];
                    if (!behavior.Enabled)
                        continue;

                    var gCount = behavior.GestureCount;
                    for (var gIdx = 0; gIdx < gCount; gIdx++)
                    {
                        var gesture = behavior.GetGesture(gIdx);
                        if (gesture.Kind == GestureKind.Wheel)
                            continue;

                        if ((gesture.Buttons & rawEvent.Buttons) == 0)
                            continue;

                        // If a front-most node already claimed an equal or higher kind, skip lower/equal behind it.
                        if (claimed && gesture.Kind <= claimingKind && !ReferenceEquals(entry.Node, claimingNode))
                            continue;

                        if (claimed && gesture.Kind < claimingKind && ReferenceEquals(entry.Node, claimingNode))
                            continue;

                        var result = gesture.Process(in ev);
                        behavior.OnGestureProcessed(gesture, in ev, result);

                        if (result == GestureResult.Possible)
                        {
                            _candidates.Add(
                                new CandidateEntry
                                {
                                    PointerId = rawEvent.PointerId,
                                    Node = entry.Node,
                                    PathDepth = pathIdx,
                                    Behavior = behavior,
                                    Gesture = gesture,
                                    FrozenWindowToParent = entry.WindowToParent,
                                    FrozenWindowToNode = entry.WindowToNode,
                                }
                            );
                        }
                        else if (result == GestureResult.Claim)
                        {
                            claimingNode = entry.Node;
                            claimingKind = gesture.Kind;
                            claimingWindowToParent = entry.WindowToParent;
                            claimingWindowToNode = entry.WindowToNode;
                            claimed = true;

                            _candidates.Add(
                                new CandidateEntry
                                {
                                    PointerId = rawEvent.PointerId,
                                    Node = entry.Node,
                                    PathDepth = pathIdx,
                                    Behavior = behavior,
                                    Gesture = gesture,
                                    FrozenWindowToParent = entry.WindowToParent,
                                    FrozenWindowToNode = entry.WindowToNode,
                                }
                            );
                            ApplyClaim(entry.Node, gesture.Kind);
                        }
                    }
                }
            }

            if (claimed && claimingNode is not null)
            {
                _pointers.Add(
                    new PointerSlot
                    {
                        PointerId = rawEvent.PointerId,
                        Phase = PointerSlotPhase.Captured,
                        CapturedNode = claimingNode,
                        FrozenWindowToParent = claimingWindowToParent,
                        FrozenWindowToNode = claimingWindowToNode,
                    }
                );
                FreezeHoverToNode(claimingNode);
                return true;
            }

            if (HasCandidatesForPointer(rawEvent.PointerId))
            {
                _pointers.Add(
                    new PointerSlot
                    {
                        PointerId = rawEvent.PointerId,
                        Phase = PointerSlotPhase.Pending,
                    }
                );
                return true;
            }

            // Return true for touch hits so SkiaRenderer sets touchNotification.Handled = true
            // and suppresses Win32 mouse promotion.
            return rawEvent.IsTouch;
        }

        private bool HandleMove(in PointerEvent rawEvent, in SKMatrix rootParentToWindow)
        {
            var slotIdx = FindSlotIndex(rawEvent.PointerId);

            if (slotIdx >= 0 && _pointers[slotIdx].Phase == PointerSlotPhase.Captured)
            {
                var slot = _pointers[slotIdx];
                DispatchToCapturedNode(in slot, in rawEvent);
                return true;
            }

            if (slotIdx >= 0 && _pointers[slotIdx].Phase == PointerSlotPhase.Pending)
            {
                DispatchToPendingCandidates(slotIdx, in rawEvent);
                if (FindSlotIndex(rawEvent.PointerId) >= 0 && _pointers[FindSlotIndex(rawEvent.PointerId)].Phase == PointerSlotPhase.Captured)
                    return true;
            }

            // When no pointer is captured, rebuild hit path so Hoverable updates on mouse move.
            if (!HasAnyCapture())
            {
                _hitPath.Clear();
                Interactable<TNode>.AsInteractable(_root).TryBuildHitPath(
                    rawEvent.PositionInWindow,
                    in rootParentToWindow,
                    _hitPath
                );
                UpdateHoverFromHitPath();
            }

            return slotIdx >= 0 || (rawEvent.IsTouch && _hitPath.Count > 0);
        }

        private bool HandleUp(in PointerEvent rawEvent, in SKMatrix rootParentToWindow)
        {
            var slotIdx = FindSlotIndex(rawEvent.PointerId);
            var handled = false;

            if (slotIdx >= 0)
            {
                var slot = _pointers[slotIdx];
                if (slot.Phase == PointerSlotPhase.Captured)
                {
                    DispatchToCapturedNode(in slot, in rawEvent);
                    handled = true;
                }
                else if (slot.Phase == PointerSlotPhase.Pending)
                {
                    handled = DispatchUpToPendingCandidates(in rawEvent);
                }

                RemovePointerAndCandidates(rawEvent.PointerId);
            }

            // Recompute hover path once capture is released.
            if (!HasAnyCapture())
            {
                _hitPath.Clear();
                if (!rawEvent.IsTouch)
                {
                    Interactable<TNode>.AsInteractable(_root).TryBuildHitPath(
                        rawEvent.PositionInWindow,
                        in rootParentToWindow,
                        _hitPath
                    );
                }
                UpdateHoverFromHitPath();
            }

            return handled || rawEvent.IsTouch;
        }

        private bool HandleWheel(in PointerEvent rawEvent, in SKMatrix rootParentToWindow)
        {
            _hitPath.Clear();
            if (
                !Interactable<TNode>.AsInteractable(_root).TryBuildHitPath(
                    rawEvent.PositionInWindow,
                    in rootParentToWindow,
                    _hitPath
                )
            )
            {
                return false;
            }

            // Wheel goes front-most to root; the first node whose Wheel gesture claims wins.
            for (var pathIdx = _hitPath.Count - 1; pathIdx >= 0; pathIdx--)
            {
                var entry = _hitPath[pathIdx];
                var ev = MapEvent(in rawEvent, in entry.WindowToParent, in entry.WindowToNode);
                var behaviors = entry.Node.Behaviors;
                var claimedOnNode = false;

                for (var bIdx = 0; bIdx < behaviors.Count; bIdx++)
                {
                    var behavior = behaviors[bIdx];
                    if (!behavior.Enabled)
                        continue;

                    var gCount = behavior.GestureCount;
                    for (var gIdx = 0; gIdx < gCount; gIdx++)
                    {
                        var gesture = behavior.GetGesture(gIdx);
                        if (gesture.Kind != GestureKind.Wheel)
                            continue;

                        var result = gesture.Process(in ev);
                        behavior.OnGestureProcessed(gesture, in ev, result);
                        if (result == GestureResult.Claim)
                            claimedOnNode = true;
                    }
                }

                if (claimedOnNode)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Enforces the binding claim rules:
        /// - cancels lower kinds on <paramref name="claimingNode"/>;
        /// - cancels equal-or-lower kinds on all other nodes;
        /// - equal kinds on <paramref name="claimingNode"/> are kept so e.g. Clickable + Selectable both proceed.
        /// </summary>
        private void ApplyClaim(TNode claimingNode, GestureKind claimingKind)
        {
            for (var i = _candidates.Count - 1; i >= 0; i--)
            {
                var c = _candidates[i];
                var sameNode = ReferenceEquals(c.Node, claimingNode);
                var shouldCancel = sameNode
                    ? c.Gesture.Kind < claimingKind
                    : c.Gesture.Kind <= claimingKind;

                if (shouldCancel)
                {
                    c.Gesture.Cancel();
                    c.Behavior.OnGestureCanceled(c.Gesture);
                    _candidates.RemoveAt(i);
                }
            }
        }

        private static PointerEvent MapEvent(
            in PointerEvent rawEvent,
            in SKMatrix windowToParent,
            in SKMatrix windowToNode
        )
        {
            var pParent = windowToParent.MapPoint(
                rawEvent.PositionInWindow.X,
                rawEvent.PositionInWindow.Y
            );
            var pNode = windowToNode.MapPoint(
                rawEvent.PositionInWindow.X,
                rawEvent.PositionInWindow.Y
            );
            return rawEvent.WithSpaces(
                new Vector2(pParent.X, pParent.Y),
                new Vector2(pNode.X, pNode.Y)
            );
        }

        // DispatchToCapturedNode, DispatchToPendingCandidates, UpdateHoverFromHitPath,
        // CancelSubtree, CancelBehavior, CancelAll ...
    }
}
```

---

## 5. Traces

### Trace 1: Parent `Draggable` (threshold 4 DIP) over Child `Clickable` (threshold 6 DIP) — Drag Claim Cancels Child Click (`A3` Fix)

Setup:
* `Root` (100×100) has `Draggable` (`DragGesture`, threshold 4 DIP, writes `Offset`, `IsDragging`, `OnDragStart`, `OnDragEnd`).
* `Child` (50×50 at `(10, 10)`) has `Clickable` (`ClickGesture`, threshold 6 DIP, writes `IsPressed`, `OnClick`) and `Hoverable` (writes `IsHovered`).
* `CallerInfo.Scaling = 1.0f`.

| Step | Input | Who Sees It | State Change | Channel Writes |
|---|---|---|---|---|
| **1** | `MouseMove(20, 20)` | `Root.TryBuildHitPath` → `[Root, Child]`; `PointerArbiter.UpdateHoverFromHitPath` | `_hoveredPath` becomes `[Root, Child]`. | `Child.Hoverable`: `IsHovered = true` |
| **2** | `MouseDown(Left, 20, 20)` | Front-to-back on `[Child, Root]`: `Child.ClickGesture.Process(Down)` → `Possible`; `Root.DragGesture.Process(Down)` → `Possible`. | `_pointers` adds `(Id: -1, Phase: Pending)`. `_candidates` holds `[Child.ClickGesture, Root.DragGesture]` with each node's frozen `windowToParent` and `windowToNode`. | `Child.Clickable`: `IsPressed = true` |
| **3** | `MouseMove(25, 20)` *(5 DIP move)* | `_candidates` in front-to-back order: 1. `Child.ClickGesture.Process(Move)` (5 DIP ≤ 6 DIP) → `Possible`. 2. `Root.DragGesture.Process(Move)` (5 DIP ≥ 4 DIP) → **`Claim`**! | `ApplyClaim(Root, GestureKind.Drag)` sees `Child.ClickGesture` (`Kind.Click <= Kind.Drag` on another node) and calls **`Child.ClickGesture.Cancel()`** + removes it from `_candidates`. Slot `-1` becomes `Captured` on `Root`. `_hoveredPath` freezes to `[Root]` (`Child` leaves hover!). | `Child.Clickable`: `IsPressed = false` *(cancelled!)*; `Child.Hoverable`: `IsHovered = false`; `Root.Draggable`: `IsDragging = true`, `OnDragStart = Unit`, `Offset = (5, 0)` |
| **4** | `MouseMove(40, 20)` *(20 DIP total)* | Slot `-1` is `Captured` on `Root` → delivered directly to `Root.DragGesture` using `Root`'s frozen `windowToParent`. | `Root.DragGesture` stays active (`Claim`). | `Root.Draggable`: `Offset = (20, 0)` |
| **5** | `MouseUp(Left, 40, 20)` | Delivered directly to `Root.DragGesture.Process(Up)`. `Child` is not in `_candidates`, so `Child.Clickable` cannot fire! | Slot `-1` and `Root.DragGesture` removed; hover recomputed at `(40, 20)` → `[Root, Child]`. | `Root.Draggable`: `IsDragging = false`, `OnDragEnd = Unit`; `Child.Hoverable`: `IsHovered = true` |

---

### Trace 2: Node Hidden Mid-Drag (`SetDisplay(YGDisplay.None)`) — Cancel Path (`A6` & `G8` Fix)

Setup:
* `Root` → `Item` (50×50) with `Draggable` (`Offset` initial `(0, 0)`) and `Hoverable`.
* User presses at `(10, 10)` and drags to `(30, 10)`, then the patch sets `Display = None` on `Item` during the next frame `Update()` while the mouse button is still held down.

| Step | Input | Who Sees It | State Change | Channel Writes |
|---|---|---|---|---|
| **1** | `MouseDown(Left, 10, 10)` | `Item.DragGesture.Process(Down)` → `Possible` | Slot `-1` is `Pending`; `_candidates` holds `[Item.DragGesture]`; `_hoveredPath` is `[Root, Item]`. | `Item.Hoverable`: `IsHovered = true` |
| **2** | `MouseMove(30, 10)` *(20 DIP)* | `Item.DragGesture.Process(Move)` → `Claim` | Slot `-1` becomes `Captured` on `Item`; frozen matrices stored on slot. | `Item.Draggable`: `IsDragging = true`, `OnDragStart = Unit`, `Offset = (20, 0)` |
| **3** | Frame `Update`: `Item.SetStyle(Display = None)` | `Item.EndStyle()` detects `_wasDisplayed && !IsDisplayed` and calls `Item.CancelSubtreeInteractions()`. | `Root._arbiter.CancelSubtree(Item)` cancels `Item.DragGesture`, calls `Draggable.OnGestureCanceled`, removes slot `-1`, and removes `Item` from `_hoveredPath`. | `Item.Draggable`: `IsDragging = false`, `Offset = (0, 0)` *(restored to value at press, `G8`)*; `Item.Hoverable`: `IsHovered = false` |
| **4** | `MouseMove(50, 10)` | `Root._arbiter.HandleMove`: slot `-1` is gone; `TryBuildHitPath` skips `Item` (`!IsDisplayed`) and hits `[Root]`. | `_hoveredPath` stays `[Root]`. | *(none)* |
| **5** | `MouseUp(Left, 50, 10)` | `Root._arbiter.HandleUp`: no slot for `-1`, returns `false`. | Idle. | *(none)* |

---

## 6. Tests

All tests live under `VL.Fu.Tests\Core\Interaction\` and `VL.Fu.Tests\Styles\StyleNodeTests.cs` and run directly in MSTest without the VL runtime.

### `PointerEventTests.cs`
* `TryFromNotification_MouseDown_MapsPositionButtonsAndScaling`
* `TryFromNotification_TouchDown_UsesTouchIdAndLeftButton`
* `TryFromNotification_MouseWheel_Normalizes120DeltaToNotches`
* `TryFromNotification_EscapeKeyDown_ProducesCancelEvent`
* `TryFromNotification_NonEscapeKeyDown_ReturnsFalse`
* `PixelsToDip_At200PercentScaling_HalvesPixelDistance`

### `InteractableWalkTests.cs`
* `TryBuildHitPath_BeforeLayout_ReturnsFalse`
* `TryBuildHitPath_DisplayNone_SkipsSubtree`
* `TryBuildHitPath_PointerEventsNone_SkipsSubtreeAndHitsSiblingBehind`
* `TryBuildHitPath_OverlappingSiblings_FrontMostChildWinsAndExcludesBackSibling`
* `TryBuildHitPath_HalfOpenBounds_SharedEdgeHitsRightSiblingOnly`
* `TryBuildHitPath_OverflowHidden_PointOutsideClipAreaSkipsChildrenButHitsParent`
* `TryBuildHitPath_RotatedAndScaledParent_MapsWindowToParentAndWindowToNode`
* `SetBehaviors_SameBehaviorOnTwoNodes_LogsWarningAndIgnoresSecondAttachment`
* `SetBehaviors_BehaviorRemoved_CancelsAndDetachesImmediately`

### `PointerArbiterTests.cs`
* `Notify_DragClaimsOverChildClick_CancelsChildClickAndCapturesDrag` *(verifies A3)*
* `Notify_TwoClickGesturesOnSameNode_BothClaimAndFire` *(equal kinds on same node proceed)*
* `Notify_ClickOnChildAndClickOnParent_FrontMostChildWinsAndCancelsParent`
* `Notify_CapturedDragMovesOutsideNodeAndClipArea_ContinuesReceivingMoveAndUpInFrozenParentSpace` *(verifies A1, A5)*
* `Notify_ParentScaledByTwo_DragDeltaInParentSpaceMatchesParentUnits` *(verifies A1)*
* `Notify_SecondTouchJoinsCapturedNode_PinchClaimCancelsDragOnSameNode` *(verifies G5)*
* `Notify_CapturedNodeHiddenViaDisplayNone_CancelsGestureAndRestoresPressValue` *(verifies A6, G8)*
* `Notify_CapturedNodeDetachedFromParent_CancelsGestureImmediately` *(verifies A6)*
* `Notify_EscapeKeyDownWhileCaptured_CancelsCapture` *(verifies G4)*
* `Notify_HoverPath_IncludesAncestorsAndFreezesWhileCaptured` *(verifies A9, G3)*
* `Notify_TouchFollowedByPromotedMouse_IgnoresPromotedMouseNotification` *(touch promotion guard)*

### `StyleNodeTests.cs` (Additions for `SetPointerEvents`)
* `Output_FromConstruction_IsBuilt` (includes `SetPointerEventsNode`)
* `SetPointerEvents_NoneAndAuto_CommitsToInteractable`
* `SetPointerEvents_UnknownEnum_LogsWarningAndIgnores`

---

## 7. Debatable Points

### D1: Where the Arbiter Lives
* **Question:** Should `PointerArbiter` be a field on the root node, an object owned by the root and handed down, or shared per window? And what happens when a subtree is re-rooted mid-gesture?
* **Options:**
  1. **Lazy field `_arbiter` on the root `Interactable<TSelf>` (`_arbiter ??= new PointerArbiter<TSelf>(Self)` inside `Notify`), reached via `AsInteractable(Self.Root())._arbiter`.**
  2. **Pushed down to every descendant on `OnParentChanged`.**
  3. **Shared static/per-window table keyed by `CallerInfo`.**
* **Recommendation:** **Option 1** (lazy field on the root node, reached on rare detach/hide mutations via `Self.Root()`).
  * When a subtree is detached or re-rooted mid-gesture (`OnParentChanged(oldParent, newParent)`), `oldParent.Root()._arbiter?.CancelSubtree(Self)` immediately cancels any active capture/candidates in that subtree on the old root, and if `Self` was previously a root (`newParent is not null && _arbiter is not null`), `Self._arbiter.CancelAll()` clears its old arbiter.
* **Costs:**
  * *Option 1:* Zero memory on every non-root node (`null` pointer); $O(\text{depth})$ walk only when a node is detached or hidden (never on the hot per-event path).
  * *Option 2:* Requires a reference field on every node and $O(\text{subtree})$ updates on every reparenting.
  * *Option 3:* Introduces global mutable state and breaks isolated unit testing.

### D2: The Spaces Handed to Gestures
* **Question:** Should `PointerEvent` carry pre-mapped positions in both node space and frozen parent space, or should the gesture receive matrices and map points itself?
* **Options:**
  1. **`PointerEvent` carries `PositionInWindow`, `PositionInParent`, and `PositionInNode` (`Vector2`s) plus `Scaling` (`float`).**
  2. **`PointerEvent` carries `SKMatrix WindowToParent` and `SKMatrix WindowToNode` and gestures call `MapPoint`.**
* **Recommendation:** **Option 1** (`PointerEvent` carries `PositionInWindow`, `PositionInParent`, `PositionInNode`, and `Scaling`).
* **Costs:**
  * *Option 1:* Keeps Tier 0 gestures (`ClickGesture`, `DragGesture`, `PinchGesture`, `WheelGesture`) 100% free of `SkiaSharp` matrix math and tree transforms—unit tests construct a `PointerEvent` with plain `Vector2`s and test the gesture state machine directly.
  * *Option 2:* Couples every gesture to `SKMatrix` and duplicates `MapPoint` calls across every gesture on the same node.

### D3: Noticing Hide and Detach
* **Question:** `Notify` only runs on input, yet a node hidden or detached while the mouse is still must immediately cancel its gestures. Push vs. pull vs. per-frame check in `Render`?
* **Options:**
  1. **Push from `OnParentChanged`, `EndStyle` (`!IsDisplayed` or `PointerEvents == None`), and `Dispose`, backed up by a cheap pull guard (`ValidateActiveTargets`) at the top of `PointerArbiter.Notify`.**
  2. **Pull-only on next `Notify`.**
  3. **Per-frame check in `Render`.**
* **Recommendation:** **Option 1** (push on `OnParentChanged` / `EndStyle` / `Dispose` + pull guard in `Notify`).
* **Costs:**
  * *Option 1:* Cancels and resets channels on the **exact frame** the node is detached or hidden (even if the user doesn't move the mouse for 10 seconds), and never relies on `Render` being called (headless tests don't call `Render`).
  * *Option 2:* Leaves `IsDragging` / `IsPressed` / `IsHovered` channels stuck at `true` until the next mouse movement.
  * *Option 3:* Fails in unit tests that test layout + `Notify` without `Render`, and adds interaction coupling to `Renderable`.

### D4: Gesture to Behavior Communication
* **Question:** How does a behavior learn about its gesture's transitions?
* **Options:**
  1. **`PointerArbiter` calls `behavior.OnGestureProcessed(gesture, in ev, result)` after `gesture.Process(in ev)`, and `behavior.OnGestureCanceled(gesture)` after `gesture.Cancel()`, and the behavior reads the gesture's state/properties directly.**
  2. **`Process` returns a rich transition struct containing deltas, click counts, and flags.**
  3. **The gesture holds `Action<...>` callbacks passed into its constructor.**
* **Recommendation:** **Option 1** (`OnGestureProcessed` / `OnGestureCanceled` on `Behavior`, polling the gesture's typed properties).
* **Costs:**
  * *Option 1:* Zero delegates/closures, no constructor arguments on gestures, and each concrete behavior (`Draggable`, `Clickable`, `Pinchable`) has a strongly-typed field (`private readonly DragGesture _gesture = new();`) whose properties (`_gesture.OffsetInParent`, `_gesture.Phase`) it reads without casting or union structs.
  * *Option 2:* Forces one bloated return struct to carry union fields for click, drag, pinch, and wheel.
  * *Option 3:* Allocates delegates in constructors and violates the "no wiring in constructors" rule.

### D5: The `Notify` Return Value to `VL.Skia`
* **Question:** When does the root report `true` ("handled") to `VL.Skia`?
* **Options:**
  1. **Return `true` when a gesture claims/captures or enters `Possible` (`Pending`), OR when a `TouchNotification` hits any node in the tree (to set `touchNotification.Handled = true` and prevent Win32 touch-to-mouse promotion). Return `false` on passive `MouseMove` hover over nodes without active/pending gestures.**
  2. **Return `true` whenever the hit path is non-empty on any event (including passive `MouseMove`).**
  3. **Return `true` only when a gesture returns `Claim`.**
* **Recommendation:** **Option 1**.
* **Costs:**
  * *Option 1:* Prevents clicks/drags on a UI node from leaking through to `VL.Skia` layers behind the UI, suppresses Win32 double-tap promotion on touchscreens, and still lets passive `MouseMove` pass through to background Skia layers when nothing is pressed.
  * *Option 2:* Swallows every `MouseMove` over any background `FuNode` container, breaking camera/cursor layers behind the UI.
  * *Option 3:* Returning `false` on `Down` while `Possible` would let layers behind the UI steal the `MouseDown` before `Click` or `Drag` claims on `Move`/`Up`, and would cause Win32 `TouchInput` to synthesize duplicate mouse clicks.

### D6: Internal Matrix Type
* **Question:** Should internal routing and capture use `SKMatrix`, `System.Numerics.Matrix3x2`, or Stride `Matrix`?
* **Options:**
  1. **`SKMatrix` internally (`WindowToParent`, `WindowToNode`), converting to Stride `Vector2` for `PointerEvent` and Stride `Matrix` on public pins.**
  2. **`System.Numerics.Matrix3x2` internally.**
  3. **Stride `Matrix` (4×4) everywhere.**
* **Recommendation:** **Option 1** (`SKMatrix` internally, Stride `Vector2`/`Matrix` on `PointerEvent` and pins).
* **Costs:**
  * *Option 1:* Zero conversion between `CallerInfo.Transformation` (`SKMatrix`) and `Layoutable.LocalTransformation` (`SKMatrix`), and supports 2D projective transforms (`Persp0/1/2`) identically to `Renderable.Render`.
  * *Option 2:* `Matrix3x2` drops perspective components and requires converting `SKMatrix` on every node during the walk.
  * *Option 3:* 4×4 inversion at every node is slower and still requires converting from `CallerInfo.Transformation` (`SKMatrix`).

### D7: How Often to Walk the Tree
* **Question:** Is the tree walk repeated on every `Move`, or only on `Down` and on `Move` while nothing is captured?
* **Options:**
  1. **Walk on `Down`, `Wheel`, and on `Move` / `Up` *only while no pointer is captured*. While any pointer is captured, skip the tree walk completely (deliver directly to `CapturedNode` using its frozen matrices, and keep hover frozen per `G3`).**
  2. **Walk on every `Move` even while captured.**
* **Recommendation:** **Option 1**.
* **Costs:**
  * *Option 1:* Directly implements `G3` ("while a pointer is captured, hover is frozen to the capturer's path") and makes dragging $O(1)$ regardless of tree size!
  * *Option 2:* Wastes $O(N)$ tree walks on every drag frame only to discard the result because capture and hover are frozen anyway.

---

## 8. Deviations from the Review

None. Every binding decision (`G1`–`G8`, `Q3`, `Q4`, composition rules, and fixes `A1`–`A10`) is implemented as specified in `review-interaction-architecture.md`.

---

## 9. Later

Deliberately left to subsequent proposals and tasks:
* **Proposal 02 (`proposal-interaction-02-headless.md`):** Concrete implementations of `ClickGesture`, `DragGesture`, `PinchGesture`, `WheelGesture` (Tier 0) and `Hoverable`, `Clickable`, `Draggable`, `Pinchable` (Tier 1).
* **Proposal 03 (`proposal-interaction-03-canvas-selection.md`):** `SelectionProvider`, `CanvasProvider`, `IGroupManipulable` (Tier 2) and `Selectable`, `Movable`, `Transformable` (Tier 3).
* **Task 05 (Sibling Z-Index):** Implementing the sorted child cache inside `Interactable<TSelf>.OrderedChildren`.
* **Task 06 (Overlay) & Task 07 (`SetClipRect`):** Extending `TryBuildHitPath` to visit overlay roots first and gate by `CustomClipRect`.
* **Keyboard & Focus Model (`G4`):** Focus target tracking, Tab navigation, `GotFocus`/`LostFocus` per node, and `KeyPressable`.
