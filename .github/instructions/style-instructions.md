---
applyTo: "Fu.Core/Styles/**/*.cs,Fu.Core/Styling/**/*.cs,VL.Fu/src/Styles/**/*.cs"
---

# Writing styles

A style has two parts:

| Part | Location | Namespace | Role |
|---|---|---|---|
| Record | `Fu.Core\Styles\XStyle.cs` | `Fu.Core.Styles` | Immutable instruction; writes the value into the node. |
| Process node | `VL.Fu\src\Styles\XNodes.cs` | `VL.Fu.Styles` (VL: `Fu.Styles`) | Holds the pin values, outputs a cached record. |

The base types (`Style`, `Style<TNode>`, `YogaStyle`, `StyleNode`) live in `Fu.Core\Styling` and should not change for a new style.

## Pipeline in short

* A style chain is a tree. The pass runs inputs depth first, left to right, then the style itself, so the **later instruction wins**.
* Every pass starts from defaults. Removing a setter falls back to an earlier setter or the default; there is no reset.
* Yoga styles are written onto a shared stage node and copied once, so an unchanged result doesn't dirty layout.
* Yoga's own specificity still applies regardless of order: `Margin.Left` beats `Margin.All`, a row gap beats `All`.

## 1. The record

Pick the base:

* `YogaStyle`: for anything that ends up in the Yoga style; override `unsafe void Build(YGNode* node)`.
* `Style<TNode>`: for values owned by a Fu node type, written through that type's staging API; override `void Build(TNode node)`. Staging methods are `internal` (e.g. `StageTransformation`), so for now these styles must live in `Fu.Core`.

```csharp
namespace Fu.Core.Styles
{
    /// <summary>
    /// Sets the Yoga height. Points, ratio (stored as Yoga percent) and auto write the same slot,
    /// so the later instruction wins, also over the height of a <see cref="SizeStyle"/>.
    /// </summary>
    public sealed record HeightStyle(IReadOnlyList<Style> Inputs, YGValue Height) : YogaStyle(Inputs)
    {
        public override unsafe void Build(YGNode* node) => YogaDimension.SetHeight(node, Height);
    }
}
```

Rules:

* `sealed record`, `IReadOnlyList<Style> Inputs` first, then the values. Never a `Spread` parameter; `Style` copies non-`Spread` lists itself.
* Values are immutable value types (or records), so equal settings give equal records; the base already compares `Inputs` item by item.
* `Build` only makes deterministic assignments. It doesn't read live node state, layout results, children or config, and keeps no reference to the node.
* Store Yoga-native values (`YGValue`, Yoga enums). Facade conversions belong to the node, e.g. ratio via `YogaValues.Ratio`.
* An unsupported value **throws** (e.g. `YogaDimension.EnsureSupported`); the pass logs it on the styled node and skips the instruction.
* A record writing several slots validates everything before writing anything, so it never applies half.
* Shared unit switches go into an internal helper (`YogaDimension`), not copied per record.

## 2. The process node

```csharp
namespace VL.Fu.Styles
{
    /// <summary>
    /// Sets the height in points.
    /// Without a value the height is not set here, so an earlier setter or the default holds.
    /// </summary>
    [ProcessNode(Name = "SetHeight")]
    public sealed class SetHeightNode : StyleNode
    {
        private Optional<float> _height;

        public SetHeightNode([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) => RebuildStyle();

        public void SetHeight(Optional<float> height = default)
        {
            if (SetValue(ref _height, ValidateFinite(height, "Height")))
                RebuildStyle();
        }

        protected override Style? BuildStyle(IReadOnlyList<Style> inputs) =>
            _height.HasValue ? new HeightStyle(inputs, YGValue.Point(_height.Value)) : null;
    }
}
```

Rules:

* `sealed`, derives `StyleNode`, no `Update` method. The `Input` pin group comes from the base.
* The constructor takes the hidden `NodeContext` and ends with `RebuildStyle()`, so the output exists from creation.
* Pin values are `Optional<T>` with `= default`. An empty value means "not set here": `BuildStyle` returns `null` and the node passes its inputs through.
* Setters: `if (SetValue(ref _x, Validate(x))) RebuildStyle();`, so an unchanged value per frame costs nothing and the output keeps its reference.
* Never throw from a node. Correct invalid values and report them with `this.LogWarning(...)`:
  * `ValidateFinite` for `float` and `Vector2` (a bad component drops the whole vector);
  * an unknown enum falls back to a default with a warning.
* Facade conventions: no NaN on pins (use `Optional`); relative sizes are ratios where `1` is the whole parent (`YogaValues.Ratio`), not percent; vectors are Stride `Vector2`.
* Nodes without a value (e.g. `Auto`) have no setter and always return their record.
* Several nodes sharing state go through an abstract `[ProcessNode]` base with a protected `SetXCore` (see `GapStyleNode`); without the attribute on the base, its members are not imported.

### Naming

* `Name = "SetX"` for the plain value, unit variants as `"SetX (Ratio)"`, `"SetX (Auto)"`; class `SetX<Variant>Node`, setter `SetX<Variant>`.
* Related nodes share one file: `XNodes.cs`.
* Don't add Percent variants; the existing `SetWidth (Percent)` is `[Obsolete]`.

## 3. Tests

In `Fu.Core.Tests\Styling\StyleNodeTests.cs` (the project references `VL.Fu`):

* add the node to `Output_FromConstruction_IsBuilt` (`null` for value nodes, the record for valueless ones);
* the value lands in the right slot and leaves the others untouched;
* later wins against an overlapping setter (e.g. `SetWidth` after `SetSize` changes only the width);
* invalid input is ignored with an empty output; an empty value passes the inputs through;
* one layout check for relative units.

Verify with `run_build` and `run_tests` filtered to project `Fu.Core.Tests`.

## Docs

* Multi-line `<summary>`; say what is set and what happens without a value.
* Record new nodes in §10 of `.github\proposals\proposal-style-instructions.md` and in `.github\summaries\summary-core-inheritance.md`.