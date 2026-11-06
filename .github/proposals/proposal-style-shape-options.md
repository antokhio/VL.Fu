# Proposal: Style Shape Options (`Fu.Core`)

Status: **Option B chosen, § 6 mostly decided** (open: names of the merge nodes, priority pin, namespace). Fix the shape

---

## 1. Context

* Every style is a **VL process node** (`[ProcessNode]`) with no `Update`: its fragments are an input style, its value pins and an `Output`. The output is built once in the constructor (so the node has a valid output from the start) and replaced only when something changes.
* Styles are **chained**: `In → SetA → SetB → Fu node`. A Fu node takes the end of the chain on a `Style` pin.
* **Upstream wins.** A patched component sets its own values close to the node, and a user overrides them by feeding a style into the component's `Style` input, which ends up upstream:

  ```text
  user: SetPadding(10) ──► [Button patch] Style in ──► SetPadding(5) ──► SetFlexDirection(Row) ──► FuNode
                                                                                     padding = 10
  ```

* Rendering is not part of the style: a Fu node is a provider, and the patch draws through an `ILayer` handed to the node (`Renderable.SetLayer`, § 7). No `SKPaint`, corner radius etc. in styles for now.
* Styles don't control the node yet, but a setter must be able to reach **any** node value (Yoga style, `Transformation`, later the layer…), not only Yoga.

## 2. What went wrong before ([VL.Fu5 `StyleNode` / `FuStyle`](../../../VL.Fu5/VL.Fu.Core/src/Style/FuStyle.cs))

```csharp
public record struct FuStyle(IFuStyle? Style, Action<IStylable> Apply);   // own action, then the input's
public void SetStyle(IFuStyle? style) { if (changed && style != null) style.Apply(this); }
```

1. **Apply-only.** Nothing ever undoes a value. Removing `SetWidth(100)` from the chain re-applies the rest, but the node stays 100 wide.
2. **Disconnecting the chain** from the node calls `SetStyle(null)`, which does nothing, so every value stays.
3. **Outputs are not snapshots.** The action reads the style node's live fields at apply time, so an older output (still held by another consumer) applies the newest values.
4. Variants of one property are separate actions (`SetWidth`, `SetWidthPercent`, `SetWidthAuto`), so nothing knows they compete for the same slot.

