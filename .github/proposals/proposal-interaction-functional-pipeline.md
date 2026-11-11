# Proposal: Functional Interaction Pipeline

Status: architectural draft for discussion, not an implementation specification.

This proposal follows the discussion of `Interactive`, `Behavior`, immutable
`Gesture` records, and `GestureHandler`. It keeps
[the detailed general-handling proposal](./proposal-interaction-01-general.md)
intact. The changes from that proposal and
[the architecture review](./review-interaction-architecture.md) are explicit in
section 8. Recommendations below are not yet approved decisions.

## 1. Scope

Define one interaction pipeline usable by both:

- Headless input behaviors and widgets composed in patches.
- Canvas navigation, selection, and individual or group manipulation.

The central distinction is:

> A gesture describes an interaction. A gesture handler interprets input.
> A behavior gives the interaction meaning. Interactive coordinates delivery.

A gesture handler interprets notifications directly. Mouse/touch normalization
is part of that handler's recognition logic, not a required stage before it:

```text
notification -> gesture handler -> gesture
gesture      -> gesture handler -> gesture   (when composition is useful)
```

There is no requirement for an intermediate pointer gesture, a separate
notification-normalization handler, a public coordinator node, an event bus,
or a generic reactive framework.

This document defines responsibilities, state ownership, processing order, and
the remaining choices. It does not rename existing classes, introduce source
code, or settle the complete public C#/VL API. Code below is pseudocode.

## 2. Types and responsibilities

Names and suggested locations are provisional.

| Type | Kind | Tier | Suggested file | Single responsibility |
|---|---|---|---|---|
| `Interactive<TSelf>` | Abstract node class | Host | `Core\Interaction\Interactive.cs` | Hosts behaviors and provides the tree, geometry, and root notification boundary. |
| `IInteractionBehavior` | Fu-specific interface | Binding | `Core\Interaction\Behaviors\Behavior.cs` | Defines an attached capability's lifecycle and participation in interaction. |
| `Behavior` | Abstract VL process node | Binding | `Core\Interaction\Behaviors\Behavior.cs` | Owns settings, handler execution state, and bindings to patch-owned channels. |
| `Gesture` | Immutable record vocabulary; representation open | Data | `Core\Interaction\Gesture.cs` | Describes an interaction at a particular phase. |
| `GestureHandler` | Processor/reducer; exact contract open | Processing | `Core\Interaction\GestureHandler.cs` | Interprets notifications, or gestures when composed, to produce next state, proposed outputs, and an ownership decision. |
| Click/drag/pinch handlers | Internal processors | Recognition | `Core\Interaction\Handlers\` | Handle mouse/touch notifications directly and recognize interaction families without knowing channels, selection, or rendering. |
| Root interaction session | Private root state, optionally a helper class | Coordination | Inside `Interactive.cs`, or a later extracted helper | Tracks candidates, capture, routing context, and cancellation across nodes. |
| Selection/canvas providers | VL behavior nodes | Shared policy | `Core\Interaction\Behaviors\` | Coordinate selection/group operations or view manipulation within a subtree. |

Declare `IInteractionBehavior` and `Behavior` together in `Behavior.cs`.
Do not introduce a second interface named `IBehavior` to shadow
`VL.Skia.IBehavior`. Use the distinct Fu name for interaction-specific members;
the existing VL contract remains unchanged.

`VL.Skia.IBehavior` exposes `Notify(notification, caller) -> bool`. That outer
integration boundary cannot by itself express pending recognition, proposed
gesture output, and arbitration. See the current
[interaction base](../../VL.Fu/src/Core/Interaction/Interactable.cs).

### No intermediate pointer representation is required

```text
Mouse notification --+
                     +--> DragGestureHandler --> DragGesture
