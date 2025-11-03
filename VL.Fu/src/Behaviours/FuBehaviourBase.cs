using VL.Core.Import;
using VL.Fu.Core;
using VL.Fu.Core.Helpers;

namespace VL.Fu.Behaviours
{
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public abstract class FuBehaviourBase : IFuBehaviour
    {
        protected CachedProperty<int> _priority = new(Common.BehaviourPriority.Low);
        public int Priority => _priority.Value;

        [Fragment]
        public virtual void SetPriority(int priority = Common.BehaviourPriority.Low) =>
            _priority.TrySetValue(priority);

        protected CachedProperty<bool> _enabled = new(true);

        public bool IsEnabled => _enabled.Value;

        [Fragment]
        public void SetEnabled(bool enabled = true) => _enabled.TrySetValue(enabled);

        public abstract bool TryActivate(
            IFuNode node,
            IReadOnlyList<FuCursor> cursors,
            IReadOnlyList<FuKey> keys,
            IReadOnlyList<FuKey> modifiers
        );
        public abstract bool TryAdvance(
            IFuNode node,
            IReadOnlyList<FuCursor> cursors,
            IReadOnlyList<FuKey> keys,
            IReadOnlyList<FuKey> modifiers
        );

        [Fragment]
        public IFuBehaviour Output => this;
    }
}
