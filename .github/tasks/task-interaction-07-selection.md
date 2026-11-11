# Task interaction-07: Selection

Source: [summary-interaction-v2.md](../summaries/summary-interaction-v2.md) §3.1, §4.2, §4.3, §7.1, §14 N7; v1 [summary-interaction.md](../summaries/summary-interaction.md) §2.4 S1–S3, S8, §6.2; [review-interaction-architecture.md](../proposals/review-interaction-architecture.md) "Canvas interaction", C2–C4, C6. Depends on: [task-interaction-06](task-interaction-06-behaviors.md).

## Goal

Items in a container can be selected with the pointer. After this task:
- `Selectable` on an item and `SelectionProvider` on a container are VL process nodes with the N6 shape;
- the left-button rows of the canvas interaction table work, except moving the selection, which is N8:
  - pressing an unselected item selects it;
  - clicking a selected item collapses the selection to it;
  - clicking the background clears the selection;
  - dragging on the background outside the selection draws a marquee;
  - Shift and Ctrl change what a press, a click or a marquee does;
- the provider talks to its items only through `SelectionNotification`, so a patched item can take part;
- the provider writes the selection bounds and the marquee rectangle into channels, in its own node space.

## Design

### 1. Types and files

| Type | Kind | File |
|---|---|---|
| `Selectable` | public sealed class, `NodeBase, IBehavior` | `Behaviors\Selectable.cs` |
| `SelectionProvider` | same | `Behaviors\SelectionProvider.cs` |
| `SelectionMode` | public enum: `Single`, `Multi` | `Behaviors\SelectionProvider.cs` |
| `SelectionNotification` | public sealed class, `NotificationBase` | `Behaviors\SelectionNotification.cs` |
| `SelectionAction` | public enum: `Clear`, `Replace`, `Add`, `Toggle`, `Marquee`, `CollectBounds` | `Behaviors\SelectionNotification.cs` |
| `NotificationExtensions.DispatchInto` | internal extension | `Core\Interaction\NotificationExtensions.cs` |

- The notification lives in `VL.Fu.Behaviors` and not in `Core\Interaction`, because it carries the `SelectionProvider`. Core doesn't depend on behaviors.
- Both behaviors keep the N6 shape: setters are fragments, outputs are channels connected by setters, and swaps compare channels with `ReferenceEquals`. The constructor is a `[Fragment]`. Setters use `PinOrder.Behavior`, and `SetEnabled` uses `PinOrder.Enabled`.

### 2. Scope lookup (S1)

`SelectionProvider.TryFind(item, out provider, out providerNode)` walks up from the item node's **parent** and returns the first ancestor that has a `SelectionProvider` in its `Behaviors`. It is O(depth) and caches nothing.
- The walk starts at the parent, so a node that is both an item and a provider belongs to the provider above it, and its own provider scopes its children.
- A disabled provider still scopes its subtree, so its items do nothing. Disabling a provider turns selection off in its scope without the items falling through to an outer provider.

### 3. `SelectionNotification`

`SelectionNotification(provider, action, target, area, modifiers)`, as in v2 §7.1:

| Member | Meaning |
|---|---|
| `Provider` | The `SelectionProvider` that sent it |
| `Action` | What the items do |
| `Target` | The item node for `Replace`, `Add` and `Toggle`; null otherwise |
| `Area` | The marquee rectangle in the provider's space, for `Marquee` |
| `ModifierKeys` | From the input that caused it. `Marquee` reads them: Shift adds, Ctrl toggles, neither replaces |
| `Concerns(item)` | Whether `item`'s nearest provider is `Provider`. Items of nested providers ignore it (S3) |
| `TryGetItemBounds(out bounds)` | The box of the node receiving it, `(0, 0, Layout.Width, Layout.Height)`, mapped into the provider's space as axis-aligned bounds (S8). False while either space is scaled to zero |
| `IncludeBounds(bounds)` | `CollectBounds` only: adds `bounds` to the union the provider reads |

