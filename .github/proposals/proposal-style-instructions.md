# Proposal: Immutable Style Instructions

Status: **draft for discussion**. Alternative to [Style Shape](proposal-style-shape.md), not an implementation plan agreed in advance.

## 1. Model

A style is an immutable instruction: **set these values on a node**, with an explicit operation to **reset the values it owns**.

Three responsibilities:

* `Style`: an immutable record containing instruction values and an optional upstream style.
* `StyleNode`: a mutable VL process node that caches and outputs instruction snapshots.
* `Stylable<TSelf>`: the execution boundary. On a changed style reference, reset the old chain and apply the new one.

No property registry, property keys, entry dictionary, or resolution/diff engine. No apply-only `FuncStyle`.

## 2. Base types

Types below belong to `Fu.Core.Styling`. The instruction protocol depends only on `NodeBase`, not on Yoga or rendering.

```csharp
public abstract record Style
{
    public Style? Input { get; init; }

    public void Apply(NodeBase node)
    {
        Input?.Apply(node);
        ApplyCore(node);
    }

    public void Remove(NodeBase node)
    {
        RemoveCore(node);
        Input?.Remove(node);
    }

    protected abstract void ApplyCore(NodeBase node);
    protected abstract void RemoveCore(NodeBase node);
}

public abstract class StyleNode
{
    private Style? _input;
    private Style? _output;

    public Style Output => _output
        ?? throw new InvalidOperationException("The concrete style node must initialize its output.");

    public void SetInput(Style? input)
    {
        if (ReferenceEquals(_input, input))
            return;

        _input = input;
        RebuildStyle();
    }

    protected bool SetValue<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        return true;
    }

    protected void RebuildStyle() => _output = BuildStyle() with { Input = _input };
    protected abstract Style BuildStyle();
}
```

`BuildStyle` constructs only the node's own instruction from its cached fields. `RebuildStyle` centrally links the upstream input using `with`. Linking belongs to `StyleNode`, not `Stylable`, because it happens while constructing the shared snapshot, not while executing it on a target.

The `with` expression creates a linked record copy only during a rebuild. Neither construction nor linking executes against a target. Output instructions contain value snapshots, not actions reading live fields; unchanged inputs still keep the cached output.

The concrete constructor calls `RebuildStyle` after its fields are initialized. The base constructor never invokes an abstract member. There is no `Update`; reading `Output` never builds anything.

Only the comparison helper is generic, not the class. `SetValue` updates one field and reports whether it changed. A multi-input setter evaluates every field, then calls `RebuildStyle` once if any changed. No cache-state record is needed. `EqualityComparer<T>.Default` compares optional presence and the contained value, so repeated empty optionals or equal values keep the same output reference.

## 3. Execution and removal

Inside `Stylable<TSelf>`:

```csharp
private Style? _style;
public Style? Style => _style;

public void SetStyle(Style? style)
{
    if (ReferenceEquals(_style, style))
        return;

    _style?.Remove(this);
    style?.Apply(this);
    _style = style;
}
```

Apply runs upstream first: **later assignments to the same slot win**. Yoga's edge-specificity rules remain unchanged: `Margin.Left` and `Margin.All` are different slots. Reset runs in the opposite order. Run the whole transition before layout/render, not a layout pass between reset and replay.

| Change | Result |
|---|---|
| `SetWidth(10) -> SetWidth(5)` | Width is 5. |
| Remove the second setter | Reset the old chain, apply the remaining chain: width is 10. |
| Remove the only width setter | Its reset restores the Yoga default. Other remaining instructions are replayed. |
| Disconnect the chain | Reset all old instructions; apply nothing. |
| Share one output between nodes | Each target applies/resets independently; the record has no target-specific state. |

**`Remove` resets; it is not a captured undo operation.** It restores a declared default, not whatever happened to be on the target before applying. Capturing previous node values inside a shared style would break reuse between targets.

Direct setter pins and styles can address the same value, but there is no priority arbitration: see the Transform example in section 5. Component defaults are ordinary style instructions placed before overrides.

## 4. One concrete setter: width

The record owns the instruction; the process node owns input caching. Yoga-specific code lives in `Fu.Core`, not in the base types.

