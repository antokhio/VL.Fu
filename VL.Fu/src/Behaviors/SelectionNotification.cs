using SkiaSharp;
using Stride.Core.Mathematics;
using VL.Fu.Core.Interaction;
using VL.Lib.IO;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Behaviors
{
    /// <summary>
    /// What a <see cref="SelectionNotification"/> asks the items of its provider to do.
    /// </summary>
    public enum SelectionAction
    {
        /// <summary>
        /// Every item deselects.
        /// </summary>
        Clear,

        /// <summary>
        /// The target selects and every other item deselects.
        /// </summary>
        Replace,

        /// <summary>
        /// The target selects; the others keep their state.
        /// </summary>
        Add,

        /// <summary>
        /// The target flips its state; the others keep theirs.
        /// </summary>
        Toggle,

        /// <summary>
        /// The items inside the marquee area select. Shift adds them, Ctrl flips them, and without either
        /// exactly the items inside end up selected.
        /// </summary>
        Marquee,

        /// <summary>
        /// A query: selected items include their bounds, and nothing changes.
        /// </summary>
        CollectBounds,
    }

    /// <summary>
    /// Sent by a <see cref="SelectionProvider"/> into its subtree to change or ask about the selection (v2 §7.1).
    /// Each item acts only when the provider is its nearest one, see <see cref="Concerns"/>, so the items of a nested provider stay out of it.
    /// </summary>
    /// <remarks>
    /// A patched item takes part like <see cref="Selectable"/>: it checks <see cref="Concerns"/> with its node, applies <see cref="Action"/>
    /// to its own selected state, and for <see cref="SelectionAction.CollectBounds"/> calls <see cref="IncludeBounds"/> while selected.
    /// </remarks>
    public sealed class SelectionNotification : NotificationBase
    {
        private readonly SelectionProvider _provider;
        private readonly SelectionAction _action;
        private readonly IInteractive? _target;
        private readonly RectangleF _area;
        private readonly SKMatrix? _windowToProvider;
        private readonly BoundsAccumulator? _bounds;

        /// <param name="provider">The provider that sends it.</param>
        /// <param name="action">What the items do.</param>
        /// <param name="target">The item node for <see cref="SelectionAction.Replace"/>, <see cref="SelectionAction.Add"/> and <see cref="SelectionAction.Toggle"/>.</param>
        /// <param name="area">The marquee area in the provider's space, for <see cref="SelectionAction.Marquee"/>.</param>
        /// <param name="windowToProvider">Maps window pixels into the provider's space, for the item bounds.</param>
        /// <param name="modifierKeys">The modifier keys of the input that caused it.</param>
        internal SelectionNotification(
            SelectionProvider provider,
            SelectionAction action,
            IInteractive? target,
            RectangleF area,
            SKMatrix? windowToProvider,
            Keys modifierKeys
        )
            : this(
                provider,
                action,
                target,
                area,
                windowToProvider,
                action == SelectionAction.CollectBounds ? new BoundsAccumulator() : null,
                modifierKeys,
                provider
            ) { }

        private SelectionNotification(
            SelectionProvider provider,
            SelectionAction action,
            IInteractive? target,
            RectangleF area,
            SKMatrix? windowToProvider,
            BoundsAccumulator? bounds,
            Keys modifierKeys,
            object sender
        )
            : base(sender, modifierKeys)
        {
            _provider = provider;
            _action = action;
            _target = target;
            _area = area;
            _windowToProvider = windowToProvider;
            _bounds = bounds;
        }

        public SelectionProvider Provider => _provider;

        public SelectionAction Action => _action;

        /// <summary>
        /// The item node that <see cref="SelectionAction.Replace"/>, <see cref="SelectionAction.Add"/> and
        /// <see cref="SelectionAction.Toggle"/> are about. Null for the other actions.
        /// </summary>
        public IInteractive? Target => _target;

        /// <summary>
        /// The marquee area in the provider's node space. Empty for every action but <see cref="SelectionAction.Marquee"/>.
        /// </summary>
        public RectangleF Area => _area;

        /// <summary>
        /// The union of the bounds the selected items included. Null when none did.
        /// </summary>
        internal RectangleF? CollectedBounds => _bounds?.Value;

        /// <summary>
        /// Whether <see cref="Provider"/> is the nearest provider above <paramref name="item"/>, so the item should act on this notification.
        /// </summary>
        public bool Concerns(IInteractive item) =>
            SelectionProvider.TryFind(item, out var provider, out _) && ReferenceEquals(provider, _provider);

        /// <summary>
        /// The box of the node receiving this notification, from its top-left corner to its layout size, as axis-aligned bounds
        /// in the provider's node space (S8). False while the node or the provider is scaled to zero.
        /// </summary>
        public bool TryGetItemBounds(out RectangleF bounds)
        {
            bounds = default;
            if (
                Sender is not FuNotificationSender { WindowToNodeMatrix: { } windowToItem } sender
                || _windowToProvider is not { } windowToProvider
                || !windowToItem.TryInvert(out var itemToWindow)
            )
                return false;

            var layout = sender.Node.Layout;
            var itemToProvider = itemToWindow.PostConcat(windowToProvider);
            var mapped = itemToProvider.MapRect(SKRect.Create(0f, 0f, layout.Width, layout.Height));
            bounds = new RectangleF(mapped.Left, mapped.Top, mapped.Width, mapped.Height);
            return true;
        }

        /// <summary>
        /// Adds <paramref name="bounds"/>, in the provider's node space, to the selection bounds the provider collects.
        /// Only <see cref="SelectionAction.CollectBounds"/> collects; for the other actions, and for bounds that aren't finite, it does nothing.
        /// </summary>
        public void IncludeBounds(RectangleF bounds) => _bounds?.Include(bounds);

        /// <summary>
        /// Whether <paramref name="outer"/> fully contains <paramref name="inner"/> (C3). Touching edges count as inside.
        /// </summary>
        internal static bool Contains(RectangleF outer, RectangleF inner) =>
            inner.Left >= outer.Left && inner.Top >= outer.Top && inner.Right <= outer.Right && inner.Bottom <= outer.Bottom;

        public override INotification WithSender(object sender) =>
            new SelectionNotification(_provider, _action, _target, _area, _windowToProvider, _bounds, ModifierKeys, sender);

        public override INotification Transform(INotificationSpaceTransformer transformer) => this;

        /// <summary>
        /// Shared by every copy <see cref="WithSender"/> makes, so the provider reads what all items included.
        /// </summary>
        private sealed class BoundsAccumulator
        {
            private RectangleF? _value;

            public RectangleF? Value => _value;

            public void Include(RectangleF bounds)
            {
                if (!float.IsFinite(bounds.X) || !float.IsFinite(bounds.Y) || !float.IsFinite(bounds.Width) || !float.IsFinite(bounds.Height))
                    return;

                if (_value is not { } current)
                {
                    _value = bounds;
                    return;
                }

                var left = MathF.Min(current.Left, bounds.Left);
                var top = MathF.Min(current.Top, bounds.Top);
                var right = MathF.Max(current.Right, bounds.Right);
                var bottom = MathF.Max(current.Bottom, bounds.Bottom);
                _value = new RectangleF(left, top, right - left, bottom - top);
            }
        }
    }
}
