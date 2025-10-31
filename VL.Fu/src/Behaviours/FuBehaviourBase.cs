using VL.Core.Import;
using VL.Fu.Core;
using VL.Fu.Core.Helpers;

namespace VL.Fu.Behaviours
{
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public abstract class FuBehaviourBase : IFuBehaviour
    {
        protected readonly CachedProperty<int> _priority = new(Common.BehaviourPriority.Low);
        public int Priority => _priority.Value;

        [Fragment]
        public void SetPriority(int priority = Common.BehaviourPriority.Low) =>
            _priority.TrySetValue(priority);

        protected readonly CachedProperty<bool> _isEnabled = new(true);
        public bool IsEnabled => _isEnabled.Value;

        [Fragment]
        public void SetEnabled(bool enabled = true) => _isEnabled.TrySetValue(enabled);

        public abstract bool TryActivate(
            IFuNode node,
            IEnumerable<FuCursor> cursors,
            IEnumerable<FuKey> keys,
            IEnumerable<FuKey> modifiers
        );

        public abstract FuNodeState Evaluate(
            IFuNode node,
            IEnumerable<FuCursor> cursors,
            IEnumerable<FuKey> keys,
            IEnumerable<FuKey> modifiers
        );

        [Fragment]
        public IFuBehaviour Output => this;
    }
}