Upstream precedence itself already worked there (own action first, then the input's, so the input overwrites); it must be kept.

## 3. Requirements

| # | Requirement |
|---|---|
| R1 | Process node, output built at creation, rebuilt only when the input reference or a value changes. |
| R2 | Disconnect-safe: a value nobody sets any more returns to its default (or to a lower-precedence setter of the same property). Covers a removed setter, a disconnected chain and a deleted style node. |
| R3 | Upstream wins for the same property. |
| R4 | A setter can reach any node value. |
| R5 | Steady state costs a reference check per Fu node per frame; Yoga is only dirtied by real changes. |
| R6 | Outputs are immutable, so one style can feed many nodes. |
| R7 | New properties can be added from other assemblies (e.g. a controls library) without editing `Fu.Core`. |

## 4. Options

### Option A – Action chain + full reset

Keep the old delegate chain, but before applying, reset the node: copy the style of a pristine Yoga node into a scratch node, apply the chain to the scratch node, then `YGNodeCopyStyle(handle, scratch)` (Yoga only dirties the node if the style actually differs).

* ✅ Smallest change; covers every Yoga property for free, including edges and unit variants.
* ❌ Only Yoga can be reset this way. Any other node value (R4) needs its own reset list, which defeats the point.
* ❌ Every change re-applies the whole chain.

### Option B – Keyed entries with a diff (recommended)

A style is an immutable linked list of entries. Each entry is a **property key** plus a **value**. The node resolves the chain into "property → winning entry", diffs it against what it applied last time, applies changed entries and resets properties that disappeared.

* ✅ R1–R7: precise resets, upstream precedence by construction, any node value, properties declared anywhere.
* ✅ Only changed properties are touched; inspectable (a node can list what is styled and from where).
* ❌ Every property needs a key and a default, and variants must share one key (`Width` takes a `YGValue`, so `SetWidth`, `SetWidthPercent` and `SetWidthAuto` all write `Width`).

### Option C – One typed record

`StyleSheet` record with a nullable field per property; a setter is `input with { Width = input.Width ?? width }`; the node diffs field by field.

* ✅ Fast, strongly typed, trivial to inspect.
* ❌ Closed set: every new property edits the record and the diff (R7 fails). Non-Yoga values still need hand-written resets.

### Option D – Mutable cached output with a version

The output object is created once and mutated in place; consumers compare a version number.

* ❌ Same output may feed several chains; a consumer must walk the whole chain every frame to see whether any version changed (R5).
* ❌ Not a snapshot (problem 3 again). Rejected; "cached at creation" is better met by an immutable output that is replaced on change.

| | R1 | R2 | R3 | R4 | R5 | R6 | R7 |
|---|---|---|---|---|---|---|---|
| A | ✅ | Yoga only | ✅ | ❌ | partly | ✅ | ✅ |
| **B** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| C | ✅ | ✅ | ✅ | partly | ✅ | ✅ | ❌ |
| D | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ | ✅ |

## 5. Option B in detail

### 5.1 Types

```csharp
/// The pin type in VL. Immutable; constructors are private protected, so only Fu.Core defines kinds of style.
public abstract class Style
{
    private protected Style() { }

    /// Writes this style into `resolved`; everything upstream is written later and overwrites.
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
    public Style Set(Style? input, T value);                   // public factory, so other assemblies can add properties (R7)
}

/// One property + value, pointing upstream.
public abstract class StyleEntry : Style
{
    public Style? Input { get; }
    public abstract StyleProperty Property { get; }
    internal abstract bool Apply(object node);                 // false: node is not a TNode
    internal abstract bool HasSameValue(StyleEntry other);
    internal override void Collect(Dictionary<StyleProperty, StyleEntry> resolved)
    {
        resolved[Property] = this;
        Input?.Collect(resolved);
    }
}

internal sealed class StyleEntry<TNode, T> : StyleEntry where TNode : class;

/// Two chains joined, § 5.4. Everything in `strong` wins over everything in `weak`.
internal sealed class StyleMerge(Style? strong, Style? weak) : Style
{
    internal override void Collect(Dictionary<StyleProperty, StyleEntry> resolved)
    {
        weak?.Collect(resolved);
        strong?.Collect(resolved);
    }
}
```

* `Style` is the single, non-generic pin type. It is an abstract base rather than an interface: resolution relies on every kind being immutable and collecting in the right order, so other assemblies add *properties* (`StyleProperty<TNode, T>` + `Set`), not new kinds.
* A property targets a node type: `Renderable` for Yoga and node values (R4), or a derived control type declared in another assembly. On a node of another type the entry is skipped and the node warns through the `NodeBase.Log` pipeline. `Stylable<TSelf>` stays the lowest layer: it passes `this` and needs nothing from `TSelf`.
* Yoga slots with edges are one property per slot (`Margin.Left`, `Margin.All`, …), matching Yoga, which resolves `Left` > `Horizontal` > `All` itself.
* Defaults: § 5.5.

### 5.2 Node side (`Stylable<TSelf>`)

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
    style?.Collect(_next);                              // upstream is written last and wins

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
| Value pin changes | That style node outputs a new entry; downstream nodes rebuild theirs; the Fu node re-applies one property. |
| Setter removed from the middle | Its property is gone from `next` → reset, or a downstream setter of the same property takes over. |
| Chain disconnected (`SetStyle(null)`) | Every applied property is reset. |
| Same style on many nodes | Immutable, shared safely. |

### 5.3 Style node side (VL)

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

### 5.4 Joining chains

Position is priority: upstream wins. That makes the common case free: a component's own setters are its defaults and its `Style` input overrides them. The opposite case needs an explicit join, because a component can't put its setters upstream of a chain that comes from outside:

```text
Icon patch:  Style in ──► SetWidth(16) ──────────────► Image.Style    the user's width wins, 16 is the default
Icon patch:  Style in ──► Override(SetWidth(16)) ────► Image.Style    16 wins, the user can't change it
Image patch: Style in ──► SetWidth(bitmap.Width) ────► FuNode         follows the bitmap until a user sets a width
```

| Node | Result | Use |
|---|---|---|
| `Override(Input, Style)` = `StyleMerge(Style, Input)` | `Style` wins over `Input` | a component enforces values |
| `Fallback(Input, Style)` = `StyleMerge(Input, Style)` | `Input` wins over `Style` | join a whole default chain (a theme, a shared look) under the input |

* Names describe the outcome. `Prepend`/`Append` describe the position, which is ambiguous about who wins, since "upstream wins" is not the VL habit.
* Alternative: a `Priority` pin on every setter (`Low`/`Normal`/`High`; highest wins, ties go upstream). Not visible from the graph, adds a pin to every setter, and invites escalation (CSS `!important`). Not now; it fits later as an extra comparison in `Collect` without changing the shape.

### 5.5 Defaults

When a property is removed from the chain, the node falls back to:

1. A downstream setter of the same property, if any. Defaults patched as setters keep working (R2, falls out of `Collect`).
2. Otherwise `property.Default`:
   * Yoga properties read it once from a **template node**: a pristine `YGNode` created with `YogaConfig.Default`, never attached, alive for the process lifetime. `YogaStyleProperty<T>(name, get, set)` sets `Default = get(template)`, so no default is copied by hand and `UseWebDefaults` is honoured automatically. Style defaults depend only on `UseWebDefaults`, a constant in `YogaConfigCache`, so one template suffices.
   * Other node values declare their default explicitly.

* `get`/`set` are function pointers (`delegate*<YGNode*, T>`, `delegate*<YGNode*, T, void>`), since `YGNode*` can't be a generic argument. Applying needs the node's handle, so `Layoutable.Handle` becomes `protected internal`.
* No node-type `DefaultStyle`: patched components express defaults as setters in their own chain.

## 6. Decisions

| # | Question | Decision |
|---|---|---|
| 1 | Precedence | Upstream wins, also inside one chain; documented on every style node. Chains are joined with `Override`/`Fallback` (§ 5.4). **Open:** the names; whether a `Priority` pin is wanted now. |
| 2 | What a removed property returns to | A downstream setter, else the property default; Yoga defaults come from a template node (§ 5.5). |
| 3 | Style vs direct setters | Not mixed: a value is either styled or set directly, no conflict handling. Values with direct setters (`Transformation`) get no style property for now. |
| 4 | Pin type | Non-generic abstract base `Style` (§ 5.1). |
| 5 | Inheritance (CSS-like) | Out of scope. |
| 6 | Namespace clash | **Open:** the type `Style` lives in the namespace `Fu.Core.Style`; inside `Fu.Core.*`, `Style` binds to the namespace. Proposed: rename the namespace to `Fu.Core.Styling`. |

## 7. Outside the style: `Renderable.SetLayer`

* `Renderable` gets `SetLayer(ILayer? layer)`; `Render` pushes the local transformation, renders the layer, then the children. No other drawing properties on the node.
* The layer is set directly, not through a style, until styles are meant to control the node.
