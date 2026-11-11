# Task interaction-06: Behaviors

Source: [summary-interaction-v2.md](../summaries/summary-interaction-v2.md) §3.5, §5.1–§5.4, §6.1, §6.2, §9, §13 (O2), §14 N6; v1 [summary-interaction.md](../summaries/summary-interaction.md) §2.2 F6, F7, §2.4 B2–B8, §6.1, §8. Depends on: [task-interaction-04](task-interaction-04-claims.md), [task-interaction-05](task-interaction-05-recognizers.md).

## Goal

The first behaviors a patch can use. After this task:
- `Hoverable`, `Clickable`, `Draggable` and `Pinchable` are VL process nodes. Their state output is an `IBehavior` for a node's `Behaviors` pin;
- each one decodes pointer input with `PointerInput.TryRead`, feeds its N5 recognizer, requests claims through N4 and writes facts into VL channels;
- cancels, disabling and swapping a channel always leave the channels in their idle facts (B7, F6, F7);
- headless widgets (a button, a drag handle, a pinch-zoom area) can be patched. That is the milestone after N6.

## Design

### 1. Types and files

| Type | Kind | File |
|---|---|---|
| `Hoverable` | public sealed class, `NodeBase, IBehavior` | `Behaviors\Hoverable.cs` |
| `Clickable` | same | `Behaviors\Clickable.cs` |
| `Draggable` | same | `Behaviors\Draggable.cs` |
| `Pinchable` | same | `Behaviors\Pinchable.cs` |

- Namespace `VL.Fu.Behaviors`, next to `VL.Fu.Styles`, so the node browser shows them under `Fu.Behaviors`.
- `NodeBase` is the node base Fu nodes already use, for patch warnings and the hidden `NodeContext`. It isn't a behavior base: there is still no shared behavior class (v2 §6.1).
- `[ProcessNode(Name = "...", HasStateOutput = true, FragmentSelection = FragmentSelection.Explicit)]`. Only the setters are fragments; the getters are for C# and tests and don't become pins.
- The constructor takes `[Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext`. Idle state is in field initializers, not in the constructor.

### 2. Channels

No helpers: the behaviors call VL's channel API directly.
- Facts: `channel?.EnsureValue(value)`, which doesn't write a value equal to the current one.
- Events: `channel?.EnsureValue(Unit.Default, force: true)`, which pushes even though the value never changes. Patched behaviors use the same call.
- Swaps: each setter compares the new channel with the field using `ReferenceEquals`.
- An unconnected channel pin gives VL's `DummyChannel`, which is disabled and ignores writes. A disposed channel is disabled too, before its subject is disposed. Writes need no guards beyond the null check.

### 3. The shared shape

Every behavior has:

| Member | Pin | Meaning |
|---|---|---|
| `Enabled => _enabled; SetEnabled(bool enabled = true)` | order `PinOrder.Enabled` (100) | False cancels at once and writes the idle facts (F6) |
| `X => _x; SetX(IChannel<T>? x)` per channel | order `PinOrder.Behavior` (25) | Swapping an instance cancels a running gesture, writes the idle facts to the old channel and keeps the new one untouched until the next gesture (F7) |
| `SetButtons(MouseButtons buttons = Left)`, `SetThreshold(float threshold)` | order 25 | Forwarded to the recognizer, which applies them from the next press (F6) |

- **Settings warn, never throw.** An invalid threshold (`!GestureMath.IsValidThreshold`) logs a warning via `Log(LogLevel.Warning, ...)` and keeps the last valid value. `MouseButtons.None` warns that the behavior will never press.
- **`Notify` while disabled** returns false and does nothing else, with one exception: if the behavior is still a member of a claim (it was disabled or lost a channel mid-gesture), it calls `notification.ReleasePointerCapture(this)` on the first notification it sees. A pending flag `_releaseClaim` tracks this. Without a notification there is no acting pass to release through.
- **Cancel handling** (`PointerCancelNotification cancel`):
  - ignored when `cancel.IsMember(this)`;
  - ignored when the behavior doesn't track `cancel.PointerId` (Pinchable: either of its two pointers);
  - **keep pending**: ignored when the gesture is not granted, its recognizer is past Idle, and `cancel.Claim is { IsActive: true }` with `Kind` lower than the behavior's own kind. This is the settled N6 rule from task 05: an ancestor's pending pinch survives a child's drag claim, and a pending drag survives a click claim;
  - otherwise: `Cancel()` the recognizer, write the idle facts, clear `_granted`, return false.
- **Grant** (`PointerCaptureNotification capture when capture.Grants(this)`): publish the start, set `_granted`, return true.
- One instance per node is a convention (v2 §6.1); the recognizers already ignore samples from a node other than the one pressed on.

