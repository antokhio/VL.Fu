using YogaSharp;

namespace Fu.Core.Layout
{
    /// <summary>
    /// The scratch Yoga node a styling pass writes Yoga instructions into, before the result is copied to the styled node.
    /// Each pass starts from the style of a pristine template, which is the Yoga default for Fu,
    /// so no default is written by hand and a removed instruction falls back on its own.
    /// </summary>
    /// <remarks>
    /// A pass runs synchronously, so all nodes on a thread share one stage. An instruction must not start
    /// another node's pass. The template and the stages are never freed; there is one per process and one per thread.
    /// </remarks>
    internal static unsafe class YogaStyleStage
    {
        // Created with the default config so the template carries the web defaults every Fu node starts with.
        private static readonly nint s_template = (nint)YGNode.New(YogaConfigCache.Acquire(YogaConfig.Default));

        [ThreadStatic]
        private static nint t_stage;

        /// <summary>
        /// The stage of the current thread, created on first use.
        /// </summary>
        public static YGNode* Node
        {
            get
            {
                if (t_stage == 0)
                    t_stage = (nint)YGNode.New(YogaConfigCache.Acquire(YogaConfig.Default));

                return (YGNode*)t_stage;
            }
        }

        /// <summary>
        /// A node that was never styled. Its style is what a styling pass starts from.
        /// </summary>
        public static YGNode* Template => (YGNode*)s_template;

        /// <summary>
        /// Gives the stage of the current thread the template's style, discarding the previous pass.
        /// </summary>
        public static void Reset() => Node->CopyStyle(Template);
    }
}