```csharp
internal sealed record WidthStyle(YGValue Width) : Style
{
    public WidthStyle(Optional<float> width)
        : this(width.HasValue ? YGValue.Point(width.Value) : YGValue.Undefined) { }

    protected override void ApplyCore(NodeBase node) => Write(node, Width);
    protected override void RemoveCore(NodeBase node) => Write(node, YogaDefaults.Width);

    private static unsafe void Write(NodeBase node, YGValue width)
    {
        if (node is not Renderable target)
        {
            node.Log(LogLevel.Warning, "Width style requires a Renderable node.");
            return;
        }

        var handle = target.Handle;
        if (handle is null)
        {
            node.Log(LogLevel.Warning, "Cannot style a disposed Yoga node.");
            return;
        }

        switch (width.Unit)
        {
            case YGUnit.Point: handle->SetWidth(width.Value); break;
            case YGUnit.Percent: handle->SetWidthPercent(width.Value); break;
            case YGUnit.Auto: handle->SetWidthAuto(); break;
            case YGUnit.Undefined: handle->SetWidth(float.NaN); break;
            default:
                throw new ArgumentOutOfRangeException(nameof(width), width.Unit, "Unsupported width unit.");
        }
    }
}

[ProcessNode(Name = "SetWidth")]
public sealed class SetWidthNode : StyleNode
{
    private Optional<float> _width;

    public SetWidthNode() => RebuildStyle();

    public void SetWidth(Optional<float> width = default)
    {
        if (SetValue(ref _width, width))
            RebuildStyle();
    }

    protected override Style BuildStyle() => new WidthStyle(_width);
}
```

The process pin and its cache use `VL.Core.Optional<float>`, not a NaN sentinel. Conversion belongs to the instruction: an empty optional produces an **explicit undefined width**, ultimately written as NaN to Yoga. It does not pass the input through: `SetWidth(10) -> SetWidth(empty)` overwrites 10. Removing the second setter restores 10 through replay.

`SetWidthPercent` and `SetWidthAuto` would construct the same `WidthStyle`, using `YGValue.Percent(value)` and `YGValue.Auto`. Percent pins likewise use `Optional<float>` and convert empty to undefined inside the instruction. They compete by execution order, not by keys.

Two implementation details, not new framework abstractions:

* `YogaDefaults.Width` is a proposed cached `YGValue` read from a pristine `YGNode` created with `YogaConfigCache.Acquire(YogaConfig.Default)`. Dispose the temporary node and balance the config lease with `Release`. Retain the copied value, including its unit, not a persistent native template.
* The existing `Layoutable.Handle` needs `protected internal` access for the Yoga instructions in `Fu.Core`. External instructions use their target's supported APIs, not an exposed public native handle.

Required imports: `Fu.Core.Base`, `Fu.Core.Layout`, `Fu.Core.Rendering`, `Microsoft.Extensions.Logging`, `VL.Core`, `VL.Core.Import`, and `YogaSharp`.

## 5. A non-Yoga setter: Transform

This instruction uses the existing `SetTransformation` API, which also updates the node's local rendering transformation. It does not write a field directly. Empty means an explicit identity transform; removal also resets to identity.

```csharp
internal sealed record TransformStyle(Optional<Matrix> Transform) : Style
{
    protected override void ApplyCore(NodeBase node)
        => Write(node, Transform.TryGetValue(Matrix.Identity));

    protected override void RemoveCore(NodeBase node)
        => Write(node, Matrix.Identity);

    private static void Write(NodeBase node, Matrix transform)
    {
        if (node is not Renderable target)
        {
            node.Log(LogLevel.Warning, "Transform style requires a Renderable node.");
            return;
        }

        target.SetTransformation(transform);
    }
}

[ProcessNode(Name = "SetTransform")]
public sealed class SetTransformNode : StyleNode
{
    private Optional<Matrix> _transform;

    public SetTransformNode() => RebuildStyle();

    public void SetTransform(Optional<Matrix> transform = default)
    {
        if (SetValue(ref _transform, transform))
            RebuildStyle();
    }

    protected override Style BuildStyle() => new TransformStyle(_transform);
}
```

Add `Stride.Core.Mathematics` to the imports. The process node caches the optional matrix exactly like the width setter; the output contains a value snapshot.

### Style versus the direct Transformation pin

**The last actual write wins, not the last `SetStyle` call.** Styles apply only on reference changes; they do not continuously enforce their values. A direct pin calling `SetTransformation` every frame keeps writing even when the style is cached.

For a chain containing only a Transform instruction, let `S` set matrix A and a new style reference `S2` set matrix C:

| Calls | Result |
|---|---|
| Install `SetStyle(S)` | A. |
| With S already installed: `SetTransformation(B); SetStyle(S)` | B; the cached style call does nothing. |
| `SetTransformation(B); SetStyle(S2)` with S2 not yet installed | C; the changed style resets and replays after the pin write. |
| `SetStyle(S2); SetTransformation(B)` | B; the direct write runs last. |
| Disconnect via `SetStyle(null)` | Identity, not the last direct-pin value B. |
| `SetStyle(null); SetTransformation(B)` | B. |

