# Proposal: Immutable Style Instructions

Status: **implemented**, replaces [Style Shape](proposal-style-shape.md). Namespace `VL.Fu.Core.Styling`. Remaining checks in § 10. Individual style nodes beyond the examples come after the base types are in place.

## 1. Model

A style is an immutable **instruction**: write these values into a node's style. A Fu node owns the defaults; every time its style changes it rebuilds its styled values from the defaults, runs the instructions in order and commits only what differs.

| Part | Role |
|---|---|
| `Style` | Immutable record: one instruction plus its inputs. The single pin type in VL. |
| `StyleNode` | VL process node. Caches its values and outputs an instruction snapshot. |
| `Stylable<TSelf>` | Runs a styling pass when the style reference changes. |

No property keys, no diff engine, no `Remove`. **Later wins**: in a chain the downstream setter, among inputs the later one.

## 2. `Style` and its Yoga subset

`Style` is the general instruction: it writes values owned by the Fu node. `YogaStyle` is the subset that writes only Yoga style, so a pass can process it in bulk:

```csharp
/// The pin type in VL and the general instruction.
public abstract record Style(IReadOnlyList<Style> Inputs)
{
    public abstract void Build(NodeBase node);

    // Value equality: Spread compares by reference, so compare Inputs item by item.
    public virtual bool Equals(Style? other);
    public override int GetHashCode();
}

/// A general style for one node type, without the cast in every record.
public abstract record Style<TNode>(IReadOnlyList<Style> Inputs) : Style(Inputs) where TNode : NodeBase
{
    public abstract void Build(TNode node);
    public sealed override void Build(NodeBase node) => Build(node as TNode ?? throw new ArgumentException(
        $"{GetType().Name} requires a {typeof(TNode).Name} node."));
}

/// The Yoga subset. Sees only a native Yoga node, never the Fu node.
public abstract record YogaStyle(IReadOnlyList<Style> Inputs) : Style(Inputs)
{
    public abstract unsafe void Build(YGNode* node);

    // Reached only on a node without Yoga; Layoutable handles YogaStyle before this.
    public sealed override void Build(NodeBase node) => throw new NotSupportedException(
        $"{GetType().Name} requires a layoutable node.");
}

/// Joins inputs without an instruction of its own.
internal sealed record CombinedStyle(IReadOnlyList<Style> Inputs) : Style(Inputs)
{
    public override void Build(NodeBase node) { }
}
```

* **`YogaStyle`** results depend only on the chain, never on the node. Instructions are processed in bulk on a stage node (§ 3), need no access to `Layoutable` internals, and can be tested against a bare `YGNode`. This also allows building a shared chain once and copying the result to every node later (§ 9).
* **`Style` / `Style<TNode>`** write staged values on the Fu node through APIs of the layer that owns them. A node of another type is reported (§ 3) and skipped.
* Other assemblies derive from `Style<TNode>` or `YogaStyle`.
(`SetWidth`, `SetWidth (Ratio)`, `SetWidth (Auto)`, `SetSize`)
* **Combining is a tree.** Every style holds a `Spread` of inputs, and a `Combine` output can be an input of another `Combine` or setter. The pass walks the tree depth first, inputs left to right, then the node itself. Since styles and `Spread`s are immutable, a tree is a value: equal trees compare equal, and one subtree can be shared by many trees.

## 3. Styling pass (`Stylable<TSelf>` and the layers above)

```csharp
// Stylable<TSelf>
private Style? _style;
public Style? Style => _style;

public void SetStyle(Style? style)
{
    if (ReferenceEquals(_style, style))
        return;                                        // steady state: one reference check
    _style = style;

    BeginStyle();                                      // staged values := defaults
    if (style is not null)
        Build(style);
    EndStyle();                                        // commit what differs
}

// Inputs left to right, then the instruction itself.
private void Build(Style style)
{
    foreach (var input in style.Inputs)
        Build(input);

    try
    {
        BuildStyleCore(style);
    }
    catch (Exception e)
    {
        this.LogWarning($"{style.GetType().Name} skipped: {e.Message}");
    }
}

protected virtual void BeginStyle() { }
protected virtual void EndStyle() { }

/// Layers above override this to process their subset (Layoutable: YogaStyle) and pass the rest down.
protected virtual void BuildStyleCore(Style instruction) => instruction.Build(this);
```