Touch notification --+
```

`DragGestureHandler` can switch on the notification type and translate each
device's input into its own press/move/release/cancel transitions. Mouse and
touch branches reuse the same drag state machine; they do not need to publish
a `PointerGesture` or construct a `PointerEvent` first.

Pointer identity, buttons, and positions are still necessary information, but
not additional pipeline participants. Small helpers may share extraction code
where needed without introducing an intermediate stream or event type.

## 3. State and lifecycle

### 3.1 Ownership

| Owner | State | Idle value |
|---|---|---|
| Each `Interactive` | Attached behaviors | Empty internal `Spread<IInteractionBehavior>`, exposed as `IReadOnlyList<IInteractionBehavior>`. |
| Root session | Pending candidates, per-pointer capture, frozen mappings, hovered path | Empty collections; no active interaction. |
| Behavior | Owner, enabled setting, channel bindings | No owner, enabled, channels unconnected. |
| Behavior's handler execution | Recognition history for that attachment | Handler-specific idle state; no tracked pointer. |
| Manipulation behavior/provider | Values and coordinate mappings captured for an operation | No active snapshot. |
| Patch/application | Value, position, transform, selection flags, view | Defined by the patch, not reset by the input pipeline. |

A pure handler definition can be shared. Its execution state cannot be shared
between unrelated behavior attachments. One behavior instance has one owner.

For multi-touch, a handler execution can track several pointers belonging to
one interaction. It is not necessarily one independent recognizer per finger.

### 3.2 Snapshot versus occurrence

A gesture record can describe the current state of a recognized interaction:

```text
DragGesture(phase, startInParent, positionInParent, offsetInParent)
```

A handler may also need private history that does not belong in that output:
candidate status, participating pointer IDs, thresholds captured for this
interaction, or click-count bookkeeping.

The record can serve as both stored state and output when that is sufficient.
Do not force every handler's private history into every gesture payload.

An output is an occurrence in an ordered stream. Record equality must not
silence two distinct clicks with equal payloads. Conversely, reading the same
stored snapshot every frame must not emit the same click again.

Completion and cancellation are explicit outputs, not permanent states that
the patch must happen to poll before they disappear.

### 3.3 Example drag lifecycle

This table describes one handler execution. `Proposed` means not yet published
to behavior/application outputs.

| State | Input | Next state | Effect |
|---|---|---|---|
| Idle | Eligible press | Pending | Save pointer identity and press position; register as a candidate. |
| Pending | Move below threshold | Pending | Keep observing; no drag output. |
| Pending | Move at/above threshold | Active if accepted | Request ownership and propose `Started`. |
| Pending | Release before threshold | Idle | Stop candidacy; no drag completion. |
| Pending | Ownership denied or cancellation | Idle | Discard proposals and clear recognition history. |
| Active | Move | Active | Propose `Updated` with total offset since press. |
| Active | Release | Idle | Propose `Completed` including the final position and offset. |
| Active | Cancellation | Idle | Emit `Cancelled` through cancellation handling; never emit successful completion. |

The coordinator must resolve a claim before publishing `Started`. A losing
handler cannot leave its proposed active state installed.

State-only feedback such as `IsPressed` may be published while pending. This
does not grant capture or mean that a click succeeded. Selection-on-press is
also a distinct policy, not an early successful click.

### 3.4 Cancellation and mutation

The root coordinates cancellation when an owner is hidden, detached, disposed,
or made non-interactive; when a behavior is removed/disabled; and on input loss
or Escape. The same cancellation route clears candidates, capture, handler
history, and active feedback.

- Cancellation must work without another mouse move and without rendering.
- Disabling cancels an execution but leaves the enabled setting false.
- Detaching clears the owner but does not dispose a patch-owned behavior.
- Channels belong to the patch and are never disposed by a behavior.
- Replacing a channel binding cancels the active operation before releasing
  the old binding, so the old output is not stranded in an active state.
- Repeated setters and unchanged behavior lists must preserve active state.
- Proposed default: snapshot recognition settings at press; changed settings
  apply to the next interaction. Disabling takes effect immediately.

For manipulations, retain the previous review's default: cancellation restores
the operation's captured application values. A headless drag only resets its
own published facts; it cannot undo arbitrary work performed by a patch.
External writes during manipulation require an explicit policy; see Q7.

## 4. Pipeline and orchestration

### 4.1 Functional handler shape

Pseudocode, not a proposed public tuple or delegate signature:

```text
Handle(previousState, input, context, settings)
    -> nextState
     + zero or more proposed output gestures
     + participation/ownership decision