Changing another instruction in a chain also replays its Transform instruction, so it can overwrite a direct pin again. Removal never remembers or restores a pin's earlier value. This is the explicit drill of reset/replay with caching, not a promise of fixed pin-versus-style precedence. Stable priority would need a separate ownership rule; it is outside this proposal.

## 6. Gap: multiple inputs, separate process nodes

`SetGap(value, gutter)` caches two fields without a cache-state record. `SetRowGap(value)` and `SetColumnGap(value)` are separate process nodes that fix the gutter. They share one small cache implementation and produce the same immutable instruction type.

```csharp
internal sealed record GapStyle(float Value, YGGutter Gutter) : Style
{
    public GapStyle(Optional<float> value, YGGutter gutter)
        : this(value.TryGetValue(float.NaN), gutter) { }

    protected override void ApplyCore(NodeBase node) => Write(node, Value, Gutter);
    protected override void RemoveCore(NodeBase node) => Write(node, float.NaN, Gutter);

    private static unsafe void Write(NodeBase node, float value, YGGutter gutter)
    {
        if (node is not Renderable target)
        {
            node.Log(LogLevel.Warning, "Gap style requires a Renderable node.");
            return;
        }

        var handle = target.Handle;
        if (handle is null)
        {
            node.Log(LogLevel.Warning, "Cannot style a disposed Yoga node.");
            return;
        }

        handle->SetGap(gutter, value);
    }
}

public abstract class GapStyleNode : StyleNode
{
    private Optional<float> _value;
    private YGGutter _gutter;

    protected GapStyleNode(YGGutter gutter) => _gutter = gutter;

    protected void SetGapValues(Optional<float> value, YGGutter gutter)
    {
        if (gutter is not (YGGutter.All or YGGutter.Row or YGGutter.Column))
            throw new ArgumentOutOfRangeException(nameof(gutter));

        var changed = SetValue(ref _value, value);
        changed |= SetValue(ref _gutter, gutter);
        if (changed)
            RebuildStyle();
    }

    protected sealed override Style BuildStyle()
        => new GapStyle(_value, _gutter);
}

[ProcessNode(Name = "SetGap")]
public sealed class SetGapNode : GapStyleNode
{
    public SetGapNode() : base(YGGutter.All) => RebuildStyle();

    public void SetGap(Optional<float> value = default, YGGutter gutter = YGGutter.All)
        => SetGapValues(value, gutter);
}

[ProcessNode(Name = "SetRowGap")]
public sealed class SetRowGapNode : GapStyleNode
{
    public SetRowGapNode() : base(YGGutter.Row) => RebuildStyle();

    public void SetRowGap(Optional<float> value = default)
        => SetGapValues(value, YGGutter.Row);
}

[ProcessNode(Name = "SetColumnGap")]
public sealed class SetColumnGapNode : GapStyleNode
{
    public SetColumnGapNode() : base(YGGutter.Column) => RebuildStyle();

    public void SetColumnGap(Optional<float> value = default)
        => SetGapValues(value, YGGutter.Column);
}
```

Only the output instruction `GapStyle` is a record. Process nodes retain ordinary fields. The `|=` deliberately evaluates both comparisons: changing the value and gutter in one call updates both fields and builds one output, not two. Repeating the same arguments builds nothing.

Changing `SetGap`'s gutter builds a new snapshot. When the target consumes it, reset/replay clears the old gutter before applying the new one; the process node itself does not execute either operation. Empty values write undefined internally; removal also clears the gutter to undefined, not numeric zero, so Yoga can fall back to `All`. Explicit row/column gutters take precedence over `All`, regardless of chain order; later assignments win within the same gutter slot.

```text
SetGap(10, All) -> SetRowGap(20) -> SetColumnGap(30) -> node
```

Row gap is 20 and column gap is 30. Removing `SetRowGap` leaves row gap falling back to 10; column gap remains 30.

## 7. Extension contract

Another assembly adds a record derived from `Style` and a corresponding process node. The record explicitly implements both operations:

* `Apply` makes deterministic, repeatable assignments.
* `Remove` restores defaults for the same owned values and is safe to repeat.
* Values are immutable snapshots. Records are only shallowly immutable: mutable collections, target references, and callbacks reading changing external state violate this contract.
* A mismatched target is reported through `NodeBase.Log`, not silently ignored.

This is a reversible **assignment protocol**, not unrestricted arbitrary work. Requiring `Remove` makes the responsibility visible; the compiler cannot prove that a custom implementation resets correctly.

Backend defaults include unset state and units, not just numeric values. A getter that reports a resolved numeric fallback is not necessarily suitable for removal: writing zero can suppress a fallback that clearing the slot would preserve. The width example copies a `YGValue`; the gap example explicitly restores undefined.