`Layoutable<TSelf>` handles Yoga in bulk:

```csharp
protected override void BeginStyle()
{
    base.BeginStyle();
    YogaStyleStage.Reset();                            // stage := template style
    _styledTransformation = Matrix.Identity;
}

protected override void BuildStyleCore(Style instruction)
{
    if (instruction is YogaStyle yoga)
        yoga.Build(YogaStyleStage.Node);
    else
        base.BuildStyleCore(instruction);
}

protected override void EndStyle()
{
    if (_handle is not null)
        _handle->CopyStyle(YogaStyleStage.Node);       // Yoga copies and dirties only if the style differs
    if (_styledTransformation != _committedTransformation)
        SetTransformation(_committedTransformation = _styledTransformation);
    base.EndStyle();
}
```

* **Template**: one pristine `YGNode` created with `YogaConfigCache.Acquire(YogaConfig.Default)` (the default config is never freed), alive for the process. Its style is the Yoga default under `UseWebDefaults`, so no default is written by hand.
* **Stage**: one `YGNode` per thread (`[ThreadStatic]`, created lazily). A pass is synchronous, so nodes share it. Instructions must not call `SetStyle` on another node.
* **Other values** follow the same pattern in the layer that owns them: `BeginStyle` sets the staged field to its default, instructions write it through an `internal` staging method (`StageTransformation(Matrix)`), `EndStyle` commits only if it differs from the last committed styled value.
* Extension node types override `BeginStyle`/`EndStyle` for their own styled values.
* A throwing or unsupported instruction is logged as a warning on the Fu node and skipped; the rest of the pass still runs.

| Change | Result |
|---|---|
| `SetWidth(10) → SetWidth(5)` | 5. |
| Remove the second setter | The stage starts at the defaults, `SetWidth(10)` writes, commit: 10. |
| Remove the only width setter | The stage keeps the default width, commit restores it. |
| Disconnect the chain | Every styled value returns to its default. |
| Change one value | Only Yoga style that actually differs dirties the node; other values commit only on change. |
| One output on many nodes | Each node runs its own pass; records hold no node state. |

## 4. Style nodes

```csharp
[ProcessNode]
public abstract class StyleNode : NodeBase
{
    private Spread<Style> _inputs = Spread<Style>.Empty;
    private Style? _output;

    protected StyleNode(NodeContext nodeContext) : base(nodeContext) { }

    public Style? Output => _output;

    public void SetInput([Pin(PinGroupKind = PinGroupKind.Collection, PinGroupDefaultCount = 1)] Spread<Style> input)
    {
        if (HaveSameItems(_inputs, input))
            return;
        _inputs = input;
        RebuildStyle();
    }

    protected bool SetValue<T>(ref T field, T value);  // EqualityComparer<T>.Default, reports a change

    protected void RebuildStyle()
    {
        var inputs = WithoutNulls(_inputs);
        _output = BuildStyle(inputs) ?? inputs.Count switch
        {
            0 => null,
            1 => inputs[0],
            _ => new CombinedStyle(inputs),
        };
    }

    /// The node's own instruction linked to its inputs, or null when it has nothing set.
    protected abstract Style? BuildStyle(IReadOnlyList<Style> inputs);
}
```

