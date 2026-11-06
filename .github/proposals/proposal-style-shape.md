# Proposal: Style Shape (`Fu.Core`)

Status: **agreed**, implementation starts next session. Open: § 7. Individual style nodes (`SetWidth`, `SetPadding`, …) come after the shape is in place.

## 1. Context

* Every style is a **VL process node** (`[ProcessNode]`) without `Update`. Its output is built in the constructor and replaced only when the input or a value changes.
* Styles are chained, `SetA → SetB → Fu node`, and the node takes the end of the chain on its `Style` pin.
* Rendering is not part of the style: the patch draws through an `ILayer` handed to the node (§ 6). No `SKPaint`, corner radius etc. for now.
* Styles don't control the node yet, but a setter must be able to reach any node value, not only Yoga.

## 2. Lessons from VL.Fu5 (`StyleNode` / `FuStyle`)

1. **Apply-only.** Nothing undid a value: a removed setter or a disconnected chain (`SetStyle(null)` was a no-op) left its values on the node.
2. **Outputs were not snapshots.** Actions read the style node's live fields, so an older output applied the newest values.
3. **Variants were unrelated actions** (`SetWidth`, `SetWidthPercent`, `SetWidthAuto`), so nothing knew they compete for one slot.
4. **Upstream won**, so a user could override a component's predefined values through its `Style` input. That is the opposite of VL's "last `Set` wins" and is replaced by `Combine` (§ 4.4).

## 3. Requirements

| # | Requirement |
|---|---|
| R1 | Process node; output built at creation, rebuilt only when the input reference or a value changes. |
| R2 | Disconnect-safe: a value nobody sets any more falls back to an earlier setter of the same property, else to its default. |
| R3 | One precedence rule everywhere: **later wins**. |
| R4 | A setter can reach any node value. |
| R5 | Steady state costs one reference check per Fu node per frame; Yoga is only dirtied by real changes. |
| R6 | Outputs are immutable, so one style can feed many nodes. |
| R7 | Other assemblies can add properties without editing `Fu.Core`. |

## 4. Shape: keyed entries with a diff

A style is an immutable graph of entries, each a **property key** plus a **value**. The node resolves it into "property → winning entry", diffs that against what it applied last time, applies changed entries and resets properties that disappeared.

Rejected: a delegate chain plus a full Yoga reset (only Yoga can be reset), one typed record with a field per property (closed set, fails R7), a mutable output with a version (not a snapshot, consumers walk the chain every frame).

### 4.1 Precedence

Styles apply in order and **later wins**:

* In a chain, downstream wins: `SetWidth(10) → SetWidth(5) → node` gives 5.
* In `Combine`, later items win.

### 4.2 Types

```csharp
/// The pin type in VL. Immutable; constructors are private protected, so only Fu.Core defines kinds of style.
public abstract class Style
{
    private protected Style() { }

    /// Writes this style into `resolved` in application order; later writes win.
    internal abstract void Collect(Dictionary<StyleProperty, StyleEntry> resolved);
}

/// The key: one static instance per styleable slot.
public abstract class StyleProperty
{
    public string Name { get; }
    internal abstract bool Reset(object node);                 // false: node is not a TNode
}

public class StyleProperty<TNode, T> : StyleProperty where TNode : class
{
    public StyleProperty(string name, T defaultValue, Action<TNode, T> apply, IEqualityComparer<T>? comparer = null);
    public T Default { get; }
    public Style Set(Style? input, T value);                   // public factory (R7)
}

/// One property + value, after its input.
public abstract class StyleEntry : Style
{
    public Style? Input { get; }
    public abstract StyleProperty Property { get; }
    internal abstract bool Apply(object node);                 // false: node is not a TNode
    internal abstract bool HasSameValue(StyleEntry other);
    internal override void Collect(Dictionary<StyleProperty, StyleEntry> resolved)
    {
        Input?.Collect(resolved);
        resolved[Property] = this;
    }
}

internal sealed class StyleEntry<TNode, T> : StyleEntry where TNode : class;

/// § 4.4. Items apply in order; later items win.
internal sealed class StyleCombination(Spread<Style> styles) : Style
{
    internal override void Collect(Dictionary<StyleProperty, StyleEntry> resolved)
    {
        foreach (var style in styles)
            style.Collect(resolved);
    }
}
```

* `Style` is the single, non-generic pin type. It is an abstract base rather than an interface: resolution relies on every kind being immutable and collecting in order, so other assemblies add *properties*, not kinds.
* A property targets a node type: `Renderable` for Yoga and node values (R4), or a derived control type from another assembly. On a node of another type the entry is skipped and the node warns through the `NodeBase.Log` pipeline. `Stylable<TSelf>` stays the lowest layer: it passes `this` and needs nothing from `TSelf`.
* Unit variants share one key: `Width` takes a `YGValue`, so `SetWidth`, `SetWidthPercent` and `SetWidthAuto` all write `Width`.
* Yoga edge slots are one property each (`Margin.Left`, `Margin.All`, …); Yoga resolves `Left` > `Horizontal` > `All` itself.

