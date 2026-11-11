# Task interaction-05: Recognizers

Source: [summary-interaction-v2.md](../summaries/summary-interaction-v2.md) §3.4, §6.1, §9 (F1, F3, F6, D2), §14 N5; v1 [summary-interaction.md](../summaries/summary-interaction.md) §2.2 F6, §2.4 B6, B8, §7.1, §8. Depends on: [task-interaction-01](task-interaction-01-plumbing.md).

## Goal

Fu behaviors decode pointer input once and feed it to small state machines. After this task:
- `PointerInput.TryRead(notification, out PointerSample sample)` turns a mouse or touch notification that reached a Fu node into one flat value: kind, pointer, button, modifiers, window position, the sender's spaces, scaling and time;
- `ClickGestureRecognizer`, `DragGestureRecognizer` and `PinchGestureRecognizer` turn samples into typed states. `Process` returns the state for that sample; nothing is pushed;
- every recognizer freezes its settings and its mapping at press, and `Cancel()` returns it to exactly its idle field values.

Nothing calls them yet: behaviors are N6. N5 is tested with plain sample sequences.

## Design

### 1. Types

All internal. Public outputs (a public sample, `IObservable<TState>`) stay a later option (v2 §6.1).

| Type | Kind | File |
|---|---|---|
| `PointerSampleKind` | enum: `Down`, `Move`, `Up`, `Wheel` | `Core\Interaction\PointerSample.cs` |
| `PointerSample` | readonly struct, `init` properties | `Core\Interaction\PointerSample.cs` |
| `PointerInput` | static class: `TryRead` | `Core\Interaction\PointerInput.cs` |
| `ClickGestureRecognizer`, `ClickGestureState`, `ClickPhase` | sealed class, readonly record struct, enum | `Core\Interaction\Recognizers\ClickGestureRecognizer.cs` |
| `DragGestureRecognizer`, `DragGestureState`, `DragPhase` | same | `Core\Interaction\Recognizers\DragGestureRecognizer.cs` |
| `PinchGestureRecognizer`, `PinchGestureState`, `PinchPhase` | same | `Core\Interaction\Recognizers\PinchGestureRecognizer.cs` |

v1's `HoverGestureRecognizer` and `WheelGestureRecognizer` are dropped: hover comes as `PointerEnterNotification` and `PointerLeaveNotification` (N3), and a wheel is one sample with nothing to recognize (Decision 5).

### 2. `PointerSample`

| Member | Meaning |
|---|---|
| `Kind` | `Down`, `Move`, `Up`, `Wheel` |
| `PointerId` | `PointerIds.Mouse` (−1) or the touch id |
| `Button` | `Down`, `Up`: the button that changed; a touch is `Left`. `Move`, `Wheel`: `None`. VL's mouse moves carry no button state, so recognizers remember the button they pressed with |
| `Modifiers` | `Keys` of the notification |
| `PositionInWindow` | `Vector2`, window pixels, as the notification carries it |
| `WheelDelta` | `Vector2` in notches (raw / 120): X from `MouseHorizontalWheelNotification`, Y from `MouseWheelNotification`, sign as VL. Zero for other kinds |
| `Node` | `IInteractive`, the sender's node |
| `WindowToNode`, `WindowToParent` | `SKMatrix?` from the sender. Null while that space is scaled to zero |
| `Scaling` | The sender's `Scaling`: window pixels per DIP |
| `TimeMs` | The sender's `TimeMs` |

- `TryGetPositionInNode(out Vector2)` and `TryGetPositionInParent(out Vector2)` map `PositionInWindow` through the current matrices, false when the matrix is null. Recognizers don't use them: they map through matrices frozen at press.
- `ToDip(float pixels)` divides by `Scaling`, treating a non-finite or non-positive scaling as 1.

### 3. `PointerInput.TryRead`

| Notification | Sample |
|---|---|
| `MouseDownNotification`, `MouseUpNotification` | `Down`, `Up`, `Button` = its `Buttons` |
| `MouseMoveNotification` | `Move` |
| `MouseWheelNotification`, `MouseHorizontalWheelNotification` | `Wheel` |
| `TouchNotification` | `Down`, `Move`, `Up` by its kind, `Button` = `Left` on Down and Up |
| everything else: `MouseClickNotification` (B4), `MouseLostNotification`, `GestureNotification`, keys, focus, Fu's capture, cancel, enter and leave | false |

- **The sender must be a `FuNotificationSender`,** otherwise false. A Fu behavior on a Fu node always gets one (v2 §3.4); one placed under a plain VL layer stays inert (Decision 4).
- Called once per behavior per notification; it allocates nothing.

### 4. Recognizer shape (v2 §6.1, v1 F3, F6)