* `[ProcessNode]` on the base, so `SetInput` and `Output` become fragments of every style node. `NodeBase` members are not fragments.
* The input is a pin group: VL pin groups accept only Array, ImmutableArray and `Spread`, so this facade is a `Spread`, an exception to the `IReadOnlyList` rule. Unconnected pins (`null`) are skipped.
* A `Spread` is immutable, so a changed item always arrives as a new reference: `SetInput` checks the reference first. Only if it differs are the items compared, so a spread rebuilt with the same items (if VL rebuilds pin groups every frame; to verify in vvvv) keeps the output stable.
* Concrete constructors call `RebuildStyle()` after their fields are set; a test checks every style node has an output state from construction. No `Update`; reading `Output` builds nothing.
* **Empty `Optional` means not set**: `BuildStyle` returns `null`, the output is the inputs, so an earlier setter or the default holds.
* Invalid pin values are corrected and reported through `NodeBase` logging on the style node.
* `Combine` is a style node whose `BuildStyle` always returns `null`.

## 5. Example: width (Yoga)

```csharp
public sealed record WidthStyle(IReadOnlyList<Style> Inputs, YGValue Width) : YogaStyle(Inputs)
{
    public override unsafe void Build(YGNode* node)
    {
        switch (Width.Unit)
        {
            case YGUnit.Point: node->SetWidth(Width.Value); break;
            case YGUnit.Percent: node->SetWidthPercent(Width.Value); break;
            case YGUnit.Auto: node->SetWidthAuto(); break;
            default: throw new ArgumentOutOfRangeException(nameof(Width), Width.Unit, "Unsupported width unit.");
        }
    }
}

[ProcessNode(Name = "SetWidth")]
public sealed class SetWidthNode : StyleNode
{
    private Optional<float> _width;

    public SetWidthNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
        : base(nodeContext) => RebuildStyle();

    public void SetWidth(Optional<float> width = default)
    {
        if (SetValue(ref _width, width))
            RebuildStyle();
    }

    protected override Style? BuildStyle(IReadOnlyList<Style> inputs)
        => _width.HasValue ? new WidthStyle(inputs, YGValue.Point(_width.Value)) : null;
}
```

`SetWidth (Ratio)` and `SetWidth (Auto)` build the same record with `YogaValues.Ratio(value)` and `YGValue.Auto`.

## 6. Example: transform (node value)

```csharp
public sealed record TransformStyle(IReadOnlyList<Style> Inputs, Matrix Transform) : Style<Renderable>(Inputs)
{
    public override void Build(Renderable node) => node.StageTransformation(Transform);
}
```

* The setter node mirrors `SetWidthNode` with `Optional<Matrix>`.
* Style vs the direct `SetTransformation` pin: the commit writes only when the styled matrix changes, so a direct pin keeps its value until then; disconnecting the style commits identity. Mixing both on one node is not a supported pattern, only a defined one.

## 7. Example: gap (several inputs, one instruction)

```csharp
public sealed record GapStyle(IReadOnlyList<Style> Inputs, YGGutter Gutter, float Value) : YogaStyle(Inputs)
{
    public override unsafe void Build(YGNode* node) => node->SetGap(Gutter, Value);
}
```

* `SetGap(value, gutter)`, `SetRowGap(value)` and `SetColumnGap(value)` share a small `GapStyleNode` base that caches value and gutter and rebuilds once per call when either changed.
* An unknown gutter is corrected to `All` and logged on the style node.
* `SetGap(10, All) → SetRowGap(20) → SetColumnGap(30)` gives row 20, column 30; removing `SetRowGap` makes row fall back to 10.

## 8. Extension contract

* Derive from `Style<TNode>` or `YogaStyle` and add a style node.
* `Build` makes deterministic assignments: no reads of live external state, no resources, events, toggles or increments.
* Records are shallowly immutable; no mutable collections, node references or callbacks inside.
* A `Style<TNode>` writes only through the staging APIs of its node type; that type owns the defaults (`BeginStyle`/`EndStyle`).
* Paint styling later: the record holds immutable paint parameters; the node owns and disposes its `SKPaint`.
* Logging helpers in `VL.Fu.Core.Base`: `LogInfo`, `LogWarning`, `LogError` as extension methods on `NodeBase`, forwarding to `NodeBase.Log`.

## 9. Requirements

