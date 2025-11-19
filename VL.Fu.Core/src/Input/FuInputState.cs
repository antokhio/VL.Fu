using System.Collections.Immutable;

namespace VL.Fu.Core.Input
{
    public readonly record struct FuInputState
    {
        public IReadOnlyDictionary<int, FuPointer> Pointers { get; init; } =
            ImmutableDictionary<int, FuPointer>.Empty;
        public IReadOnlySet<FuKey> Keys { get; init; } = ImmutableHashSet<FuKey>.Empty;
        public IReadOnlySet<FuKey> Modifiers { get; init; } = ImmutableHashSet<FuKey>.Empty;
        public FuMouse Mouse { get; init; } = default;

        public FuInputState(
            IReadOnlyDictionary<int, FuPointer> pointers,
            IReadOnlySet<FuKey> keys,
            IReadOnlySet<FuKey> modifiers,
            FuMouse mouse
        )
        {
            Pointers = pointers;
            Keys = keys;
            Modifiers = modifiers;
            Mouse = mouse;
        }
    }
}