- The notification carries the provider's window-to-node matrix from the moment it was sent. `TryGetItemBounds` maps item → window through the inverse of the item sender's `WindowToNodeMatrix`, then window → provider through that matrix.
- `WithSender` returns a copy that shares the accumulator. `Transform` returns `this`, like the claim notifications.

| Action | An item it concerns |
|---|---|
| `Clear` | Deselects |
| `Replace` | Selects if it is `Target`, otherwise deselects |
| `Add` | Selects if it is `Target` |
| `Toggle` | Flips if it is `Target` |
| `Marquee` | Contained means `Area` fully contains its bounds (C3). Shift: contained items select. Ctrl: contained items flip. Neither: selects exactly the contained items |
| `CollectBounds` | A query (§4.3): a selected item includes its bounds. Nothing is written |

`Clear`, `Replace`, `Add`, `Toggle` and `Marquee` are fan-outs into the provider's subtree (v2 §3.1). They reach displayed, laid-out nodes, so a hidden item keeps its selection.

### 4. Dispatching from an item

An item decides on press or click, but the notification has to go into its **provider's** subtree, and `Dispatch` only reaches the acting node's subtree. `DispatchInto(notification, node, toDispatch)` is the same queued fan-out, rooted at `node`. `node` must be the acting node or one of its ancestors, otherwise it warns and does nothing. The selection behaviors use it internally; the public surface is unchanged.

### 5. `Selectable`

Pins: `SetIsSelected(IChannel<bool>)` (two-way: the channel is the truth, C6) and `SetEnabled`. It hosts a `ClickGestureRecognizer` for the left button.

| Input | No modifier | Shift | Ctrl |
|---|---|---|---|
| Press, unselected | `Replace` | `Add` | `Toggle` |
| Click, selected at press | `Replace` (collapse) | nothing | `Toggle` (deselect) |

- In `Single` mode, `Add` and `Toggle` on an unselected item become `Replace`. Ctrl-click on a selected item still deselects it.
- The press never claims (S2), so a drag on the same item (N8 `Movable`) wins.
- A click is processed (returns true) whenever the item has an enabled provider, so ancestors don't treat it as a background click. A processed Up cancels the path anyway, which resets the provider's own recognizers.
- On press, the item tells the provider that this pass's press is an item press. When the provider sees the same Down (it bubbles there after the item), it doesn't start its own click or marquee for it. The mark is the pass's original notification, so a mark that the provider never consumed is ignored at the next press.
- **No provider in scope:** a click toggles the item itself. It warns once per behavior.
- A `PointerCancelNotification` for its pointer resets the click. What the press selected stays: selection is a fact, not gesture feedback.
- On a `SelectionNotification` it concerns: it applies the table in §3, writing `IsSelected` with `EnsureValue`.

### 6. `SelectionProvider`

