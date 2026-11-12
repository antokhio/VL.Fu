---
applyTo: "VL.Fu/src/Behaviors/**/*.cs,VL.Fu/src/Core/Interaction/**/*.cs,VL.Fu.Tests/Behaviors/**/*.cs,VL.Fu.Tests/Core/Interaction/**/*.cs"
---

# Writing behaviors

A behavior is a VL.Skia `IBehavior` attached to a Fu node through `SetBehaviors`. It turns the node's notifications into channel writes; the patch applies them. Behaviors never move, resize or restyle a node themselves.

| Part | Location | Namespace | Role |
|---|---|---|---|
| Behavior | `VL.Fu\src\Behaviors\X.cs` | `VL.Fu.Behaviors` | Process node with channel pins; one per gesture or role. |
| Notification | `VL.Fu\src\Behaviors\XNotification.cs` | `VL.Fu.Behaviors` | What a behavior sends to other nodes (selection, group manipulation). |
| Recognizer | `VL.Fu\src\Core\Interaction\Recognizers\XGestureRecognizer.cs` | `VL.Fu.Core.Interaction.Recognizers` | The gesture's state machine, without pins or channels. |
| Plumbing | `VL.Fu\src\Core\Interaction` | `VL.Fu.Core.Interaction` | Dispatcher, routes, claims, `PointerInput`; changes go through a phase spec. |

The design is sealed in `.github\summaries\summary-interaction-v2.md` (§5 claims, §6 behaviors, §7 selection and groups, §8 canvas); codes such as B7 or O2 refer to it. Each phase is specified in `.github\tasks\task-interaction-NN-*.md`, whose "As built" section records where the code differs from the plan.

## 1. The process node

```csharp
namespace VL.Fu.Behaviors
{
    /// <summary>
    /// Makes the node it is attached to draggable: one pointer pressed with one of <see cref="Buttons"/> that moves further
    /// than <see cref="Threshold"/>. Attach it to a Fu node's behaviors and connect channels to the outputs you need.
    /// </summary>
    [ProcessNode(Name = "Draggable", HasStateOutput = true, FragmentSelection = FragmentSelection.Explicit)]
    public sealed class Draggable : NodeBase, IBehavior
    {
        private readonly DragGestureRecognizer _drag = new();

        private IChannel<Vector2>? _offset = null;
        private bool _enabled = true;

        private bool _granted = false;
        private int? _owedRelease = null;

        [Fragment]
        public Draggable([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        /// <summary>
        /// How far the pointer moved since the press, in the space of the node's parent as it was at the press.
        /// </summary>
        public IChannel<Vector2>? Offset => _offset;

        /// <summary>
        /// Connects the channel that receives the offset. Changing it during a drag cancels the drag,
        /// which writes zero to the previous channel.
        /// </summary>
        [Fragment(Order = PinOrder.Behavior)]
        public void SetOffset(IChannel<Vector2>? offset)
        {
            if (ReferenceEquals(offset, _offset))
                return;

            CancelDrag();
            _offset = offset;
        }

        // Settings, SetEnabled, Notify, OnGranted, OnCancelled, Reset and ReleaseOwedClaim: see the sections below.
    }
}
```

Rules:

* `public sealed class X : NodeBase, IBehavior` with `[ProcessNode(Name = "X", HasStateOutput = true, FragmentSelection = FragmentSelection.Explicit)]`. The constructor takes the hidden `NodeContext`, has an empty body and is marked `[Fragment]`, otherwise VL doesn't show the node.
* There is no behavior base class (v2 §6.1). Copy the cancel and owed-release code from the closest sibling instead:
  * `Draggable` for one pointer;
  * `Pinchable` for two pointers;
  * `CanvasProvider` and `Scrollable` for behaviors that write a value the patch feeds back;
  * `Movable` and `Transformable` for group work.
* Every setter is marked `[Fragment(Order = PinOrder.Behavior)]`, except `SetEnabled(bool enabled = true)`, which uses `PinOrder.Enabled`. Each getter and setter pair follows `public X => _x; public void SetX(...)`. Setters run every frame, so each one returns early when the value hasn't changed.
* Outputs are `IChannel<T>?` pins:
  * The setter compares the new channel with `ReferenceEquals`. A swap cancels a running gesture first, so the previous channel gets the idle facts and the new one stays untouched.
  * A value is written with `_x?.EnsureValue(value)`.
  * An event is an `IChannel<Unit>` fired with `_x?.EnsureValue(Unit.Default, force: true)`.
* A behavior whose value the patch feeds back, such as `Scrollable`'s offset or `CanvasProvider`'s view, reads the channel's current value as its start. Don't keep a second copy of it.
* Settings take plain defaults, such as `Threshold = 4f` or `Buttons = MouseButtons.Left`:
  * A setting the recognizer uses applies from the next press.
  * An invalid value is ignored, and the node logs `this.LogWarning("X must be ..., got {value}. It is ignored.")`. Use `GestureMath.IsValidThreshold` for distances.
  * A setting that disables the behavior, such as `Buttons` set to `None`, is kept but logs why nothing happens.
  * A node never throws.
