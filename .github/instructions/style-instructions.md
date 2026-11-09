---
applyTo: "VL.Fu/src/Core/Styles/**/*.cs,VL.Fu/src/Core/Styling/**/*.cs,VL.Fu/src/Styles/**/*.cs,VL.Fu.Tests/Core/Styling/**/*.cs,VL.Fu.Tests/Styles/**/*.cs"
---

# Writing styles

A style has two parts:

| Part | Location | Namespace | Role |
|---|---|---|---|
| Record | `VL.Fu\src\Core\Styles\XStyle.cs` | `VL.Fu.Core.Styles` | Immutable instruction; writes the value into the node. |
| Process node | `VL.Fu\src\Styles\XNode.cs` or `XNodes.cs` | `VL.Fu.Styles` (VL: `Fu.Styles`) | Holds the pin values, outputs a cached record. |

The base types (`IStyle`, `Style`, `Style<TNode>`, `YogaStyle`, `StyleNode`) live in `VL.Fu\src\Core\Styling` (`VL.Fu.Core.Styling`) and should not change for a new style.

**Pins and inputs are typed `IStyle`, never `Style` or a record:** vvvv fails to compile a pin group of a record type. Records implement `IStyle` through `Style`.

## Pipeline in short

* A style chain is a tree. The pass runs inputs depth first, left to right, then the style itself, so the **later instruction wins**.
* Every pass starts from defaults. Removing a setter falls back to an earlier setter or the default; there is no reset.
* Yoga styles are written onto a shared stage node and copied once, so an unchanged result doesn't dirty layout.
* Yoga resolves overlapping slots by specificity, not order (`Margin.Left` beats `Margin.All`). Records writing overlapping slots therefore go through a helper that also clears the more specific slots it covers, so the later instruction wins: edges and positions use `YogaEdges`. Gaps don't clear yet (a row gap still beats `All`); see `.github\issues\style-overrides-issue.md`.
* Code outside the style pass (rendering, interaction) reads the committed Yoga style from the node's handle, e.g. `Layoutable.IsDisplayed`, never the records.

## 1. The record

Pick the base:

* `YogaStyle`: for anything that ends up in the Yoga style; override `unsafe void Build(YGNode* node)`.
* `Style<TNode>`: for values owned by Fu nodes, written through their staging API; override `void Build(TNode node)`.
  * Target `IFuNode`, the type every node of a Fu tree has, not a generic layer like `Layoutable<T>` or a concrete class.
  * Staging methods on core layers are `internal` (e.g. `StageTransformation`); reach them by casting to the closed layer: `((Layoutable<IFuNode>)node).StageTransformation(Transform)`. The cast is safe, since a node only styles itself.
  * On a node that isn't a `TNode`, the pass logs "X only styles TNode nodes" and skips the instruction.

```csharp
namespace VL.Fu.Core.Styles
{
    /// <summary>
    /// Sets the Yoga height. Points, ratio (stored as Yoga percent) and auto write the same slot,
    /// so the later instruction wins, also over the height of a <see cref="SizeStyle"/>.
    /// </summary>
    public sealed record HeightStyle(IReadOnlyList<IStyle> Inputs, YGValue Height) : YogaStyle(Inputs)
    {
        public override unsafe void Build(YGNode* node) => YogaDimension.SetHeight(node, Height);
    }
}
```

Rules:

* `sealed record`, `IReadOnlyList<IStyle> Inputs` first, then the values. Never a `Spread` parameter; `Style` copies non-`Spread` lists itself.
* Values are immutable value types (or records), so equal settings give equal records; the base already compares `Inputs` item by item.
* `Build` only makes deterministic assignments. It doesn't read live node state, layout results, children or config, and keeps no reference to the node.
* Store Yoga-native values (`YGValue`, Yoga enums). Facade conversions belong to the node, e.g. ratio via `YogaValues.Ratio`.
* An unsupported value **throws** (e.g. `YogaDimension.EnsureSupported`); the pass logs it on the styled node and skips the instruction.
* A record writing several slots validates everything before writing anything, so it never applies half.
* Shared unit switches go into an internal helper (`YogaDimension`), not copied per record.

