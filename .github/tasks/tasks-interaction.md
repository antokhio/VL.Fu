# Interaction: tasks

The design and the phase plan are in [summary-interaction-v2.md](../summaries/summary-interaction-v2.md); v1 ([summary-interaction.md](../summaries/summary-interaction.md)) is superseded. Each phase below becomes one `task-interaction-NN-*.md` file when it is cut. A phase is done when the solution builds and `VL.Fu.Tests` passes.

| # | Phase | Task file | Depends on | Status |
|---|---|---|---|---|
| N1 | Plumbing: `Interactive`, `OrderedChildren`, `PointerEvents` style, the `Behaviors` pin group | [task-interaction-01-plumbing.md](task-interaction-01-plumbing.md) | none | done |
| N2 | Delivery: `Forward`, `FuNotificationSender`, delivery classes, classification, broadcast/hit/target routes, hit test | [task-interaction-02-delivery.md](task-interaction-02-delivery.md) | N1 | done |
| N3 | Orchestration: `NotificationDispatcher`, `InteractionPass`, outbox, snapshot routes, `Dispatch`/`Query`, `HoverTracker`, touch promotion | [task-interaction-03-orchestration.md](task-interaction-03-orchestration.md) | N2 | done |
| N4 | Claims: `PointerClaims`, requests and resolution, `PointerCaptureNotification`, `PointerCancelNotification`, lifetime rules, the `SetBehaviors` diff | [task-interaction-04-claims.md](task-interaction-04-claims.md) | N3 | done |
| N5 | Recognizers, `PointerInput.TryRead` | [task-interaction-05-recognizers.md](task-interaction-05-recognizers.md) | N1 | done |
| N6 | Headless behaviors: `Hoverable`, `Clickable`, `Draggable`, `Pinchable` | [task-interaction-06-behaviors.md](task-interaction-06-behaviors.md) | N4, N5 | done |
| — | Milestone: headless widgets patched | | N6 | |
| N7 | Selection: `Selectable`, `SelectionProvider`, `SelectionNotification`, bounds query | [task-interaction-07-selection.md](task-interaction-07-selection.md) | N6 | done |
| N8 | `Movable`, `Transformable`, `GroupTransaction`, `GroupManipulationNotification` | [task-interaction-08-move-transform.md](task-interaction-08-move-transform.md) | N7 | done |
| N9 | `CanvasProvider` | [task-interaction-09-canvas.md](task-interaction-09-canvas.md) | N6 | done |
| N9b | Scroll: `Scrollable` behavior writing `IChannel<Vector2>`, wired to render 08's `SetScrollOffset` style; clamped by `ContentArea` | | N9, render 08 | |
| N10 | Docs | | N9 | |