Every recognizer:
- holds its state in fields whose initializers are the idle values. The constructor is empty. `Cancel()` assigns the same values back, so a cancelled recognizer and a new one can't be told apart;
- has `SetX` settings that compare before storing and are captured at press into `_xAtPress` fields (F6). A change applies to the next press. A non-finite or negative threshold is ignored and the old value kept: behaviors warn about it (N6);
- has `Process(in PointerSample sample)`, a `switch` over its phase, returning the state **for this sample**;
- has `Current`, the state after the last sample it used, or the idle state after `Ended`, `Released`, `Cancel()`;
- remembers the node of its press and ignores samples from any other node (v2 §6.1: one instance per node is a convention);
- never allocates in `Process`.

**Phases name what a sample did.** `Unchanged` is returned for a sample the recognizer ignored or that changed nothing it reports (another pointer, a chorded button, a move within the threshold, a wheel). `Current.Phase` is never `Unchanged`. Behaviors switch on the returned phase and treat `Unchanged` like `default` (Decision 1).

`Released` is shared by all three: the pointer went up before the gesture happened.

### 5. `ClickGestureRecognizer`

Settings: `SetButtons(MouseButtons)` (default `Left`), `SetThresholdDip(float)` (default 6).

| Phase now | Sample | Returns | Next |
|---|---|---|---|
| Idle | `Down`, `Button` in buttons | `Pressed` | Pressed; freezes pointer, button, node, press position, scaling, buttons and threshold |
| Pressed | `Move` of its pointer, distance from press < threshold | `Unchanged` | |
| Pressed | `Move` of its pointer, distance ≥ threshold | `Slipped` | Slipped |
| Pressed | `Up` of its pointer and button | `Clicked` | Idle |
| Slipped | `Up` of its pointer and button | `Released` | Idle |
| any | anything else | `Unchanged` | |

- The distance is in DIP: window pixels between the press and the sample, divided by the scaling frozen at press.
- Once slipped, a press never clicks, even if the pointer comes back.
- `ClickGestureState(Phase, PointerId, PressInWindow)`.

### 6. `DragGestureRecognizer`

Settings: `SetButtons(MouseButtons)` (default `Left`), `SetThresholdDip(float)` (default 4).

| Phase now | Sample | Returns | Next |
|---|---|---|---|
| Idle | `Down`, `Button` in buttons, `WindowToParent` not null | `Pressed` | Pressed; freezes pointer, button, node, `WindowToParent`, scaling, settings, the press in window and parent space |
| Pressed | `Move` of its pointer, distance < threshold | `Unchanged` | |
| Pressed | `Move` of its pointer, distance ≥ threshold | `Started` | Started |
| Started, Moved | `Move` of its pointer | `Moved` | Moved |
| Pressed | `Up` of its pointer and button | `Released` | Idle |
| Started, Moved | `Up` of its pointer and button | `Ended` | Idle |
| any | anything else | `Unchanged` | |

- `DragGestureState(Phase, PointerId, StartInParent, PositionInParent, OffsetInParent)`, with v1 §7.1:

```
startInParent    = windowToParentAtPress · pressInWindow
positionInParent = windowToParentAtPress · positionInWindow
offsetInParent   = positionInParent − startInParent
```

- **The offset is measured from the press,** not from where the threshold was crossed, so a dragged node stays under the pointer (Decision 2). `Started` already carries an offset of about the threshold.
- `Ended` carries the offset of the Up's position.

### 7. `PinchGestureRecognizer` (v1 B6)

Settings: `SetThresholdDip(float)` (default 4). Any pointer counts, mouse or touch, any button.

| Phase now | Sample | Returns | Next |
|---|---|---|---|
| Idle | `Down`, `WindowToParent` not null | `Pressed` | Pressed; freezes the first pointer, node, `WindowToParent`, scaling and threshold |
| Pressed | `Down` of another pointer | `Paired` | Paired; anchors both positions: center, distance, angle |
| Paired | `Move` of either pointer, both within the threshold of their anchor | `Unchanged` | |
| Paired | `Move` of either pointer, one past the threshold | `Started` | Started |
| Started, Moved | `Move` of either pointer | `Moved` | Moved |
| Pressed, Paired | `Up` of a tracked pointer | `Released` | Idle |
| Started, Moved | `Up` of a tracked pointer | `Ended` | Idle |
| any | a third pointer, anything else | `Unchanged` | |

- Both positions map through the `WindowToParent` frozen at the **first** press, so the two fingers are in one space.
- `PinchGestureState(Phase, FirstPointerId, SecondPointerId, CenterInParent, Scale, Rotation, TranslationInParent)`, totals since `Paired`:

```
centerInParent      = (p1 + p2) / 2
scale               = |p2 − p1| / |p2 − p1|anchor        1 when the anchor distance is below 1e-4
rotation           += wrap(angle(p2 − p1) − lastAngle)   wrap into (−π, π], radians, unwrapped total
translationInParent = centerInParent − centerAnchor
```

- When either pointer lifts, the pinch ends and the recognizer is idle. The other pointer is no longer tracked: it can't start a new pinch until it is pressed again (B6).
- The pointer ids in the state are what a behavior passes to `RequestPointerCapture` (N4 §3).