### Record shapes

| Shape | Example | `Build` |
|---|---|---|
| Value | `HeightStyle(Inputs, YGValue Height)` | One setter call, through `YogaDimension` for size slots. |
| Enum | `DisplayStyle(Inputs, YGDisplay Display)` | `YogaEnums.EnsureSupported(Display, nameof(Display))`, then the setter; Yoga trusts enum values blindly. When only part of the enum is valid for the slot, the record owns `internal static bool IsSupported(T)` and passes it too (`AlignItemsStyle`). |
| Edge | `PaddingStyle(Inputs, YGEdge Edge, float Value)` | `YogaEdges.EnsureSupported(Edge, Value, "Padding")`, then `YogaEdges.Set(node, &SetPadding, Edge, Value)`. The record owns the function pointer target, `internal static unsafe void SetPadding(YGNode*, YGEdge, float)`. |
| Four edges | `PaddingLRTBStyle(Inputs, float Left, float Right, float Top, float Bottom)` | NaN marks an edge as not set. Validates all four, then `YogaEdges.Set(node, &PaddingStyle.SetPadding, Left, Right, Top, Bottom)`, reusing the single-edge record's target. |
| Axes | `PositionStyle(Inputs, float X, float Y)` | NaN marks an axis as not set and is skipped; infinity throws. One record serves the X, Y and both-axes nodes. |
| Derived | `AspectRatioStyle(Inputs, float AspectRatio)` | Stores the one canonical value; converting the input variants (`Vector2`, `Int2`) is the node's job. |

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

        protected override IStyle? BuildStyle(IReadOnlyList<IStyle> inputs) =>
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
  * `ValidateFinite` for `float`, `Vector2` and `Matrix` (a bad component drops the whole value);
  * `ValidateNonNegative` for values Yoga clamps or rejects below 0 (grow, shrink, padding, border);
  * `ValidateEnum(value, name, Record.IsSupported)` for enums: an unknown or unsupported value is ignored with a warning, so an earlier setter or the default holds. The record's `internal static IsSupported` is the single source of the allowed set.
  * other ranges are checked in the node with the same wording, `"X must be above 0, got {value}. It is ignored."` (see `AspectRatioStyleNode`);
  * a node mirrors its record's checks, so invalid input never reaches the pass. The record's throw is the backstop for records built in code.
* Facade conventions: no NaN on pins (use `Optional`); relative sizes are ratios where `1` is the whole parent (`YogaValues.Ratio`), not percent; vectors are Stride `Vector2`, whole-number pairs `Int2`; Yoga enums (`YGDisplay`, `YGEdge`, ...) go on pins as they are, since VL imports them.
* A non-value pin has a plain default instead of `Optional`, e.g. `YGEdge edge = YGEdge.All`; an unknown value is reported and the whole setting ignored. `SetGap` still corrects an unknown gutter to `All` instead; see `.github\issues\style-overrides-issue.md`.
* Nodes without a value (e.g. `Auto`) have no setter and always return their record.
* Axis nodes build the shared record with NaN for the other axis: `SetPositionX` builds `PositionStyle(inputs, x, float.NaN)`, so a later X doesn't touch an earlier Y.

### Shared base

Several nodes writing the same record share state through an abstract base:

* `[ProcessNode]` on the base, otherwise its members are not imported; the base is `public abstract`, the leaves `sealed`.
* The base holds the fields and the protected constructor ending in `RebuildStyle()`; leaves pass the `NodeContext` on with an empty body.
* Leaves only validate and call a protected `SetXCore(...)`, which stores the values with `SetValue` and rebuilds once if any changed. A `ClearX()` drops the value when the input is empty.
* The base implements `protected sealed override BuildStyle`; per-style differences are abstract factories (`BuildEdgeStyle`, `BuildEdgesStyle`) or virtual flags (`AllowsNegative`).
* Existing bases:
  * `GapStyleNode`: gap with gutter.
  * `EdgesStyleNode`, extended by `PaddingStyleNode`, `MarginStyleNode` and `BorderStyleNode`: one edge (`SetEdgeCore`) or four physical edges (`SetEdgesCore`); the four-edge values win when any is set.
  * `AspectRatioStyleNode`: converts every variant to one ratio, so proportional inputs such as 16:9 and 32:18 keep the output reference.