For future paint styling, store immutable paint parameters in the record. The target owns and disposes its mutable `SKPaint`; the style does not share or dispose it. Resource acquisition, event subscriptions, toggles, and increments are outside this initial protocol.

Yoga can use explicit property resets as above. A whole-Yoga reset is another possible backend strategy, but must happen once at its ownership boundary, not once per width/padding/etc. instruction. It is not needed to introduce another base class now.

## 8. Combine example

Combine joins independent chains in list order: apply left-to-right, remove right-to-left. Later assignments to the same slot win; Yoga's edge/gutter-specificity rules still apply. It is composition, not property merging; shared upstream instructions are not deduplicated.

```csharp
internal sealed record CombinedStyle(Spread<Style> Styles) : Style
{
    protected override void ApplyCore(NodeBase node)
    {
        foreach (var style in Styles)
            style.Apply(node);
    }

    protected override void RemoveCore(NodeBase node)
    {
        for (var i = Styles.Count - 1; i >= 0; i--)
            Styles[i].Remove(node);
    }
}

[ProcessNode(Name = "Combine")]
public sealed class CombineStyleNode
{
    private Spread<Style?> _inputs = Spread<Style?>.Empty;
    private Style? _output;

    public CombineStyleNode() => _output = BuildStyle();

    public Style? Output => _output;

    public void SetStyles(IReadOnlyList<Style?> styles)
    {
        if (styles.Count == _inputs.Count)
        {
            var unchanged = true;
            for (var i = 0; i < styles.Count; i++)
            {
                if (!ReferenceEquals(styles[i], _inputs[i]))
                {
                    unchanged = false;
                    break;
                }
            }

            if (unchanged)
                return;
        }

        _inputs = styles.ToSpread();
        _output = BuildStyle();
    }

    private Style? BuildStyle()
    {
        var styles = _inputs.OfType<Style>().ToSpread();
        return styles.Count switch
        {
            0 => null,
            1 => styles[0],
            _ => new CombinedStyle(styles),
        };
    }
}
```

Add `VL.Lib.Collections` and `System.Linq` to the imports. The facade accepts `IReadOnlyList<Style?>`; internally, `Spread` holds immutable snapshots. Null items are skipped. Zero active items output null; one active item passes through without a wrapper.

The setter compares the item references, not the list reference. A fresh list containing the same references in the same positions keeps the cached output. Mutable caller-owned lists are copied into a `Spread`; an existing immutable `Spread` may be reused. Editing a mutable input list cannot change an older output.

```text
defaults:  SetWidth(10) -> SetTransform(A) --+
user:      SetWidth(20) --------------------+--> Combine [defaults, user] --> node
```

Width is 20 and transform is A. Disconnecting `user` rebuilds Combine with only `defaults`: the target removes the old combination in reverse order, then applies defaults, restoring width 10. Reversing the list to `[user, defaults]` makes width 10 win instead.

## 9. Requirements worth keeping

| Previous requirement | Proposed treatment |
|---|---|
| R1: cached process-node output | Keep. |
| R2: safe removal/disconnection | Keep, through reset/replay. |
| R3: later wins | Keep for assignments to the same backend slot; native specificity remains unchanged. |
| R4: reach any node value | Narrow to explicitly supported, resettable assignments. |
| R5: steady-state reference check | Keep on the target's `SetStyle` call; Combine checks item references. **Drop the guarantee that mutations dirty Yoga only when final values differ.** Reset/replay can dirty unchanged properties temporarily. |
| R6: immutable, shareable outputs | Keep as an authoring contract; no live-field actions. |
| R7: third-party extension | Keep, by adding instruction records; no registration or edits to `Fu.Core`. |

Trade-off: a mutation costs the old resets plus the new applications. This is deliberately simpler than a diff engine. If measurements later justify avoiding replay, property identity/conflict tracking can be reconsidered; a record alone does not provide it.

## 10. Previous approaches

* [VL.Fu StyleNode](https://github.com/antokhio/VL.Fu/blob/feat/v4/measure-fn/VL.Fu/src/Styles/StyleNode.cs) cached an action bound to the mutable process node. Here, the output captures record values instead.
* [VL.Fu setters](https://github.com/antokhio/VL.Fu/blob/feat/v4/measure-fn/VL.Fu/src/Styles/Styles.cs) showed the desired small setter shape, but no reset contract.
* [VL.Flex FlexStyle](https://github.com/antokhio/VL.Flex/blob/master/VL.Flex/src/FlexStyle.cs) already used value records. It applied itself before upstream and had no reset; this proposal reverses apply precedence and adds reset/replay.