* Buttons are `MouseButtons`, and a touch counts as `MouseButtons.Left`. A behavior that treats mouse and touch differently, such as `Scrollable`, tells them apart by `sample.PointerId == PointerIds.Mouse` at the press.
* Facts about the node, such as the hit test, the transform pivot and the clip area, come from the internal `IInteractionNode`. Each is forwarded explicitly in `Interactive`. A behavior that needs another layout fact gets it added there; don't make layout members public for it.

## 2. `Notify`

```csharp
public bool Notify(INotification notification, CallerInfo caller)
{
    ReleaseOwedClaim(notification);

    if (!_enabled)
        return false;

    switch (notification)
    {
        case PointerCaptureNotification capture when capture.Grants(this):
            return OnGranted(capture);
        case PointerCancelNotification cancel:
            OnCancelled(cancel);
            return false;
    }

    if (!PointerInput.TryRead(notification, out var sample))
        return false;

    var drag = _drag.Process(in sample);
    switch (drag.Phase)
    {
        case DragPhase.Started:
            // Ancestors still see this move and may ask for the pointer for a stronger gesture.
            notification.RequestPointerCapture(this, GestureKind.Drag);
            return false;
        case DragPhase.Moved when _granted:
            _offset?.EnsureValue(drag.OffsetInParent);
            return true;
        // ...
        default:
            return false;
    }
}
```

* The order is fixed:
  1. settle an owed release, before the enabled check, so a disabled behavior still lets go of the pointer;
  2. return when disabled;
  3. handle Fu's control notifications;
  4. read the pointer sample and feed the recognizer.
* `PointerInput.TryRead` is the one decoder for mouse, touch and wheel. A sample carries:
  * the kind, pointer id, button and modifiers;
  * `PositionInWindow`, and `WheelDelta` in notches;
  * the receiving `Node` and the sender's `WindowToNode` and `WindowToParent` matrices;
  * `Scaling` and `TimeMs`.

  Every other notification is switched on directly, such as `PointerEnterNotification`, a key or the behavior's own notification types.
* Return true only for input the behavior used:
  * a move or release of a granted gesture;
  * the Up that clicked;
  * a wheel notch that changed the value.

  Return false at the press, at the start that only asks for the pointer, and when nothing changed. A wheel at its limit then goes on to an outer scroll view. The result of a control notification is never read (v2 §11).
* The wheel takes no claim. It bubbles up from the deepest node until a behavior returns true.
* The dispatcher ignores the mouse while a touch is down and for a short time after it lifts (`TouchPromotionGuard`), because a host can make mouse input out of a touch. A behavior doesn't filter that input itself.

## 3. Claims

A gesture that needs the pointer to itself asks for it and publishes only once the claim is granted (v2 §5). Requesting doesn't mean winning: a stronger gesture, or one that asked on a deeper node, can take the pointer.

* Ask when the recognizer starts, with `notification.RequestPointerCapture(this, GestureKind.X)`. `Pinchable` passes both of its pointers. The kinds rank `Click` < `Drag` < `Pinch`.
* Before asking, a behavior that has nothing to do can cancel its recognizer and leave the pointer to its ancestors. `Scrollable` does this for a drag along an axis it can't scroll.
* `OnGranted`:
  * When the recognizer is no longer started or moving, it releases with `capture.ReleasePointerCapture(this)` and returns false.
  * Otherwise it freezes what the gesture measures from, such as the value at the start, publishes the start and returns true.
* `OnCancelled` ignores:
  * an idle recognizer;
  * another pointer;
  * a cancel where `cancel.IsMember(this)` is true.

  A gesture that asked but wasn't granted keeps its state when `cancel.SparesPending(kind)`, so an ancestor's pinch survives a child's drag. Otherwise it resets with `Reset(owesRelease: false)`, because the claim is already gone.
* A cancel from a setter, such as disabling or a channel swap, calls `Reset(owesRelease: true)`. A setter has no notification to release with, so a granted gesture stores its pointer id in `_owedRelease`. The release then happens on the next notification of that pointer.
* Idle facts (B7): a gesture that published writes its idle values when it is cancelled and fires no end event:
  * the offset goes to zero, or back to its value at the start for a fed-back value;
  * the flags go to false.

  A gesture that never published stays silent.
* A behavior removed from `SetBehaviors` is cancelled through the dispatcher's sweep like any other, so it needs no extra code.

## 4. Spaces