### Naming

* `Name = "SetX"` for the plain value, unit variants as `"SetX (Ratio)"`, `"SetX (Auto)"`; class `SetX<Variant>Node`, setter `SetX<Variant>`.
* Edge styles have `SetX` with a value and a `YGEdge` pin (default `All`), like `SetGap`, plus `"SetX (Uniform)"`, `"SetX (Horizontal)"`, `"SetX (Vertical)"` and `"SetX (LRTB)"`, sharing an abstract `[ProcessNode]` `XStyleNode : EdgesStyleNode`.
* Several input types for one value are variants named after the type, e.g. `"SetAspectRatio (Vector2)"` and `"SetAspectRatio (Int2)"`.
* Position offsets follow the width/height/size shape instead of the edge shape: `SetPositionX` (left), `SetPositionY` (top) and `SetPosition` (`Vector2`), all writing one `PositionStyle(Inputs, float X, float Y)` where NaN marks an axis as not set.
* One node per file is singular, `XNode.cs` (`TransformNode.cs`, `CombineNode.cs`); related nodes share one plural file, `XNodes.cs` (`WidthNodes.cs`, `GapNodes.cs`, `FlexNodes.cs`, `AlignNodes.cs`, `PositionNodes.cs`, `DisplayNodes.cs`). A shared base used by several files gets its own file (`EdgesStyleNode.cs`); a base used by one file stays in it (`PaddingStyleNode` in `PaddingNodes.cs`).
* Pin names in warnings are the facade names (`"PositionX"`, `"AspectRatio"`), so the user can find the pin.

### Left out on purpose

* Percent variants; the existing `SetWidth (Percent)` is `[Obsolete]`.
* `SetFlex`: under web defaults Yoga's `flex` only acts as a grow value that `FlexGrow` beats.
* Ratio or Auto variants of position offsets, until there is a need.

## 3. Tests

In `VL.Fu.Tests\Styles\StyleNodeTests.cs`:

* add every node to `Output_FromConstruction_IsBuilt` (`null` for value nodes, the record for valueless ones), obsolete ones too, inside `#pragma warning disable CS0618`;
* the value lands in the right slot and leaves the others untouched;
* later wins against an overlapping setter (e.g. `SetWidth` after `SetSize` changes only the width);
* invalid input is ignored with an empty output; an empty value passes the inputs through;
* an unknown enum value (e.g. `(YGDisplay)99`) is ignored;
* a record built in code with an invalid value is skipped by the pass and the earlier value holds;
* one layout check for behaviour that only shows after layout (relative units, absolute placement, direction, box sizing).

Helpers in that file: `S(...)` builds the input `Spread`; `LayOut(rootStyle, childStyles)` lays out a root with children and returns their bounds; `Resolved(style, EdgeKind)` returns the resolved edges, honouring the node's own direction. Styles that change rendering also get a test in `VL.Fu.Tests\Core\Rendering\RenderableTests.cs` (e.g. Display None skips the subtree).

Verify by building the solution and running the `VL.Fu.Tests` tests: in Visual Studio through the build and Test Explorer (filtered to project `VL.Fu.Tests`), from the command line with `dotnet test VL.Fu.Tests\VL.Fu.Tests.csproj`.

## Docs

* Multi-line `<summary>`; say what is set and what happens without a value, naming the default: "Without a value the display is not set here, so an earlier setter or the default, flex, holds."
* Put Yoga behaviour the user wouldn't expect in `<remarks>` (e.g. a ratio needs a definite parent size; X also clears Start and End).
* Say when a style only partly works yet (`SetOverflow` doesn't clip).
* Record new nodes in §10 of `.github\proposals\proposal-style-instructions.md` and in `.github\summaries\summary-core-inheritance.md`.