### 4. `Hoverable`

| Channel | Type | Written |
|---|---|---|
| `IsHovered` | `IChannel<bool>` | `PointerEnterNotification` → true, `PointerLeaveNotification` → false |

- Ignores claims, captures, cancels and pointer ids: hover has one path, for the mouse or the primary touch (v2 §3.5). Returns false.
- Disabling or swapping writes false. Re-enabling while the pointer is over the node leaves it false until the next Enter (Deferred: ask the hover tracker).

### 5. `Clickable`

| Channel | Type |
|---|---|
| `OnClick` | `IChannel<Unit>` |
| `IsPressed` | `IChannel<bool>` |

Settings: `Buttons` (Left), `Threshold` (6 DIP).

| Recognizer phase | Behavior | Returns |
|---|---|---|
| `Pressed` | remember `_pressedNode = sample.Node`, `_pressedPointer = sample.PointerId`; `IsPressed = true` at once (O2) | false: ancestors and a `Draggable` beside it see the Down too |
| `Slipped` | nothing: `IsPressed` stays true until the Up (Decision 2) | false |
| `Clicked` | `IsPressed = false`, fire `OnClick` | **true**: N4 then cancels ancestors along the path, so the innermost click wins |
| `Released` | `IsPressed = false` | false |

A cancel for the pressed pointer always resets the recognizer, so `OnClick` can't fire (O2). Then:
- `cancel.Claim?.Node` is `_pressedNode` (for example a `Draggable` beside it took over): `IsPressed` stays true, and `_holdUntilUp` is set. The claim node gets all of the pointer's input, so the Up arrives; any `Up` sample of `_pressedPointer` then writes false;
- otherwise (another node took over, the press was lost, the node was swept): `IsPressed = false` at once.

`Clickable` never requests a claim: a click has nothing to guard while it moves.

### 6. `Draggable`

| Channel | Type |
|---|---|
| `Offset` | `IChannel<Vector2>`, in the parent space frozen at press |
| `IsDragging` | `IChannel<bool>` |
| `OnDragStart`, `OnDragEnd` | `IChannel<Unit>` |

Settings: `Buttons` (Left), `Threshold` (4 DIP).