| Requirement | Treatment |
|---|---|
| Cached process-node output | Built at construction, rebuilt on an input item or value change. |
| Disconnect-safe | Every pass starts at the defaults. |
| Later wins | Chain order and input order; Yoga specificity unchanged. |
| Reach node values | Through `Style<TNode>` and the owning layer's staging API. |
| Steady state | One reference check per node per frame; a pass dirties Yoga only if the copied style differs. |
| Immutable, shareable outputs | Records; per-node work only in the pass. |
| Extensions | New records and style nodes, no edits to `VL.Fu.Core`. |

Later, if passes show up in measurements: since a `YogaStyle` chain's result is node-independent, a shared chain can be built once per `Style` instance and copied to each node.

## 10. Open and first tests

* Namespace: done, `VL.Fu.Core.Styling` (folder `VL.Fu\src\Core\Styling`).
* VL rebuilds a pin-group `Spread` only when one of its inputs changes (believed, to confirm in vvvv); the item comparison is a fallback either way.
* Implemented. Layout of the code:
  * Base in `VL.Fu\src\Core\Styling` (`VL.Fu.Core.Styling`): `Style`, `Style<TNode>`, `CombinedStyle`, `YogaStyle`, `Stylable`, `StyleNode`; plus `VL.Fu\src\Core\Layout\YogaStyleStage.cs`.
  * Records in `VL.Fu\src\Core\Styles` (`VL.Fu.Core.Styles`): `WidthStyle`, `HeightStyle`, `SizeStyle`, `GapStyle`, `TransformStyle`; helpers `YogaValues` (public) and `YogaDimension` (internal unit switch shared by the size records).
  * Process nodes in `VL.Fu\src\Styles` (`VL.Fu.Styles`, imported to VL as `Fu.Styles`): `Combine`, `SetWidth`, `SetWidth (Ratio)`, `SetWidth (Auto)`, `SetHeight`, `SetHeight (Ratio)`, `SetHeight (Auto)`, `SetSize`, `SetSize (Ratio)`, `SetSize (Auto)`, `SetGap`, `SetRowGap`, `SetColumnGap`, `SetTransform`. `SetWidth (Percent)` is `[Obsolete]`.
* Ratio instead of percent on the facade: `1` is the whole parent. Records stay Yoga-native and store a ratio as `YGUnit.Percent` via `YogaValues.Ratio(ratio)`, which multiplies by `YogaValues.PercentPerRatio` (100).
* `SetSize` takes a `Vector2`; a non-finite component ignores the whole vector with a warning. `SizeStyle` checks both units before writing, so an unsupported unit leaves both axes untouched. `SetSize` and `SetWidth`/`SetHeight` write the same slots, so the later one wins per axis.
  * Tests in `VL.Fu.Tests\Core\Styling` (`StylableTests.cs`) and `VL.Fu.Tests\Styles` (`StyleNodeTests.cs`).
* `Spread` only on the `SetInput` pin group. Records take `IReadOnlyList<Style>` and keep it as an immutable `Spread`: a `Spread` is kept as is, any other list is copied.
* Confirmed by test: `YGNodeCopyStyle` leaves an equal style undirtied, and a fresh node's style equals the template.
* Additions while implementing: `StyleNode.ValidateFinite` turns NaN or infinity into an empty value with a warning on the style node; a `null` `Inputs` becomes empty; `SetInput` compares items by reference.
* `[assembly: ImportAsIs(Category = "Fu", Namespace = "VL.Fu")]` in `VL.Fu\src\Properties\AssemblyInfo.cs` imports the nodes under `Fu` (and `Fu.Styles`).

First tests, all in place:

1. `YGNodeCopyStyle` leaves an equal style undirtied (the bulk path relies on it).
2. Tree combining: `Combine(Combine(a, b), c)` applies a, b, c in order; a shared subtree in two trees applies in both; equal trees compare equal, a changed leaf makes them differ.
3. A new `Spread` with the same items keeps the style node's output reference; a changed item rebuilds it.
4. Removed setter, disconnected chain and empty value fall back to an earlier setter or the default.
5. A throwing instruction is skipped with a warning; the rest of the pass applies.
