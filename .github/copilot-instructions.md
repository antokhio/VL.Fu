# Copilot Instructions

## Project Context

`Fu` stands for `functional ui` project. `Fu` is made for vvvv vl users to help create user interfaces using nodes.

- MCP servers available: Stride (engine source) and vvvv/VL (gamma libraries). Use them to look up Stride/VL APIs instead of guessing.

## Work in Progress

- Half-implemented code is a **request, not a report**. Files that don't compile, properties that are constructed but never wired, abstract members left unimplemented, fields assigned once and never read, dropped `IDisposable`s — these are the parts that need to be implemented, used as a template, and I am asking you to finish. Assume them - known.
- Do not answer with an inventory of what is missing. Infer the intent from the surrounding code (usually a sibling that already works), implement it, and build.
- Do surface anything that changes the _design_: a missing API that has to be added, a layering conflict, two plausible behaviours where the choice matters. Ask about those before writing code, not after.

## Tooling Boundaries

- vvvv gamma patches `.vl` files are hand-authored in the vvvv IDE by user. Don't read or edit them, they are outside of scope.

## Code preference

- Prefer IReadOnlyList<T> on facade Spread<T> internally.
- Properties on classes, or maybe used in context of ProcessNode, should have `public X => _x; public void SetX(...)` pattern for getters and setters, where setters and getters maybe called via mainloop every frame.
- NaN handling: we keep NaN's internally but on facade we replace it with Optional<float>
- A concrete `[ProcessNode(FragmentSelection = FragmentSelection.Explicit)]` class must mark its constructor with `[Fragment]`, otherwise VL doesn't expose it as a node. Abstract bases don't need it.