As in the v2 §6.1 example:
- `Started` → `notification.RequestPointerCapture(this, GestureKind.Drag)`, return false. Ancestors still see this Move and may ask for a stronger claim;
- grant → `Offset` from `Current`, `IsDragging = true`, fire `OnDragStart`, return true;
- `Moved` while granted → `Offset`, return true;
- `Ended` while granted → final `Offset`, `IsDragging = false`, fire `OnDragEnd`, return true. `Offset` keeps its value after the end (B8);
- `Moved`/`Ended` while not granted (the request was denied or isn't resolved yet) → false.

Idle facts on a cancel, disable or swap: `Offset = 0`, `IsDragging = false`, no `OnDragEnd` (B7).

### 7. `Pinchable`

| Channel | Type | Idle |
|---|---|---|
| `Scale` | `IChannel<float>` | 1 |
| `Rotation` | `IChannel<float>`, radians | 0 |
| `Translation` | `IChannel<Vector2>`, parent space at the first press | 0 |
| `Center` | `IChannel<Vector2>`, parent space at the first press | left as is |
| `IsPinching` | `IChannel<bool>` | false |

Settings: `Threshold` (4 DIP). There are no buttons: a pinch is two touches.

- `Started` → `RequestPointerCapture(this, GestureKind.Pinch, [first, second])`, all or nothing (§5.1), return false.
- grant → write all totals and `IsPinching = true`, return true.
- `Moved` while granted → totals, return true.
- `Ended` while granted → final totals, `IsPinching = false`, then `notification.ReleasePointerCapture(this)`. The other finger stays down but the recognizer no longer tracks it; releasing ends the claim, and that pointer falls back to its recorded path (§5.4 row 2). Return true. The totals keep their values after the end, as `Offset` does.
- A cancel writes the idle totals and `IsPinching = false`.

### 8. N4 change: a claimed Up always cancels the path

`PointerClaims.Release` sends the cancel along the recorded path only when the Up was handled. A claimed pointer's Up goes only to the claim node. If that node doesn't process the Up, the ancestors that kept a pending gesture (§3 keep pending) or that requested and were denied never hear about it, and they stay pressed.

```csharp
if (handled || pressed.Claim is not null)
    AddCancel(output, pressed.Id, pressed.Claim, pressed.Path, pressed.Path.Count, 0, sender, modifiers);
```

The claim is ended in `Release` before the queued cancel is delivered, so `IsActive` is false then and the keep-pending rule doesn't hold the ancestors. Claim members still ignore it through `IsMember`. One `ClaimsTests` case may assert the old behavior and is updated.

## Decisions (settled)

1. **The behaviors derive from `NodeBase`** for warnings; there is no behavior base class.
2. **A `Slipped` click keeps `IsPressed`** until the Up, like a pressed button that the pointer has left.
3. **`Offset` and the pinch totals keep their values after the end** (B8) and return to idle only on a cancel, disable or swap (B7).
4. **The start is published at the grant**, not at `Started`. Values jump by the threshold at the grant, as in the v2 §6.1 example.
5. **`Pinchable` releases its claim** when one finger lifts.
6. **A disabled behavior, or one whose channel was swapped mid-gesture, leaves its claim** on the next notification it gets.
7. **No behavior consumes the wheel** in N6.
8. **Pin order:** channels and settings at 25, `Enabled` at 100.

## Tests

`VL.Fu.Tests\Behaviors\` (as built: the scenarios live in the per-behavior classes, on `BehaviorScene`; the claimed-Up case is also in `ClaimsTests`):

- **Channels:** first verify that `Channel.Create` works without the VL runtime (v1 §10). If it doesn't, the tests use a small `IChannel<T>` test double.
- **`HoverableTests`:** Enter and Leave write; disabling writes false; swapping writes false to the old channel and leaves the new one.
- **`ClickableTests`:** a click fires once and returns true on the Up; `IsPressed` follows the Down at once; a slip keeps `IsPressed` and doesn't fire; a wrong button is ignored; disabling mid-press writes false.
- **`DraggableTests`:** Started requests a claim and writes nothing; the grant writes the offset and the start; moves and the end write in order; `Offset` keeps its value after the end; a cancel writes 0 and false without `OnDragEnd`; an invalid threshold warns and keeps the old value.
- **`PinchableTests`:** two touches pair, start and request both pointers; the grant writes the totals; one lift ends the pinch and releases the claim; a cancel writes the idle totals.
- **`BehaviorScenarioTests`**, using the Back/Front/Child scene from `ClaimsTests` and real notifications through the dispatcher:
  - `Clickable` and `Draggable` on one node: a drag suppresses `OnClick` but keeps `IsPressed` until the Up (O2);
  - a `Draggable` ancestor takes over a `Clickable` child: the child's `IsPressed` is cleared at once;
  - nested `Clickable`s: only the inner one fires, and the outer one is reset by the cancel after the Up;
  - an ancestor `Pinchable` replaces a child's drag: the child gets `Offset = 0`, `IsDragging = false` and no `OnDragEnd`;
  - a claimed Up that nobody processes still resets a pending ancestor (§8);
  - disabling a granted `Draggable`: idle facts at once, and the claim is gone after the next notification;
  - removing a behavior mid-drag: the sweep cancel writes the idle facts.

## As built

- **Channels:** `Channel.Create` works without the VL runtime, so the tests use real channels (`Recorder<T>`).
- **Idle facts are written only after something was published.** A press, a grant or a hover must have happened first, so a cancel of a gesture that never published leaves the channels alone.
- **`Hoverable`** keeps tracking Enter and Leave while it is disabled, and writes `enabled && hovered`. Re-enabling it while the pointer is over the node writes true at once. Swapping its channel writes false to the old one and the current state to the new one, instead of leaving the new one untouched.
- **Keep pending** is `PointerCancelNotification.SparesPending(GestureKind)` (internal), next to `IsMember`.
- **Owed release:** disabling or swapping during a granted gesture stores the pointer id in `_owedRelease`. The behavior calls `ReleasePointerCapture(this)` on the next notification that has that pointer id, even while it is disabled.
- **Warnings** use `this.LogWarning(...)` from `NodeBase`.

## Deferred

- `Hoverable` doesn't know whether the pointer is over a node it was just added to; it waits for the next Enter.
- Public `IObservable<TState>` outputs, double click, wheel behaviors.
- VL nodes for `Dispatch`, `Query`, `RequestPointerCapture` and `ReleasePointerCapture` for patched behaviors (N10 docs, or earlier if needed).

## Risk

- **Channels in tests.** If `Channel<T>` needs VL's runtime services, the tests switch to a test double; the behaviors themselves don't change.
- **Hover after removal.** A `Hoverable` removed from a node while hovered gets the sweep cancel, which it ignores, so `IsHovered` stays true in its channel. Disposing the node writes nothing either. A fix is either a hover-specific cancel or writing false on any cancel that has a null sender; we choose one once it shows up in a patch.
- **The N4 change** (§8) sends one more cancel per claimed Up. Members ignore it, and behaviors that don't track the pointer ignore it.