* Freeze the matrices at the press. Offsets are measured with the press-time `WindowToParent` or `WindowToNode`, so they stay right while the patch moves the node by them.
* Distances given by the user, such as thresholds, are in DIP. The recognizers apply `Scaling`.
* A transform about a point other than the node's pivot is compensated with `IInteractionNode.TransformPivotInParent`.
* A behavior writes values the patch can apply as they are. `Draggable` writes an offset in the parent's space, `Scrollable` an offset the patch negates into a translation, and `CanvasProvider` a view matrix.

## 5. Talking to other nodes

* `notification.Dispatch(x)` sends `x` to the receiving node's subtree after the current delivery. `notification.Query(q)` delivers `q` there synchronously; its receivers only add to the accumulator `q` carries. The internal `DispatchInto` sends into an ancestor's subtree, such as the selection scope.
* A custom notification is a `public sealed class XNotification : NotificationBase` and implements `WithSender` and `Transform`. Use `SelectionNotification` and `GroupManipulationNotification` as templates.
* A receiver ignores what it dispatched itself by comparing the sender.
* Wrong calls log a warning and do nothing, never throw. That covers calls outside a delivery, with a foreign notification or while answering a query.

## 6. Recognizers

* A recognizer is a phase enum plus a `switch`, with idle field initializers. `Process(in PointerSample)` returns the new state, `Current` holds it, and `Cancel()` returns to idle.
* A recognizer has no channels, logging or `IObservable`.
* Reuse `ClickGestureRecognizer`, `DragGestureRecognizer` and `PinchGestureRecognizer`. Shared math, such as thresholds, goes into `GestureMath`; don't copy it into behaviors.
* `PinchGestureRecognizer` pairs any two pointers. A behavior that only pinches with touches routes the mouse elsewhere, as `CanvasProvider` does.

## 7. Tests

Put them in `VL.Fu.Tests\Behaviors\XTests.cs` as a `public sealed class XTests` marked `[TestClass]`. Recognizer tests go in `VL.Fu.Tests\Core\Interaction\Recognizers`, using `TestPointer`.

Helpers in `BehaviorScene.cs`:

* `BehaviorScene`:
  * Layout: a 100 × 100 `Root`; `Back` at (10, 10) and `Front` at (30, 30), both 40 × 40; `Child` at `Front`'s top-left corner, 10 × 10. So (35, 35) hits `Child`, (60, 60) `Front` and (15, 15) `Back`.
  * Input runs through the real dispatcher, so claims are requested, resolved, granted and cancelled as in a patch.
  * Input helpers: `Down`, `Move`, `Up`, `Wheel` (with modifiers), `HorizontalWheel`, `TouchDown`, `TouchMove`, `TouchUp`, `Escape`.
  * Scene helpers: `Attach`, `Resize`, `AddChild`, `Clip`. `Claims` exposes the claims.
* `Recorder<T>`: a real channel plus every value written into it. Equal `EnsureValue` writes show up once; fired events show up every time.
* `Probe`: a behavior made of one function, for a competitor that only asks, processes or watches.

Rules:

* Create behaviors with `new X(null!)`. Each test owns its scene and recorders through `using var`.
* Name tests `Subject_Condition_Result`, for example `Disabling_DuringADrag_WritesTheIdleFacts_AndReleasesTheClaimOnTheNextNotification`.
* Cover:
  * the lifecycle: nothing before the threshold, the start once granted, the values, and the end on the Up;
  * the buttons, and touch against mouse;
  * every cancel path: Esc, disabling, a channel swap, removal, and another gesture taking the pointer. Each one writes the idle facts and fires no end event.
  * an owed release: after the next notification, `scene.Claims.ClaimOf(PointerIds.Mouse)` is null;
  * competition with nested behaviors: an inner `Draggable` wins over an outer one, and a `Clickable` under a drag doesn't click;
  * invalid settings are ignored, with a `[DataRow]` per value (negative, NaN, infinity).
* Removal is `scene.Attach(node)` with no behaviors, followed by `scene.Root.Dispatcher.Sweep()`.
* Don't mix touch and mouse in one test, since the dispatcher mutes the mouse during a touch. Test the mouse during a drag with a mouse drag.

Verify by building the solution and running the `VL.Fu.Tests` tests: in Visual Studio through the build and Test Explorer (filtered to project `VL.Fu.Tests`), from the command line with `dotnet test VL.Fu.Tests\VL.Fu.Tests.csproj`.

## Docs

* Use a multi-line `<summary>` saying what the behavior does and how to wire it ("Attach it to a Fu node's behaviors and connect channels to ...").
* Use `<remarks>` for:
  * when the behavior publishes;
  * the space its values are in;
  * what a cancel writes;
  * which gestures it competes with.
* A channel setter's summary says what changing it during a gesture writes, and a setting's summary says when it applies.
* A new behavior gets:
  * a phase spec in `.github\tasks\` with decisions, tests and an "As built" section;
  * a row in `.github\tasks\tasks-interaction.md`.