```

A notification-driven handler exposes a `ProcessNotification` entry point.
Its device dispatch can be as direct as this schematic switch:

```text
DragGestureHandler.ProcessNotification(notification):
    mouse notification -> ProcessMouse(notification)
    touch notification -> ProcessTouch(notification)
    other notification -> unchanged state, no output
```

`ProcessMouse` and `ProcessTouch` interpret their notification kinds and call
the same internal drag transitions. State, geometry context, and settings are
explicit inputs to a reducer, or held by the execution instance if Q3 chooses
that API. The switch does not imply a stateless one-argument recognizer.

A handler can also process an existing gesture when composition needs it.
That is an extension of the model, not an intermediate stage every notification
must traverse. A shared generic handler base/interface is only justified if
the chosen public composition API needs one.

Handlers must not write channels, mutate nodes, or decide which other nodes
lose. Those effects would happen too early to arbitrate reliably.

### 4.2 Processing order

```text
Root receives notification
    |
    v
Root: route by hit path or existing pointer session
    |
    +--> click handler.ProcessNotification --+
    +--> drag handler.ProcessNotification ---+--> resolve ownership
    +--> pinch handler.ProcessNotification --+          |
                                                       v
                                                accepted gestures
                                                       |
                                                       v
                                                behaviors/providers
                                                       |
                                                       v
                                              patch-owned channels
```

1. Inspect the original notification for the identity, position, and lifecycle
   information needed to route it. Handle root-level cancellation notifications.
2. On press, collect eligible handlers along the front-most hit path.
3. Keep pending candidates until release, rejection, or cancellation. A move
   outside the original bounds must not silently strand a pending handler.
4. Pass the original notification and the relevant geometry context to each
   participating handler. Each handler interprets the input it supports.
5. Resolve claims across the affected candidates before publishing effects.
   Cancel losers and discard their proposed outputs.
6. Publish accepted updates, including permitted pending feedback, to behaviors.
7. Release completed/cancelled sessions and update hover as required.

There is no root conversion into a second event object and no per-child
notification wrapper. Each participating handler does its own notification
dispatch. That repeats a small amount of input inspection; it is an explicit
trade-off for a simpler pipeline, not a claim that decoding happens only once.

Routing and handlers can reuse field-extraction helpers. Coordinate mapping
remains target-specific and is supplied as context alongside the unchanged
notification. It is not a reason to require a pointer-gesture stage.

Capture routes subsequent input to the owning node even outside its bounds.
Handlers on that node can cooperate, including a drag-to-pinch transition.
Capture remains per pointer; one pointer's capture must not redirect unrelated
pointers to that node.

`Interactive` owns coordination conceptually. A private helper may hold the
root's state without adding another public VL node. Child `Interactive`s do
not start competing root sessions for forwarded input.

### 4.3 Geometry, hover, and ordering

Use the existing rendering geometry and reverse draw order for targeting.
The [render path](../../VL.Fu/src/Core/Rendering/Renderable.cs) already applies
local transformations and clips children independently of its own layer.

- Window positions and DIP thresholds describe physical input movement.
- Node-local positions support hit testing.
- Frozen parent/reference-space positions support manipulation without
  feedback as the manipulated node moves.
- Shared group operations map through a common provider space.
- Non-invertible transforms cannot produce fabricated coordinates; targeting
  skips them and affected active operations are cancelled explicitly.

Hover remains a routing fact, not a click/drag competitor. It can reach a
behavior through the same delivery machinery without requiring a recognizer.

The execution model is ordered, synchronous processing on the UI/main thread,
not per-frame sampling of only the latest notification. The detailed proposal
records the existing renderer investigation; revalidate its frame/thread
assumptions against the target runtime during implementation.

Synchronous channel subscribers may mutate the tree or disable a behavior.
Publication must therefore revalidate recipients and process invalidation
before delivering further queued updates to them. Never iterate a mutable
live behavior list while publishing callbacks.

### 4.4 Both usage styles

```text
Headless/widget:
    Drag handler -> Draggable -> offset channel -> patch-defined slider
    Click handler -> Clickable -> activation channel -> patch-defined button

