# Interaction: tasks

The design and the phase plan are in [summary-interaction.md](../summaries/summary-interaction.md). Each phase below becomes one `task-interaction-NN-*.md` file when it is cut. A phase is done when the solution builds and `VL.Fu.Tests` passes.

| # | Phase | Task file | Depends on | Status |
|---|---|---|---|---|
| I1 | Plumbing: rename to `Interactive`, `OrderedChildren`, `PointerEvents` style, drop `IBehavior`, delete `Gesture.cs` | | none | |
| I2 | `PointerEvent` | | I1 | |
| I3 | Hit walk | | I1 | |
| I4 | `Behavior` base, `IGestureRecognizer<TState>` | | I1 | |
| I5 | `PointerEventsProvider` | | I2, I3, I4 | |
| I6 | Recognizers | | I4 | |
| I7 | Headless behaviors | | I5, I6 | |
| — | Milestone: headless widgets patched | | I7 | |
| I8 | Selection | | I7 | |
| I9 | `Movable`, `IGroupManipulable` | | I8 | |
| I10 | `Transformable` | | I9 | |
| I11 | `CanvasProvider` | | I7; I9 for the combined trace | |
| I12 | Docs | | I11 | |