## Decisions (settled)

1. **`Process` returns what the sample did,** with `Unchanged` for samples that changed nothing reported. A behavior that returns true on `Moved` can't claim another pointer's notification by accident, which returning the standing phase would allow.
2. **The drag offset starts at the press** (v1 B8 measured it from the start). Behaviors write `Offset` from `Current` when the claim is granted (v2 §6.1), so the node jumps to the pointer once, by the threshold, and then follows it exactly.
3. **Click threshold in DIP from the press, sticky.** Coming back doesn't restore the click.
4. **`TryRead` needs a Fu sender.** Without one there are no node spaces to freeze. Fu behaviors under a plain VL layer do nothing; N6 may warn.
5. **No hover or wheel recognizers.** Hover is notification-based since N3. A wheel has no state; `CanvasProvider` and scroll views read `WheelDelta` from the sample.
6. **`GestureNotification` isn't read.** Windows sends it only to windows that don't register touch; VL's renderers do. Touchpad pinch through it is deferred.
7. **A pinch needs two new presses on the node.** A pointer that was already down before the recognizer went idle isn't adopted.

## Tests

`VL.Fu.Tests\Core\Interaction\PointerInputTests.cs` and `VL.Fu.Tests\Core\Interaction\Recognizers\*Tests.cs` (new), on samples built directly, plus a few `TryRead` cases with a real `FuNotificationSender`:

- **`TryRead`.** Each mouse and touch kind maps to its sample; touch Down and Up are `Left`; wheel deltas are in notches on the right axis; click, lost, gesture, key and Fu notifications return false; a non-Fu sender returns false; the matrices, scaling and time come from the sender, and null spaces stay null.
- **Shape, for each recognizer.** A new and a cancelled recognizer have equal `Current` and process the same sequence the same way. A setting changed during a press applies to the next press. Samples from another node, another pointer or a chorded button are `Unchanged`. `Process` allocates nothing (`GC.GetAllocatedBytesForCurrentThread`).
- **Click.** Down then Up: `Pressed`, `Clicked`. Slip past 6 DIP: `Slipped`, then `Released` even after coming back. The threshold scales with `Scaling`. A button outside the setting is `Unchanged`. Right-button setting clicks with the right button only.
- **Drag.** Within the threshold: `Unchanged`, Up gives `Released`. Crossing it: `Started` with the offset from the press; later moves `Moved`; Up `Ended`. A parent rotated or scaled at press gives the offset in parent space; a sample whose matrices changed after the press still uses the frozen ones (A1). A press with no `WindowToParent` doesn't start.
- **Pinch.** Second pointer: `Paired`. Within the threshold: `Unchanged`; past it: `Started`. Spreading the fingers to twice the distance gives scale 2; turning them by 3π/2 in steps gives a rotation of 3π/2, not −π/2; moving both gives the translation. One lifts: `Ended` and idle; the remaining pointer's moves are `Unchanged`. A third pointer is ignored.

## As built

Where the implementation differs from the design above:

* `DragGestureState` is `(Phase, PointerId, StartInParent, PositionInParent)`. `OffsetInParent` is a computed property, so it can't disagree with the two positions.
* The distance and angle math shared by the three is in an internal `GestureMath`: `Map`, `DistanceDip`, `Wrap`, `IsValidThreshold`.
* Click: an Up from `Pressed` also checks its own distance. An Up past the threshold without a Move before it gives `Released`, not `Clicked`.
* Pinch, `Pressed`: moves of the first pointer return `Unchanged` but are tracked, so `CenterInParent` follows it and the anchors are taken where both pointers are at `Paired`.
* Pinch, `Paired`: moves within the threshold update the totals in `Current` while returning `Unchanged`, so `Started` already carries the totals of its own move.
* Pinch: each pointer's Up must match the button it was pressed with; another button's Up is `Unchanged`. `Ended` carries the totals at the Up's position.
* `TryRead` tests cover click and key notifications as the ones it doesn't read; lost, gesture and Fu notifications fall through the same `default`.
* Tests build samples with an internal `TestPointer` helper (`Recognizers\TestPointer.cs`), which also holds a minimal `TestNode`.

## Deferred

* Behaviors that use the recognizers,
* `GestureNotification` (touchpad) as pinch input; long press and double click from `TimeMs`.
* Public `PointerSample` and `IObservable<TState>` outputs.

## Risk

* **A pending pinch and a granted child drag.** v2 §5.1 lets an ancestor's pinch replace a child's drag claim. When the child's drag is granted first, the cancel for that finger reaches the ancestor's `Pinchable` too, and "everyone who isn't a member resets" (v2 §5.3) would reset its paired recognizer before it can request. **Settled for N6:** a behavior whose gesture is still pending (not granted) and of a **higher** kind than the cancel's claim keeps its recognizer's state. A cancel with a null claim resets everyone.
* The threshold depends on `Scaling` being window pixels per DIP, as v1 §10 assumed. Not yet checked on a scaled display.
