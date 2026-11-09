# Proposal: Codebase Layout & Assembly Structure (`Fu.Core` vs `VL.Fu`)

Status: **draft / options comparison** (informed by [tebjan/vvvv-skills: vvvv-node-libraries](https://github.com/tebjan/vvvv-skills/blob/main/skills/vvvv-node-libraries/SKILL.md))

## 1. Context & Problem Statement

`Fu.Core` was originally started as a C# core library that might be reusable outside of vvvv gamma (`VL`), while `VL.Fu` was created as the VL-facing package holding `[ProcessNode]` wrappers and `VL.Fu.vl`.

In practice, however, **`Fu.Core` is deeply and intentionally coupled to VL**:
* **Base diagnostics & lifecycle (`NodeBase`):** Takes `[Pin(Visibility = PinVisibility.Hidden)] NodeContext` in every constructor up the chain (`NodeBase` → `Traversable` → `Stylable` → `Layoutable` → `Interactable` → `Renderable`), and uses `NodeContext.AddPersistentMessage` / `NodeContext.GetLogger()`.
* **Collections & primitives (`Traversable`, `Style`, `StyleNode`):** Uses `VL.Lib.Collections.Spread<T>`, `SpreadBuilder<T>`, and `VL.Core.Optional<T>` at the heart of its memory and equality model.
* **Process nodes in Core (`StyleNode`):** Uses `[ProcessNode]` and `[Pin(PinGroupKind = PinGroupKind.Collection)]` directly in `Fu.Core.Styling`.
* **Rendering & interaction contracts (`Interactable`, `Renderable`, `Layoutable`):** Implements `VL.Skia.IBehavior`, `VL.Skia.ILayer`, `VL.Skia.CallerInfo`, `VL.Lib.IO.Notifications.INotification`, and `Stride.Core.Mathematics` (`Matrix`, `RectangleF`).
* **Assembly initialization (`Initializer.cs`):** Registers `[assembly: AssemblyInitializer(typeof(Fu.Core.Initializer))]` via `VL.Core.CompilerServices`.

Additionally, splitting the code across `Fu.Core` and `VL.Fu` has already introduced friction:
1. **Split features:** Adding a single style requires touching `Fu.Core\Styles\FooStyle.cs` (`Fu.Core.Styles`), `VL.Fu\src\Styles\FooNodes.cs` (`VL.Fu.Styles`), and `Fu.Core.Tests\Styling\...` (which has to reference *both* `Fu.Core` and `VL.Fu`).
2. **Cross-assembly visibility:** Staging methods on layers (like `Layoutable.StageTransformation`) are currently `internal` in `Fu.Core`. If a concrete style record lives in `VL.Fu`, it cannot call `internal` staging methods on `Fu.Core` types without `InternalsVisibleTo` or widening the public API.
3. **VL Node Browser Import Rules (`vvvv-node-libraries` skill):**
   * In vvvv, `[ProcessNode]` does **not** gate node visibility—**every `public` type and its public members** inside an imported namespace become nodes in the NodeBrowser!
   * Furthermore, `[ImportAsIs]` / `[ImportNamespace]` are **recursive** across all child namespaces, and `[ProcessNode(HasStateOutput = true)]` (like `FuNode`) un-hides the class and **every inherited `public` method/property** as individual operation nodes.
   * Having types split across two assemblies (`Fu.Core` and `VL.Fu`) forces us to either import both assemblies (exposing C# engine plumbing in the NodeBrowser unless carefully curated) or struggle with cross-assembly pin types and `internal` visibility.

Before the codebase grows with gestures, rendering, layout styles, and controls, we should decide on the project, namespace, and VL import structure.

---

## 2. Key vvvv Node Library Mechanics to Design Around

From the [`vvvv-node-libraries` skill](https://github.com/tebjan/vvvv-skills/blob/main/skills/vvvv-node-libraries/SKILL.md) (verified against `VL.StandardLibs`):

1. **Library Folder Recognition & `.csproj` Output Path:**
   * vvvv recognizes `VL.Fu/` as a library because `VL.Fu/` matches `VL.Fu.vl` (and `VL.Fu.nuspec`).
   * The `.csproj` inside `VL.Fu\src\` should output directly to `..\lib\net8.0\` (`<OutputPath>..\lib\net8.0\</OutputPath>` with `<AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>`, rather than `<BaseOutputPath>..\lib\</BaseOutputPath>` which inserts a `Debug\` folder: `VL.Fu\lib\Debug\net8.0\`).
2. **Recursive Namespace Importing & `Category` Requirement:**
   * `[assembly: ImportAsIs(Namespace = "VL.Fu", Category = "Fu")]` (or `[assembly: ImportNamespace(...)]`) is **recursive**: every sub-namespace under `VL.Fu` is imported automatically.
   * **Always specify `Category = "Fu"`**: omitting `Category` strips `VL.Fu` and dumps sub-namespaces like `VL.Fu.Styles` into top-level `Styles` instead of `Fu.Styles`!
   * If using multiple `[assembly: ImportNamespace(...)]` attributes, **first declaration wins** (not longest prefix)—most specific namespaces must be listed first, with the root catch-all last.
3. **Four Levers for Controlling NodeBrowser Visibility:**
   * **Lever 1 — `internal`:** `ImportAll` ignores non-public types completely (`if (s.DeclaredAccessibility != Accessibility.Public) return;`). Anything that is `internal` (like `WidthStyle`, `GapStyle`, `TransformStyle`, `YogaConfigCache`, `YogaStyleStage`, `CombinedStyle`, `MatrixExtensions`) never enters VL at all, while remaining testable via `[InternalsVisibleTo("VL.Fu.Tests")]`.
   * **Lever 2 — `[Smell(SymbolSmell.Internal)]` / `[Smell(SymbolSmell.Hidden)]` / `[Smell(SymbolSmell.Advanced)]` (from `VL.Core.Import`):** Keeps a C# type or member `public` (so it can be a pin type, base class, or cross-assembly API), but filters it from the NodeBrowser. Note: `[Smell]` on a type does *not* cascade to its members!
   * **Lever 3 — Aspect Keywords in Namespace / Category (`Internal`, `Hidden`, `Advanced`, `Experimental`):** Any category or sub-namespace segment named `Internal`, `Hidden`, or `Advanced` applies that `SymbolSmell` to **all types and members inside it** and strips the keyword from the final category name. For example, with `[assembly: ImportNamespace("VL.Fu.Core", Category = "Fu.Core.Internal")]` (or namespace `VL.Fu.Core.Internal`), every public base class (`NodeBase`, `Traversable`, `Stylable`, `Layoutable`, `Interactable`, `Renderable`) remains a valid `public` C# type and pin type in VL, while its C# methods (`AddPersistentWarning`, `Log`, `ClearWarning`, `Build`) are hidden from end-user NodeBrowser search!
   * **Lever 4 — `[ProcessNode]` State Output (`HasStateOutput`):**
     * `[ProcessNode]` without `HasStateOutput = true` (e.g. `SetWidthNode`, `SetGapNode`, `CombineNode`) automatically stamps `SymbolSmell.Hidden` on the backing C# class so only the ProcessNode appears.
     * `[ProcessNode(HasStateOutput = true)]` (e.g. `FuNode`) un-hides the class so its instance pin can be wired into `Children` / `Parent` / `ILayer`—which also exposes its `public` members (`Parent`, `Children`, `SetParent`, `SetChildren`, `Style`, `SetStyle`, `Layout`, `Transformation`, `SetTransformation`, `Config`, `SetConfig`, `CalculateLayout`, `Bounds`, `Render`, `Notify`) as operation nodes. Any `public` C# helper on `NodeBase` that shouldn't be a patch node (like `AddPersistentWarning`, `Log`, `ClearWarning`) should be `protected` / `internal` or marked `[Smell(SymbolSmell.Internal)]`.

---

## 3. Evaluating the Three Options

### Option 1: Rename `Fu.Core` → `VL.Fu.Core` (Keep Two Assemblies)

Keep the two-project split (`VL.Fu.Core` + `VL.Fu`), renaming `Fu.Core` so the naming honestly reflects that it is a VL library.

* **Projects:**
  * `VL.Fu.Core\VL.Fu.Core.csproj`
  * `VL.Fu\src\VL.Fu.csproj`
  * `VL.Fu.Tests\VL.Fu.Tests.csproj`
* **Pros:**
  * Physical compiler boundary between the foundational engine and concrete nodes/widgets.
* **Cons:**
  * **Artificial split for styles & elements:** Every feature is split across two `.csproj`s.
  * **`internal` boundary friction:** Because `WidthStyle`, `GapStyle`, `TransformStyle` only exist to feed `Style` pins (where the pin type is `Style`), in a single assembly those concrete records can be `internal` (Lever 1—zero NodeBrowser clutter) or share `internal` staging methods (`StageTransformation`). Across two assemblies, they must be `public` or use `[InternalsVisibleTo("VL.Fu")]`.
  * **Two assemblies to configure for VL import:** Both `VL.Fu.Core.dll` and `VL.Fu.dll` land in `lib\net8.0\` and need coordinated `[ImportNamespace]` / `[AssemblyInitializer]` setups.

---

### Option 2: Single Assembly — Merge `Fu.Core` into `VL.Fu` (with `VL.Fu.Core` Namespace)

Remove the `Fu.Core` project and place all C# code inside `VL.Fu\src\VL.Fu.csproj`, organizing the engine layers under `VL.Fu.Core.*` and VL-exposed nodes under `VL.Fu.*`.

* **Projects:**
  * `VL.Fu\src\VL.Fu.csproj` (outputs directly to `..\lib\net8.0\`)
  * `VL.Fu.Tests\VL.Fu.Tests.csproj`
* **Pros:**
  * **Matches the standard vvvv library pattern (`vvvv-node-libraries`):**
    ```text
    VL.Fu/
    ├── VL.Fu.vl
    ├── lib/net8.0/VL.Fu.dll
    └── src/
        ├── Initialization.cs
        └── VL.Fu.csproj
    ```
  * **Lever 1 (`internal`) works at full power:**
    * Engine plumbing (`INodeBase`, `CombinedStyle`, `YogaConfigCache`, `YogaStyleStage`, `MatrixExtensions`) and staging hooks (`StageTransformation`) are simply `internal`.
    * Even concrete style records (`WidthStyle`, `GapStyle`, `TransformStyle`) can be `internal` if users only patch with `Style` pins and `SetWidth` / `SetGap` / `SetTransform` process nodes—preventing the records and their `Build(...)` methods from ever appearing as nodes in the NodeBrowser, while `VL.Fu.Tests` still tests them directly via `[InternalsVisibleTo("VL.Fu.Tests")]`!
  * **Centralized `Initialization.cs` with `[ImportNamespace]`:**
    * One place controls category mapping and aspect smells (`Internal`, `Advanced`) for the entire library.
  * **Co-located features:** A style record and its `[ProcessNode]`(s) live in the same folder/file (`src/Styles/Width.cs`, `src/Styles/Gap.cs`, `src/Styles/Transform.cs`).
* **Cons:**
  * Layering between `Core` and concrete nodes/styles is enforced by namespace rules (and Roslyn `check_architecture`) rather than separate `.csproj` files.

---

### Option 3: Strip VL Constraints from `Fu.Core` (Pure Headless UI Core)

Refactor `Fu.Core` so it has zero dependency on `VL.Core` or `VL.Core.Skia`, turning it into a standalone C# Yoga + Skia UI library, and adapt it in `VL.Fu`.

* **Pros:**
  * Reusable outside vvvv.
* **Cons:**
  * **High effort & ongoing adapter tax for a non-goal:** Requires replacing `NodeContext` patch warnings, `Spread<T>`, `Optional<T>`, `StyleNode`, `ILayer`, `IBehavior`, `CallerInfo`, `INotification`, and `Stride.Core.Mathematics` with custom abstractions and wrapper layers.

---

## 4. Comparison Summary

| Criterion | Option 1: `VL.Fu.Core` + `VL.Fu` (2 projects) | Option 2: Single `VL.Fu` project (`VL.Fu.Core` namespace) | Option 3: Pure VL-free `Fu.Core` |
| :--- | :--- | :--- | :--- |
| **Effort now** | Low (rename project + namespaces) | Low (move files into `VL.Fu\src\Core\`, delete `Fu.Core`) | High (rewrite base, tree, style node, skia contracts) |
| **Ongoing friction** | Medium (2 projects per feature, cross-DLL visibility) | **Lowest** (one assembly, `internal` works naturally) | High (adapters for `Spread`, `Optional`, `NodeContext`, `ILayer`) |
| **NodeBrowser hygiene (`vvvv-node-libraries`)** | Harder (must keep cross-DLL types `public` + manage `[Smell]`) | **Best** (`internal` hides records/plumbing; one `Initialization.cs`) | Harder (public C# core + public VL wrappers) |
| **Fits `vvvv-node-libraries` layout** | Non-standard (extra DLL) | **Exact match (`VL.Fu\src\VL.Fu.csproj` → `lib\net8.0\`)** | Unusual / over-abstracted |

**Recommendation:** **Option 2** (Single `VL.Fu` assembly with `VL.Fu.Core` namespace).

---

## 5. Proposed Layout & VL Import Design (Option 2)

### 5.1 Repository & Directory Structure

Following the [Library Recognition Pattern](https://github.com/tebjan/vvvv-skills/blob/main/skills/vvvv-node-libraries/SKILL.md#library-recognition-pattern):

```text
VL.Fu/                                   # Solution / repo root
├── Directory.Build.props
├── Directory.Packages.props
├── VL.Fu.slnx
├── VL.Fu/                               # vvvv package root (matches VL.Fu.vl)
│   ├── VL.Fu.vl                         # Hand-authored vvvv document
│   ├── lib/
│   │   └── net8.0/                      # OutputPath of VL.Fu.csproj (no Debug/ subfolder)
│   │       └── VL.Fu.dll
│   └── src/
│       ├── VL.Fu.csproj
│       ├── Initialization.cs            # [assembly: ImportNamespace(...)], [assembly: AssemblyInitializer(...)]
│       │
│       ├── Core/                        # Foundational engine (Namespace: VL.Fu.Core.*)
│       │   ├── Base/
│       │   │   ├── NodeBase.cs          # INodeBase (internal), NodeBase
│       │   │   └── NodeBaseExtensions.cs
│       │   ├── Tree/
│       │   │   └── Traversable.cs       # ITraversable, ITraversable<T>, Traversable<TSelf>, TraversableExtensions
│       │   ├── Styling/
│       │   │   ├── Style.cs             # Style, Style<TNode>, CombinedStyle (internal)
│       │   │   ├── YogaStyle.cs         # YogaStyle
│       │   │   ├── Stylable.cs          # IStylable, Stylable<TSelf>
│       │   │   └── StyleNode.cs         # StyleNode ([ProcessNode] base)
│       │   ├── Layout/
│       │   │   ├── Layoutable.cs        # ILayoutable, Layoutable<TSelf>
│       │   │   ├── YogaConfig.cs        # YogaConfig, YogaConfigCache (internal)
│       │   │   └── YogaStyleStage.cs    # YogaStyleStage (internal)
│       │   ├── Interaction/
│       │   │   ├── Interactable.cs      # Interactable<TSelf>
│       │   │   └── Gesture.cs           # Gesture
│       │   ├── Rendering/
│       │   │   └── Renderable.cs        # IRenderable, Renderable
│       │   └── Extensions/
│       │       └── MatrixExtensions.cs  # internal Matrix <-> SKMatrix helpers
│       │
│       ├── Styles/                      # Concrete Style records + StyleNode process nodes (Namespace: VL.Fu.Styles)
│       │   ├── CombineNode.cs           # CombineNode
│       │   ├── WidthNodes.cs            # WidthStyle + SetWidthNode, SetWidthPercentNode, SetWidthAutoNode
│       │   ├── GapNodes.cs              # GapStyle + GapStyleNode, SetGapNode, SetRowGapNode, SetColumnGapNode
│       │   └── TransformNode.cs         # TransformStyle + SetTransformNode
│       │
│       └── Elements/                    # Concrete UI element ProcessNodes (Namespace: VL.Fu.Elements or VL.Fu)
│           └── FuNode.cs                # FuNode : Renderable
│
└── VL.Fu.Tests/                         # Unit tests ([InternalsVisibleTo("VL.Fu.Tests")])
    ├── VL.Fu.Tests.csproj
    ├── Core/
    │   ├── Tree/TraversableTests.cs
    │   ├── Styling/StylableTests.cs
    │   ├── Layout/LayoutableTests.cs
    │   ├── Layout/YogaConfigTests.cs
    │   ├── Rendering/RenderableTests.cs
    │   └── Extensions/MatrixExtensionsTests.cs
    └── Styles/
        └── StyleNodeTests.cs
```

### 5.2 `.csproj` Configuration (`VL.Fu\src\VL.Fu.csproj`)

Per the `vvvv-node-libraries` skill, use `<OutputPath>..\lib\net8.0\</OutputPath>` with `<AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>` instead of `<BaseOutputPath>..\lib\</BaseOutputPath>` so compiled DLLs land in `VL.Fu\lib\net8.0\` regardless of `Debug`/`Release` configuration:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputPath>..\lib\net8.0\</OutputPath>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="antokhio.YogaSharp" />
    <PackageReference Include="VL.Core" />
    <PackageReference Include="VL.Core.Skia" />
  </ItemGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="VL.Fu.Tests" />
  </ItemGroup>
</Project>
```

### 5.3 `Initialization.cs` & Category / Aspect Strategy

Using `[assembly: ImportNamespace]` (specific-first, catch-all last) lets us map C# namespaces cleanly to vvvv categories and aspects:

```csharp
using VL.Core;
using VL.Core.CompilerServices;
using VL.Core.Import;

// Specific first, catch-all last (first-wins rule):
// 1. Core engine types/interfaces land under Fu.Core with the Advanced (or Internal) aspect
[assembly: ImportNamespace("VL.Fu.Core",   Category = "Fu.Core.Advanced")]
// 2. Style setter ProcessNodes land under Fu.Styles
[assembly: ImportNamespace("VL.Fu.Styles", Category = "Fu.Styles")]
// 3. Root / element ProcessNodes land under Fu
[assembly: ImportNamespace("VL.Fu",        Category = "Fu")]

[assembly: AssemblyInitializer(typeof(VL.Fu.Initialization))]

namespace VL.Fu
{
    public sealed class Initialization : AssemblyInitializer<Initialization>
    {
        public override void Configure(AppHost appHost)
        {
            base.Configure(appHost);
        }
    }
}
```

### 5.4 NodeBrowser Hygiene Checklist (Applying the 4 Levers)

Because `FuNode` has `[ProcessNode(HasStateOutput = true)]`, vvvv imports `FuNode` and its inherited public members. To keep the NodeBrowser clean:
1. **Concrete `Style` records (`WidthStyle`, `GapStyle`, `TransformStyle`):**
   * Can be marked `internal` (or `[Smell(SymbolSmell.Internal)]` if kept `public` for external C# extensions) so `WidthStyle.Create` and `WidthStyle.Build` don't clutter `Fu.Styles` alongside `SetWidth`.
2. **C#-only diagnostics on `NodeBase` (`AddPersistentWarning`, `Log`, `ClearWarning`, `WarningLifetime`) and `NodeBaseExtensions` (`LogInfo`, `LogWarning`, `LogError`):**
   * Make `NodeBaseExtensions` `internal` (since all nodes live in `VL.Fu`, or mark `[Smell(SymbolSmell.Internal)]`), and either make `NodeBase`'s warning methods `protected` (with `INodeBase` for internal callers/tests) or decorate them with `[Smell(SymbolSmell.Internal)]` so patchers don't see `ClearWarning` / `AddPersistentWarning` as nodes on `FuNode`.
3. **C#-only `Style.Build` / `YogaStyle.Build`:**
   * Mark `Build` with `[Smell(SymbolSmell.Internal)]` (or `internal` / `protected internal` if all styles are in-assembly) so `Build` isn't offered as a patch operation on a `Style` wire.
4. **Architecture Guard:**
   * `VL.Fu.Core.*` never references `VL.Fu.Styles` or `VL.Fu` element classes, verifiable via Roslyn `check_architecture`.

