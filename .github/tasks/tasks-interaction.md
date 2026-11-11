# Interaction: tasks

Design comes first, then implementation.

- The decisions are in [review-interaction-architecture.md](../proposals/review-interaction-architecture.md).
- Three proposals turn them into a design at code level. An agent writes each one from a prompt in [`.github/prompts`](../prompts/).
- Each proposal is reviewed before the next one is written.
- Implementation tasks are cut only after all three reviews.

## Order

| # | Step | Prompt | Output | Depends on | Status |
|---|---|---|---|---|---|
| 01 | General: `PointerEvent`, walk, arbiter, contracts | [prompt](../prompts/interaction-01-general.prompt.md) | `proposal-interaction-01-general.md` | none | prompt ready |
| 01r | Review 01 | | `review-interaction-01-general.md` | 01 | |
| 02 | Headless: gestures, `Behavior`, `Hoverable` / `Clickable` / `Draggable` / `Pinchable` | [prompt](../prompts/interaction-02-headless.prompt.md) | `proposal-interaction-02-headless.md` | 01r | prompt ready |
| 02r | Review 02 | | `review-interaction-02-headless.md` | 02 | |
| 03 | Canvas and selection: providers, `IGroupManipulable`, manipulators | [prompt](../prompts/interaction-03-canvas-selection.prompt.md) | `proposal-interaction-03-canvas-selection.md` | 01r, 02r | prompt ready |
| 03r | Review 03 | | `review-interaction-03-canvas-selection.md` | 03 | |
| 04 | Cut the implementation tasks | | `task-interaction-NN-*.md` | 03r | |

The steps run in sequence, not in parallel. Proposal 02 builds on contracts defined in 01:
- `IGesture`;
- how a behavior learns about transitions;
- the spaces a gesture receives.

Proposal 03 builds on both. Run in parallel, each agent would invent its own version of these contracts.

All three prompts share the [rules](../prompts/interaction-proposal-rules.md):
- binding inputs;
- the output structure;
- readable, explicit code;
- no initial state in constructors;
- channel and facade conventions.