Canvas/selection:
    Drag handler -> Movable -> individual or provider-coordinated movement
    Press/click handler -> Selectable -> selection policy
    Drag/wheel/pinch handlers -> CanvasProvider -> view channel
```

`Movable` reuses drag recognition, not a separate `Draggable` behavior node.
Providers do not create another input pipeline. A selection provider and a
canvas provider remain independent: selectable lists need no pan/zoom, and
pan/zoom views need no selection.

Application values live in patch-owned channels. Manipulators write those
channels rather than directly calling node setters that the patch would
overwrite on its next frame.

## 5. End-to-end traces

### 5.1 Child click versus ancestor drag

| Step | Input | Who sees it | State change | Channel writes |
|---|---|---|---|---|
| 1 | Mouse down on child | Root routing, then child click and ancestor drag handlers receive the notification directly | Both become pending; no capture | Child `IsPressed = true`. |
| 2 | Small move | Both candidates | Remain pending | None. |
| 3 | Move crosses drag threshold | Both candidates, then root resolver | Drag accepted; child click cancelled; pointer captured to ancestor | Child `IsPressed = false`; ancestor `IsDragging = true`, start event, initial offset. |
| 4 | Move outside ancestor | Captured ancestor handlers | Active drag continues | Updated offset. |
| 5 | Mouse up | Captured ancestor handlers | Drag completes; session returns to idle | Final offset, `IsDragging = false`, successful end event; no click. |

### 5.2 Touch-driven manipulation cancelled by detach

| Step | Input | Who sees it | State change | Channel writes |
|---|---|---|---|---|
| 1 | Touch down | Root routing and the touch branch of the same drag handler used for mouse | Pending; operation records its baseline according to manipulation policy | Optional pressed feedback. |
| 2 | Touch move crosses threshold | Item drag handler and root resolver | Drag accepted and captured | Position changes through `Movable` or its selection provider. |
| 3 | Item detached, with no new touch event | Tree lifecycle hook and old root session | Cancel active operation, release capture, clear handler state | Restore captured position; clear active feedback; no successful completion. |
| 4 | Later touch release | Root input boundary | No valid old candidate remains | No stale position or completion writes. |

### 5.3 Press an unselected item and drag

| Step | Input | Who sees it | State change | Channel writes |
|---|---|---|---|---|
| 1 | Press on item | Item selection handler/behavior and drag handler | Select-on-press policy runs without claiming drag ownership; drag is pending | Item selection flags change through selection policy. |
| 2 | Movement crosses threshold | Drag handler, root resolver, `Movable` | Accepted drag begins a provider-coordinated operation | Provider applies movement to the selected group. |
| 3 | Release | Active handlers and provider | Operation completes | Final positions; active feedback cleared. |

Selecting on press is deliberate behavior policy. It must not be inferred
from a `ClickCompleted` output or delayed until drag recognition has finished.

## 6. Proposed tests

These are acceptance cases for a later implementation, not tests added by this
documentation change. Handler and routing tests should use plain data and fake
targets/sinks, without a running VL patch.

| Area | Focused test |
|---|---|
| Notification handling | `ProcessNotification_EquivalentMouseAndTouchSequences_ProducesEquivalentDragPhases` |
| Routing | `Notify_CompetingCandidates_DeliversOriginalNotificationToBothHandlers` |
| Drag | `Handle_MoveBelowThreshold_RemainsPending` |
| Drag | `Handle_MoveAtThreshold_RequestsOwnership` |
| Drag | `Handle_ReleaseWithNewPosition_CompletesWithFinalOffset` |
| Output delivery | `Publish_EqualClickPayloads_DeliversBothOccurrences` |
| Arbitration | `Notify_AncestorDragClaims_CancelsChildClickBeforePublishing` |
| Capture | `Notify_PointerLeavesBounds_ContinuesCapturedDrag` |
| Capture | `Notify_UnrelatedPointerPressed_DoesNotInheritOtherPointerCapture` |
| Geometry | `Notify_ZoomedParent_DragUsesFrozenParentSpace` |
| Lifecycle | `Detach_ActiveManipulation_RestoresBaselineAndReleasesCapture` |
| Lifecycle | `SetBehaviors_UnchangedInstances_PreservesPendingState` |
| Reentrancy | `Publish_SubscriberDetachesTarget_DiscardsRemainingTargetUpdates` |
| Selection | `Notify_PressUnselectedItemThenDrag_MovesNewSelection` |

Notification handling needs separate verification against the actual VL
contracts, particularly click counts, cancellation, and touch-to-mouse
promotion. Promotion suppression, if required, is a root input concern so
duplicate input does not reach any handler; it does not require conversion to
another event type. Do not infer platform semantics solely from synthetic tests.

## 7. Open questions and recommendations

### Q1. Record classes or record structs? Must all gestures share a base type?

- **A: A polymorphic `Gesture` record-class hierarchy.** Simple heterogeneous
  composition and pattern matching; new snapshots allocate on hot paths.
- **B: Typed `readonly record struct` payloads.** Can avoid those allocations,
  but cannot inherit the existing abstract record class; interface/object
  transport can introduce boxing.
- **Recommendation:** keep the semantic record model, but choose representation
  after Q2. Do not promise zero allocation with a record-class pipeline.
  Prefer typed value payloads if handlers stay internal and performance is a
  hard requirement.

### Q2. Is handler composition public in VL, or internal to behaviors?

- **A: Behaviors own built-in handlers.** Simple patch UX and fewer public
  types; custom combinations require C# extension points or new behaviors.
- **B: Patches compose handler stages explicitly.** Greater flexibility, but
  requires typed connections, branching, execution-state ownership, and a
  clear way to prevent shared handler instances sharing live state.
- **Recommendation:** begin with A and keep the processing contracts testable.
  Choose B now only if building custom interaction pipelines in patches is a
  primary library feature; it materially changes the public design.

### Q3. Is handler state explicit or hidden inside a handler object?

- **A: Reducer plus explicit state.** Supports deterministic replay and
  independent execution contexts; needs a state/result representation.
- **B: Mutable handler instance.** Smaller initial API; the instance must be
  single-owner and cannot safely be reused across unrelated attachments.
- **Recommendation:** A for the recognition core, with a behavior-owned
  execution wrapper holding the current state. Do not require patchers to
  thread state manually unless Q2 explicitly chooses that experience.

### Q4. Are gesture snapshots public outputs, and do they contain totals or deltas?

- **A: Internal snapshots, existing per-value/event channels on behaviors.**
  Fits the reviewed patch-facing design; less access to generic gesture data.
- **B: Public typed gesture occurrences, optionally with a current snapshot.**
  Supports functional composition but needs an explicit delivery contract;
  polling a snapshot alone loses multiple transitions between frames.
- **Recommendation:** retain channel facades initially, with internal typed
  snapshots carrying phase and totals since start. Consumers can derive deltas.
  If B is desired, define occurrence delivery before adding a `State` pin.

### Q5. Is arbitration fixed or configured by the pipeline?

- **A: Preserve the reviewed policy.** Pinch supersedes drag, drag supersedes
  click; same-kind handlers on the same node may cooperate; wheel is separate.
  Predictable initial behavior, but limited custom gesture families.
- **B: Configurable conflict/coexistence rules.** More extensible, but increases
  configuration and requires deterministic handling of contradictory rules.
- **Recommendation:** A initially, with policy owned by the root and not
  encoded in handler list order. Do not treat every accepted output as an
  exclusive claim: hover and pending press feedback are examples.

### Q6. Rename the host now, and keep a compatibility alias?

- **A: Rename `Interactable`/`IInteractable` to `Interactive`/`IInteractive`.**
  Matches this model but changes C# references and potentially imported nodes.
- **B: Keep existing names or provide compatibility wrappers.** Less immediate
  disruption, but wrappers add API surface and may complicate VL imports.
- **Recommendation:** use `Interactive` as the design name; perform the actual
  rename only after checking C# callers and agreeing how existing patches are
  migrated. This proposal does not read or modify `.vl` files.

### Q7. Who wins if the patch writes an application value during manipulation?

- **A: The interaction owns the value until completion/cancellation.** Simple
  snapshot-and-restore behavior, but can overwrite a newer external edit.
- **B: An external edit cancels the operation, and the external value wins.**
  Avoids stale restoration over the edit, but requires reliable write-origin
  identification and coordination across a manipulated group.
- **Recommendation:** B for externally editable channels, subject to verifying
  the channel API. Normal cancellation still restores the baseline when no
  external edit superseded it. This is a proposed qualification of the older
  unconditional restore rule, not an already-settled behavior.

**Public API blockers:** Q1-Q4. Q5 can retain the previous policy for a first
implementation; Q6 is a migration decision; Q7 must be settled before writable
manipulators are implemented.

## 8. Explicit differences from the existing proposals

1. **`Gesture` becomes data, not the recognizer.** The detailed proposal's
   `IGesture.Process` responsibility moves to a handler. The existing
   [empty record](../../VL.Fu/src/Core/Interaction/Gesture.cs) fits the data
   terminology, although Q1 may change its representation.
2. **Handlers interpret notifications directly.** `DragGestureHandler`, for
   example, handles both mouse and touch notifications itself. There is no
   required normalization handler or intermediate `PointerEvent`/`PointerGesture`
   stream. Shared extraction helpers are an implementation detail.
3. **Coordination is a root responsibility, not an additional public concept.**
   A private arbiter/session helper remains valid; there is no requirement to
   put all its implementation into the base class.
4. **Functional transition results replace reading mutable recognizer fields**
   if Q3's recommendation is accepted. Typed results avoid a universal result
   object containing unrelated click, drag, pinch, and wheel fields.
5. **The review removed gesture records and public gesture streams.** This
   draft deliberately reopens immutable records as internal pipeline data;
   public gesture outputs remain unresolved in Q4.
6. **A blanket zero-allocation claim is not retained.** Record representation,
   result delivery, and pipeline composition must be chosen and measured.
7. **External-write cancellation is reopened explicitly in Q7.** Other reviewed
   interaction policies remain the baseline unless individually changed.
8. **The Fu behavior contract is `IInteractionBehavior`.** It is declared with
   `Behavior` in `Behavior.cs`, not in a separate `IBehavior.cs` file and not as
   a replacement for the existing VL interface.

## 9. Later

After resolving the public API questions:

1. Specify the smallest click/drag/hover implementation and its Fu behavior
   contract, including what `Notify` reports to the outer VL.Skia layer.
2. Verify direct notification handling, click-count handling, and touch promotion
   against the actual runtime; avoid double activation from promoted input.
3. Implement and test routing, pending sessions, capture, and cancellation
   before adding application-value manipulation.
4. Add canvas and selection policies on the same handlers and root session.

Deferred: keyboard focus, long-press timers, arbitrary user-defined handler
graphs unless Q2 requires them, snapping, undo history, and public replay APIs.
No new widget drawing or `.vl` changes are part of this proposal.