Pins: `SetMode(SelectionMode = Multi)`, `SetMarquee(bool enabled = true, MouseButtons buttons = Left)`, the output channels `SetSelectionBounds` and `SetMarqueeRect` (`IChannel<Optional<RectangleF>>`, in the provider's node space), and `SetEnabled`.

Background is everything in the provider's node that isn't an item press: the node itself, and children that aren't `Selectable` or didn't process the input.

| Input on the background | Result |
|---|---|
| Left click | `Clear`, even inside the bounds (C2) |
| Marquee-button drag starting outside `SelectionBounds` | Marquee: claims the pointer as `GestureKind.Drag`. While granted, `MarqueeRect` follows the pointer. On release it dispatches `Marquee` and `MarqueeRect` goes back to no value |
| Drag starting inside `SelectionBounds` | Nothing in N7. Reserved for the group move (N8) |
| Right click | Nothing (C4) |

- **Single mode:** no marquee. It clears other items only on user interaction. Two items the patch selected stay selected.
- **Bounds:** `SelectionBounds` is the union that `CollectBounds` returns, or no value when nothing is selected. It is refreshed:
  - on every pointer input the provider receives, before deciding inside or outside on a press;
  - after each `SelectionNotification` it sent, which reaches the provider last, after its items.
  A change the patch makes through an item's channel shows at the next pointer input over the provider.
- The marquee rectangle is measured in the provider's node space through the window-to-node matrix frozen at press (A1).
- **Cancel** (another claim takes the pointer, Esc, device loss, `SetEnabled(false)`, `SetMarquee(false)`, `SetMode(Single)`, swapping the `MarqueeRect` channel): the marquee is dropped. No selection is applied and `MarqueeRect` returns to no value. A cancel from a setter owes the claim release, like `Draggable`.

## Decisions

| # | Decision |
|---|---|
| D1 | Outputs are channels, like N6, since only setters become pins. |
| D2 | The notification is in `VL.Fu.Behaviors`, so core doesn't reference the behavior it carries. |
| D3 | An item's press reaches the provider's subtree through the internal `DispatchInto`, rooted at the provider node the scope walk found. |
| D4 | A press on a selected item acts on release, so "press a selected item and drag" keeps the selection for N8. |
| D5 | The marquee applies once, on release (S3). The rectangle is live; the selection isn't. |
| D6 | A drag inside the bounds does nothing until N8. |
| D7 | Bounds are refreshed on pointer input and after the provider's own changes; there is no per-frame walk. |

## Tests

`VL.Fu.Tests\Behaviors\SelectionTests.cs`, through `BehaviorScene` with the provider on Root and the items Back and Front. `Down`, `Up` and `Move` take modifier keys.
- Press on an unselected item selects it and deselects the other; Shift adds; Ctrl toggles; in Single mode, Shift replaces.
- Click on a selected item collapses the selection; Ctrl-click deselects; Shift-click keeps the selection.
- Press on an unselected item and release: one `Replace`, no collapse.
- Background click clears, also inside the bounds.
- Marquee: it selects only fully contained items; Shift adds; Ctrl toggles; `MarqueeRect` while dragging and none after; Single mode has no marquee; a drag inside the bounds does nothing.
- Cancel: Esc during a marquee clears `MarqueeRect` and selects nothing.
- `SelectionBounds` after selecting one item and two items, and none after a clear.
- Nested provider: an item of an inner provider (Child, with the provider on Front) isn't touched by the outer provider's `Clear`.
- No provider: a click toggles the item.
- Disabled provider: items do nothing.
- A press on an item doesn't start the provider's marquee when dragged.

## As built

- `SelectionProvider.TryFind` (internal, static) is the scope walk; `SelectionNotification.Concerns` is built on it.
- `SelectionProvider.NoteItemPress` and `Send` are the internal calls an item makes. `Send` uses `DispatchInto` with the provider node from the walk.
- The provider remembers an item press as the pass's original notification (`InteractionPass.Notification`), and every press it sees consumes the mark.
- `SelectionNotification`:
  - `IncludeBounds` skips bounds that aren't finite;
  - the accumulator is a private class that `WithSender` copies share;
  - `Contains` is internal; touching edges count as inside.
- The provider writes `SelectionBounds` only when it changes (`EnsureValue`); `Optional<RectangleF>` compares by value.
- `BehaviorScene.Down/Up/Move` take modifier keys, and `Escape()` sends Esc.
- `SelectionTests` covers 19 cases; the suite is 435 green.

## Deferred

- Group move from the bounds or a selected item: N8 (`GroupTransaction`).
- Keys and a selection list: `SelectionProvider<T>`, `Selectable<T>.SetKey(T)` (v1 §11).
- Keyboard selection (Ctrl+A, arrows), focus.
- A live marquee preview of what will be selected.

## Risk

- The bounds query walks the provider's subtree on every pointer input over it. That is fine for UI-sized trees. For a canvas with thousands of items, refreshing only on press and after changes is the fallback.
- `IsSelected` written by the patch mid-press is honored at the next press, not the current one.