### 4.3 Node side (`Stylable<TSelf>`)

```csharp
private Style? _style;
private Dictionary<StyleProperty, StyleEntry> _applied = new();
private Dictionary<StyleProperty, StyleEntry> _next = new();

public void SetStyle(Style? style)
{
    if (ReferenceEquals(_style, style))
        return;                                         // steady state: one reference check
    _style = style;

    _next.Clear();
    style?.Collect(_next);

    foreach (var property in _applied.Keys)
        if (!_next.ContainsKey(property))
            property.Reset(this);                       // R2: removed setter, disconnected chain, deleted node

    foreach (var (property, entry) in _next)
        if (!_applied.TryGetValue(property, out var previous) || !previous.HasSameValue(entry))
            if (!entry.Apply(this))
                Log(LogLevel.Warning, $"{property.Name} does not apply to {GetType().Name}.");

    (_applied, _next) = (_next, _applied);              // no allocation per change
}
```

| Patch action | Effect |
|---|---|
| Value pin changes | That style node outputs a new entry, downstream nodes rebuild theirs, the Fu node re-applies one property. |
| Setter removed | Its property falls back to an earlier setter of the same property, else resets. |
| Chain disconnected (`SetStyle(null)`) | Every applied property resets. |

### 4.4 `Combine`

`Combine(IReadOnlyList<Style?> styles)` joins chains; the order of the list is the precedence. A component decides where its own values go relative to its `Style` input:

```text
Button patch:  SetPadding(5) → SetFlexDirection(Row) ─┐
                                          Style in ───┴─► Combine ─► FuNode   defaults first: the user's padding wins

Icon patch:                               Style in ───┐
                                      SetWidth(16) ───┴─► Combine ─► Image    enforced last: 16 wins
```

* The list is copied into a `Spread` (snapshot, R6); `null` items are skipped.
* The node rebuilds its output only when an item changed by reference, so an upstream spread rebuilt with the same items keeps the output stable (R5).
* No items outputs `null`; a single item outputs the item itself.

### 4.5 Style nodes (VL)

```csharp
[ProcessNode]
public abstract class StyleNode<T>
{
    private Style? _input;
    private T _value;

    protected StyleNode(T defaultValue) { _value = defaultValue; Output = Build(); }   // cached at creation

    public void SetInput(Style? input) { if (!ReferenceEquals(_input, input)) { _input = input; Output = Build(); } }
    protected void SetValue(T value) { if (!Equals(_value, value)) { _value = value; Output = Build(); } }

    public Style Output { get; private set; }
    protected abstract StyleProperty<Renderable, T> Property { get; }
    private Style Build() => Property.Set(_input, _value);
}

[ProcessNode(Name = "SetWidth")]
public sealed class SetWidthNode : StyleNode<YGValue>
{
    public void SetWidth(float width = float.NaN) => SetValue(YGValue.Points(width));
}
```

* VL calls methods before getters, so `Output` is current in the same frame.
* A node setting several slots (`SetMargin(left, top, right, bottom)`) emits several entries.

### 4.6 Defaults

When a property is no longer set, it falls back to an earlier setter of the same property (defaults patched as setters keep working), else to `property.Default`:

* Yoga properties read it once from a **template node**: a pristine `YGNode` created with `YogaConfig.Default`, never attached, alive for the process lifetime. `YogaStyleProperty<T>(name, get, set)` sets `Default = get(template)`, so no default is copied by hand and `UseWebDefaults` (a constant in `YogaConfigCache`) is honoured.
* `get`/`set` are function pointers (`delegate*<YGNode*, T>`, `delegate*<YGNode*, T, void>`), since `YGNode*` can't be a generic argument. Applying needs the handle, so `Layoutable.Handle` becomes `protected internal`.
* Other node values declare their default explicitly.
* No node-type `DefaultStyle`: patched components put their defaults first in a `Combine`.

## 5. Decisions

| Question | Decision |
|---|---|
| Precedence | Later wins, in chains and in `Combine` (§ 4.1, § 4.4). No priority pin. |
| What a removed property returns to | An earlier setter, else the property default; Yoga defaults from a template node (§ 4.6). |
| Style vs direct setters | Not mixed: a value is either styled or set directly. Values with direct setters (`Transformation`) get no style property for now. |
| Pin type | Non-generic abstract base `Style` (§ 4.2). |
| Inheritance (CSS-like) | Out of scope. |

## 6. Outside the style: `Renderable.SetLayer`

`Renderable` gets `SetLayer(ILayer? layer)`; `Render` pushes the local transformation, renders the layer, then the children. The layer is set directly, not through a style.

## 7. Open

* **Namespace clash.** The type `Style` lives in the namespace `Fu.Core.Style`; inside `Fu.Core.*`, `Style` binds to the namespace. Proposed: rename the namespace to `Fu.Core.Styling`.
