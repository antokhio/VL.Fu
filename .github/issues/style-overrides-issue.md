# Style overrides: Yoga specificity fights "later instruction wins"

## Summary

Fu's style chain promises that the **later instruction wins**: inputs run depth first, left to right, then the style itself, all written onto one staging node (`YogaStyleStage`).

Yoga doesn't resolve its multi-slot styles by write order. It resolves them by **specificity**. A more specific slot written earlier silently beats a broader slot written later, so the patch result depends on which slot was used, not on the order of the nodes.

The edge styles have a workaround, but it is lossy around `Start`/`End`. `Gap` has no workaround yet. The phase 3 insets (`Position`) will hit the same problem.

## How Yoga resolves

From Yoga's `Style.h`:

* **Horizontal edges.** The left edge resolves `Start` (LTR) or `End` (RTL), then `Left`, then `Horizontal`, then `All`. The right edge mirrors this.
* **Vertical edges.** The top edge resolves `Top`, then `Vertical`, then `All`. The bottom edge mirrors this.
* **Gap.** A gutter resolves `Row` or `Column`, then `All`.
* **Unset.** A slot holding `StyleLength::points(NaN)` counts as unset, so writing NaN clears it.

Example of the conflict:

```
SetPadding (Edge = Left, 10)  ->  SetPadding (Uniform, 4)
expected (later wins): L 4, R 4, T 4, B 4
Yoga without clearing:  L 10, R 4, T 4, B 4
```

## Current state

### Edges: padding, margin, border

These are fixed by clearing, with one known loss.

`YogaEdges.Set` writes the slot, then clears the more specific slots it covers:

| Written slot | Clears |
|---|---|
| `All` | every other slot |
| `Horizontal` | `Left`, `Right`, `Start`, `End` |
| `Vertical` | `Top`, `Bottom` |
| `Left` / `Right` | `Start`, `End` |
| `Top`, `Bottom`, `Start`, `End` | nothing |

**Known loss.** While styling, the layout direction is unknown: it comes from the root's `CalculateLayout` and any `SetDirection` inherited from ancestors. So whether `Start` lands on the left or the right is undecided, and writing `Left` or `Right` clears **both** `Start` and `End`. For example:

```
SetMargin (Edge = Start, 8)  ->  SetMargin (Edge = Right, 2)
expected (LTR): L 8, R 2
actual:         L 0, R 2     // Start was cleared although it lands on the left
```

### Gap

Not fixed. `GapStyle.Build` writes `node->SetGap(Gutter, Value)` without clearing, so `SetRowGap` before `SetGap` (All) still wins over the later All. Its doc comment and `style-instructions.md` (rule "Yoga's own specificity still applies regardless of order") document this as intended. That contradicts the "later wins" rule the edges now follow.

Separately, `SetGap` still turns an unknown gutter into `All` instead of ignoring it with a warning like the other nodes.

### Phase 3 insets (`Position`)

Not written yet. Position uses the same `YGEdge` slots and the same resolution, so it can reuse `YogaEdges` and will inherit the same `Start`/`End` loss.

## Options

1. **Keep clearing, accept the loss** (current). Simple and stateless per record. The loss only shows when logical (`Start`/`End`) and physical (`Left`/`Right`) edges are mixed in one chain.
2. **Resolve per chain.** Accumulate each edge-like style into a small per-node table during the style pass, keeping the order of writes for every slot. Write the final slots once at the end, with knowledge of the node's resolved direction. That needs:
   * the direction available at style time, either from the parent's resolved layout direction of the previous frame or by resolving `SetDirection` from ancestors first;
   * a second stage after `Build`, or records that write into the table instead of the `YGNode*`.

   This loses nothing, but layering and records get more complex.
3. **Normalise to physical edges.** Translate `Start`/`End` into `Left`/`Right` while styling, using the direction known at that point. That needs the same direction availability as option 2. It changes behaviour when the direction flips at runtime: the values no longer follow the direction.
4. **Restrict the mix.** Warn when a chain mixes logical and physical edges on the same axis, and keep option 1's clearing. Users get told instead of silently losing a value.

## To do

- [ ] Decide between options 1–4 for edges.
- [ ] Apply the same rule to `Gap`: clear `Row` and `Column` when `All` is written, update the `GapStyle` docs and the "Yoga's own specificity" line in `style-instructions.md`, and add tests.
- [ ] Make `SetGap` ignore an unknown gutter with a warning instead of falling back to `All`.
- [ ] Use the chosen rule for the phase 3 insets (`SetPosition`/`SetInset`).
- [ ] Tests: mixed logical and physical edges under LTR and RTL, for every edge style and for insets.

## References

* `VL.Fu\src\Core\Styles\YogaEdges.cs` (clearing table and remarks)
* `VL.Fu\src\Core\Styles\GapStyle.cs`
* `VL.Fu\src\Core\Layout\YogaStyleStage.cs`, `Layoutable.cs` (style pass, `CopyStyle`)
* `.github\instructions\style-instructions.md` (rules "later instruction wins" and "Yoga's own specificity", edge rule)
* `VL.Fu.Tests\Styles\StyleNodeTests.cs` (edge tests using computed values)
