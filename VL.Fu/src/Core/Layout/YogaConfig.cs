using YogaSharp;

namespace VL.Fu.Core.Layout
{
    /// <summary>
    /// Yoga settings for a subtree. Set it on any node with <see cref="ILayoutable.SetConfig"/>;
    /// every descendant without its own config inherits it, and nodes without any config above them use <see cref="Default"/>.
    /// </summary>
    /// <remarks>
    /// Yoga rounds each node with its own config, so different <see cref="PointScaleFactor"/>s inside one tree
    /// can leave small gaps between nodes.
    /// <c>UseWebDefaults</c> is not part of the config: Yoga can't change it on an existing node, so it is the same for all of Fu.
    /// </remarks>
    /// <param name="PointScaleFactor">
    /// Units per rounding step. 0 turns rounding off, which suits vvvv spaces where one unit is far bigger than a pixel.
    /// </param>
    /// <param name="Errata">
    /// Yoga behaviours that deviate from the web, kept for compatibility. <see cref="YGErrata.None"/> follows the spec.
    /// </param>
    /// <param name="WebFlexBasis">
    /// Enables Yoga's experimental web-compatible flex-basis behaviour.
    /// </param>
    public sealed record YogaConfig(
        float PointScaleFactor = 0f,
        YGErrata Errata = YGErrata.None,
        bool WebFlexBasis = true
    )
    {
        /// <summary>
        /// The config used by every node that has no config of its own and none above it.
        /// </summary>
        public static YogaConfig Default { get; } = new();
    }

    /// <summary>
    /// Shares one native Yoga config between all nodes using an equal <see cref="YogaConfig"/>.
    /// Configs are reference counted and freed once the last node lets go, so a patch animating a value
    /// doesn't leak a native config per frame. <see cref="YogaConfig.Default"/> is never freed.
    /// </summary>
    internal static unsafe class YogaConfigCache
    {
        private const bool UseWebDefaults = true;

        private sealed class Entry
        {
            public required nint Handle;
            public int References;
        }

        private static readonly object s_lock = new();
        private static readonly Dictionary<YogaConfig, Entry> s_entries = new()
        {
            [YogaConfig.Default] = new Entry
            {
                Handle = (nint)Create(YogaConfig.Default),
                References = 1,
            },
        };

        /// <summary>
        /// Returns the native config for <paramref name="config"/>, creating it if needed.
        /// Every call must be balanced by one <see cref="Release"/> once the node no longer uses it.
        /// </summary>
        public static YGConfig* Acquire(YogaConfig config)
        {
            lock (s_lock)
            {
                if (!s_entries.TryGetValue(config, out var entry))
                    s_entries.Add(config, entry = new Entry { Handle = (nint)Create(config) });

                entry.References++;
                return (YGConfig*)entry.Handle;
            }
        }

        /// <summary>
        /// Gives back a config obtained from <see cref="Acquire"/>.
        /// No node may still point at it when the count drops to zero, so switch the node first, then release.
        /// </summary>
        public static void Release(YogaConfig config)
        {
            lock (s_lock)
            {
                if (!s_entries.TryGetValue(config, out var entry) || --entry.References > 0)
                    return;

                s_entries.Remove(config);
                ((YGConfig*)entry.Handle)->Dispose();
            }
        }

        /// <summary>
        /// Whether a native config for <paramref name="config"/> currently exists. Used by tests.
        /// </summary>
        internal static bool IsAlive(YogaConfig config)
        {
            lock (s_lock)
                return s_entries.ContainsKey(config);
        }

        private static YGConfig* Create(YogaConfig config)
        {
            var native = YGConfig.New();
            native->SetUseWebDefaults(UseWebDefaults);
            native->SetPointScaleFactor(config.PointScaleFactor);
            native->SetErrata(config.Errata);
            // YogaSharp exposes YGConfigSetExperimentalFeatureEnabled under this name.
            native->SetContext(YGExperimentalFeature.WebFlexBasis, config.WebFlexBasis);
            return native;
        }
    }
}
