using SkiaSharp;
using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Fu.Core.Base;
using VL.Fu.Core.Constants;
using VL.Fu.Core.Extensions;
using VL.Fu.Core.Interaction;
using VL.Fu.Core.Interaction.Recognizers;
using VL.Lib.IO.Notifications;
using VL.Lib.Reactive;
using VL.Model;
using VL.Skia;

namespace VL.Fu.Behaviors
{
    /// <summary>
    /// Lets the user move, scale and turn the node it is attached to: one pointer drags it, two touches pinch it.
    /// Connect a channel to <see cref="Transformation"/> and apply it to the node's transformation: the gestures write it,
    /// and the patch may write it too.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A drag moves the node by how far the pointer moved since the press. A pinch scales and turns it about the point between the
    /// touches and moves it with that point, so the content under the touches stays under them whatever the node's transform origin is.
    /// <see cref="SetAllow"/> leaves out any of the three. A second touch going down during a drag turns it into a pinch, which carries
    /// on from where the drag left the node. Lifting one touch ends the pinch; the other one does nothing until a new press.
    /// </para>
    /// <para>
    /// When the node is selected under an enabled <see cref="SelectionProvider"/>, the gestures transform every selected item of that
    /// provider together, about the same point on screen, and the gestures of other selected items transform this one along.
    /// </para>
    /// <para>
    /// When a gesture is cancelled, because another gesture took the pointer, Esc was pressed, the pointer was lost or the behavior
    /// was disabled, <see cref="Transformation"/> returns to its value at the start of the gesture. Use either <see cref="Movable"/>
    /// or <see cref="Transformable"/> on a node, not both.
    /// </para>
    /// </remarks>
    [ProcessNode(Name = "Transformable", HasStateOutput = true, FragmentSelection = FragmentSelection.Explicit)]
    public sealed class Transformable : NodeBase, IBehavior
    {
        private readonly DragGestureRecognizer _drag = new();
        private readonly PinchGestureRecognizer _pinch = new();
        private readonly int[] _pointers = new int[2];

        private IChannel<Matrix>? _transformation = null;
        private bool _allowTranslate = true;
        private bool _allowScale = true;
        private bool _allowRotate = true;
        private bool _enabled = true;

        private bool _dragGranted = false;
        private bool _pinchGranted = false;
        private int? _owedRelease = null;
        private int? _owedSecond = null;
        private Matrix _matrixAtStart = Matrix.Identity;
        private Vector2 _pivotAtStart = Vector2.Zero;
        private Vector2 _pressInWindow = Vector2.Zero;
        private Vector2 _lastInWindow = Vector2.Zero;
        private SKMatrix _pinchWindowToParent = SKMatrix.Identity;
        private SKMatrix _pinchParentToWindow = SKMatrix.Identity;
        private bool _warnedMovable = false;

        private GroupTransaction? _drive = null;
        private GroupTransaction? _owedGroupCancel = null;
        private GroupTransaction? _member = null;
        private Matrix _matrixAtBegin = Matrix.Identity;
        private Vector2 _pivotAtBegin = Vector2.Zero;

        [Fragment]
        public Transformable([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
            : base(nodeContext) { }

        /// <summary>
        /// The node's transformation, about its transform origin.
        /// </summary>
        public IChannel<Matrix>? Transformation => _transformation;

        /// <summary>
        /// Whether the press found a <see cref="Movable"/> on the same node and warned about it (M1).
        /// It warns once per behavior.
        /// </summary>
        internal bool WarnedMovable => _warnedMovable;

        /// <summary>
        /// Connects the channel that holds the transformation. Changing it during a gesture cancels the gesture,
        /// which writes the value at its start to the previous channel.
        /// </summary>
        [Fragment(Order = PinOrder.Behavior)]
        public void SetTransformation(IChannel<Matrix>? transformation)
        {
            if (ReferenceEquals(transformation, _transformation))
                return;

            CancelGesture();
            _member = null;
            _transformation = transformation;
        }

        public bool AllowTranslate => _allowTranslate;

        public bool AllowScale => _allowScale;

        public bool AllowRotate => _allowRotate;

        /// <summary>
        /// Chooses what the gestures change. Without translate, a drag does nothing and a pinch keeps the point it started about
        /// in place. A change applies to the next move of a running gesture.
        /// </summary>
        [Fragment(Order = PinOrder.Behavior)]
        public void SetAllow(bool translate = true, bool scale = true, bool rotate = true)
        {
            _allowTranslate = translate;
            _allowScale = scale;
            _allowRotate = rotate;
        }

        public bool Enabled => _enabled;

        /// <summary>
        /// Disabling cancels a gesture at once, writing the value at its start, and lets go of the pointers with the next notification
        /// of one of them the node receives. A disabled node stops following a group transform and keeps its transformation.
        /// </summary>
        [Fragment(Order = PinOrder.Enabled)]
        public void SetEnabled(bool enabled = true)
        {
            if (enabled == _enabled)
                return;

            if (!enabled)
            {
                CancelGesture();
                _member = null;
            }

            _enabled = enabled;
        }

        public bool Notify(INotification notification, CallerInfo caller)
        {
            SettleOwed(notification);

            if (!_enabled)
                return false;

            switch (notification)
            {
                case GroupManipulationNotification group:
                    OnGroup(group);
                    return false;
                case PointerCaptureNotification capture when capture.Grants(this):
                    return capture.Claim.Kind == GestureKind.Pinch ? OnPinchGranted(capture) : OnDragGranted(capture);
                case PointerCancelNotification cancel:
                    OnCancelled(cancel);
                    return false;
            }

            if (!PointerInput.TryRead(notification, out var sample))
                return false;

            if (DropCancelledGroup(notification, sample.PointerId))
                return false;

            var pinch = _pinch.Process(in sample);
            switch (pinch.Phase)
            {
                case PinchPhase.Pressed:
                    if (sample.WindowToParent is { } windowToParent && windowToParent.TryInvert(out var parentToWindow))
                    {
                        _pinchWindowToParent = windowToParent;
                        _pinchParentToWindow = parentToWindow;
                    }
                    break;
                case PinchPhase.Paired:
                    // The pinch carries on from what the drag wrote, so it starts from here.
                    EndDrag(notification);
                    Freeze(sample.Node);
                    return false;
                case PinchPhase.Started:
                    _pointers[0] = pinch.FirstPointerId;
                    _pointers[1] = pinch.SecondPointerId;
                    notification.RequestPointerCapture(this, GestureKind.Pinch, _pointers);
                    return false;
                case PinchPhase.Moved when _pinchGranted:
                    PublishPinch(notification, in pinch);
                    return true;
                case PinchPhase.Ended when _pinchGranted:
                    _pinchGranted = false;
                    PublishPinch(notification, in pinch);
                    EndGroup(notification);
                    // The other touch is still down, but this pinch is over: let it go back to the nodes it was pressed on.
                    notification.ReleasePointerCapture(this);
                    return true;
            }

            // While two touches pinch, a third one doesn't drag.
            if (_pinch.Current.Phase is not (PinchPhase.Idle or PinchPhase.Pressed))
                return false;

            var drag = _drag.Process(in sample);
            switch (drag.Phase)
            {
                case DragPhase.Pressed:
                    _pressInWindow = sample.PositionInWindow;
                    _lastInWindow = sample.PositionInWindow;
                    Freeze(sample.Node);
                    WarnIfMovable(sample.Node);
                    return false;
                case DragPhase.Started:
                    _lastInWindow = sample.PositionInWindow;
                    if (!_allowTranslate)
                    {
                        // Nothing to move: leave the pointer to other gestures.
                        _drag.Cancel();
                        return false;
                    }
                    notification.RequestPointerCapture(this, GestureKind.Drag);
                    return false;
                case DragPhase.Moved when _dragGranted:
                    _lastInWindow = sample.PositionInWindow;
                    PublishDrag(notification, in drag);
                    return true;
                case DragPhase.Ended when _dragGranted:
                    _lastInWindow = sample.PositionInWindow;
                    _dragGranted = false;
                    PublishDrag(notification, in drag);
                    EndGroup(notification);
                    return true;
                default:
                    return false;
            }
        }

        private bool OnDragGranted(PointerCaptureNotification capture)
        {
            var current = _drag.Current;
            if (
                current.Phase is not (DragPhase.Started or DragPhase.Moved)
                || !TryStartGroup(capture, current.PointerId)
            )
            {
                // The drag was cancelled after it asked, or its group is transformed already: the pointer is of no use.
                capture.ReleasePointerCapture(this);
                _drag.Cancel();
                return false;
            }

            _dragGranted = true;
            PublishDrag(capture, in current);
            return true;
        }

        private bool OnPinchGranted(PointerCaptureNotification capture)
        {
            var current = _pinch.Current;
            if (
                current.Phase is not (PinchPhase.Started or PinchPhase.Moved)
                || !TryStartGroup(capture, current.FirstPointerId)
            )
            {
                // The pinch was cancelled after it asked, or its group is transformed already: the touches are of no use.
                capture.ReleasePointerCapture(this);
                _pinch.Cancel();
                return false;
            }

            _pinchGranted = true;
            PublishPinch(capture, in current);
            return true;
        }

        private bool TryStartGroup(PointerCaptureNotification capture, int pointerId)
        {
            if (
                capture.Sender is not FuNotificationSender { Node: var node }
                || !GroupTransaction.TryStart(this, pointerId, node, out var transaction)
            )
                return false;

            if (transaction is not null)
            {
                _drive = transaction;
                transaction.Begin(capture);
            }

            return true;
        }

        /// <summary>
        /// The drag translates by the pointer's offset in the parent's space, or in window pixels for a group.
        /// </summary>
        private void PublishDrag(INotification notification, in DragGestureState drag)
        {
            if (_drive is { } transaction)
            {
                var delta = _allowTranslate ? _lastInWindow - _pressInWindow : Vector2.Zero;
                transaction.Apply(notification, SKMatrix.CreateTranslation(delta.X, delta.Y));
                return;
            }

            var offset = _allowTranslate ? drag.OffsetInParent : Vector2.Zero;
            _transformation?.EnsureValue(Compose(in _matrixAtStart, _pivotAtStart, SKMatrix.CreateTranslation(offset.X, offset.Y)));
        }

        /// <summary>
        /// The pinch turns and scales about the center at pairing and moves it to the current center:
        /// <c>T(center) · R · S · T(−centerAtPair)</c> in the parent's space, mapped into window pixels for a group.
        /// </summary>
        private void PublishPinch(INotification notification, in PinchGestureState pinch)
        {
            var atPair = pinch.CenterInParent - pinch.TranslationInParent;
            var center = _allowTranslate ? pinch.CenterInParent : atPair;
            var scale = _allowScale ? pinch.Scale : 1f;
            var rotation = _allowRotate ? pinch.Rotation : 0f;
            var delta = SKMatrix
                .CreateTranslation(center.X, center.Y)
                .PreConcat(SKMatrix.CreateRotation(rotation))
                .PreConcat(SKMatrix.CreateScale(scale, scale))
                .PreConcat(SKMatrix.CreateTranslation(-atPair.X, -atPair.Y));

            if (_drive is { } transaction)
            {
                transaction.Apply(notification, GestureMath.Conjugate(in delta, in _pinchParentToWindow, in _pinchWindowToParent));
                return;
            }

            _transformation?.EnsureValue(Compose(in _matrixAtStart, _pivotAtStart, in delta));
        }

        private void OnGroup(GroupManipulationNotification group)
        {
            switch (group.Phase)
            {
                case GroupPhase.Begin:
                    // A node transformed alone by its own gesture stays out.
                    if ((_dragGranted || _pinchGranted) && _drive is null)
                        return;

                    if (group.Sender is FuNotificationSender { Node: var node } && group.Concerns(node) && group.Join(this))
                    {
                        _member = group.Transaction;
                        _matrixAtBegin = _transformation?.Value ?? Matrix.Identity;
                        _pivotAtBegin = PivotOf(node);
                    }
                    return;
                case GroupPhase.Apply when ReferenceEquals(_member, group.Transaction):
                    if (group.TryGetDelta(this, out var delta))
                        _transformation?.EnsureValue(Compose(in _matrixAtBegin, _pivotAtBegin, in delta));
                    return;
                case GroupPhase.End when ReferenceEquals(_member, group.Transaction):
                    _member = null;
                    return;
                case GroupPhase.Cancel when ReferenceEquals(_member, group.Transaction):
                    _member = null;
                    _transformation?.EnsureValue(_matrixAtBegin);
                    return;
            }
        }

        /// <summary>
        /// A gesture that published resets, unless this behavior is a member of the claim. A pending one resets unless a weaker
        /// gesture holds the pointer, so a pinch waiting for its second touch survives a child's drag.
        /// </summary>
        private void OnCancelled(PointerCancelNotification cancel)
        {
            if (cancel.IsMember(this))
                return;

            var drag = _drag.Current;
            var dragTracks = drag.Phase != DragPhase.Idle && drag.PointerId == cancel.PointerId;
            var pinchTracks = PinchTracks(cancel.PointerId);
            if ((dragTracks && _dragGranted) || (pinchTracks && _pinchGranted))
            {
                // The claim was taken away or ended, so there is nothing to let go.
                Reset(cancel, owesRelease: false);
                return;
            }

            if (dragTracks && !cancel.SparesPending(GestureKind.Drag))
                _drag.Cancel();

            if (pinchTracks && !cancel.SparesPending(GestureKind.Pinch))
                _pinch.Cancel();
        }

        private bool PinchTracks(int pointerId)
        {
            var current = _pinch.Current;
            return current.Phase switch
            {
                PinchPhase.Idle => false,
                PinchPhase.Pressed => current.FirstPointerId == pointerId,
                _ => current.FirstPointerId == pointerId || current.SecondPointerId == pointerId,
            };
        }

        /// <summary>
        /// The second touch went down: a drag that published ends and keeps what it wrote, including its group's values.
        /// Its claim stays until the pinch's claim takes the touches over.
        /// </summary>
        private void EndDrag(INotification notification)
        {
            if (_dragGranted)
            {
                _dragGranted = false;
                EndGroup(notification);
            }

            _drag.Cancel();
        }

        private void EndGroup(INotification notification)
        {
            if (_drive is not { } transaction)
                return;

            _drive = null;
            transaction.End(notification);
        }

        /// <summary>
        /// The provider was disabled while a gesture of this node transformed its group: the group is cancelled with this input
        /// of one of the gesture's pointers, and the gesture lets go of them.
        /// </summary>
        private bool DropCancelledGroup(INotification notification, int pointerId)
        {
            if (_drive is not { IsCancelled: true } transaction)
                return false;

            var drag = _drag.Current;
            if (!(drag.Phase != DragPhase.Idle && drag.PointerId == pointerId) && !PinchTracks(pointerId))
                return false;

            _drive = null;
            _dragGranted = false;
            _pinchGranted = false;
            _drag.Cancel();
            _pinch.Cancel();
            transaction.Cancel(notification);
            notification.ReleasePointerCapture(this);
            return true;
        }

        /// <summary>
        /// Cancels the gesture from a setter, which has no notification to let go of the pointers with.
        /// The release, and the cancel of a group it drives, are owed until the next notification of one of them (D4).
        /// </summary>
        private void CancelGesture() => Reset(null, owesRelease: true);

        /// <summary>
        /// Returns to idle. A gesture that transformed the node alone writes its idle fact: the value at its start.
        /// A group it drives is cancelled with <paramref name="notification"/>, or owed without one.
        /// </summary>
        private void Reset(INotification? notification, bool owesRelease)
        {
            if (_dragGranted || _pinchGranted)
            {
                if (owesRelease)
                {
                    var pinch = _pinch.Current;
                    _owedRelease = _pinchGranted ? pinch.FirstPointerId : _drag.Current.PointerId;
                    _owedSecond = _pinchGranted ? pinch.SecondPointerId : null;
                }

                if (_drive is { } transaction)
                {
                    _drive = null;
                    if (notification is not null)
                        transaction.Cancel(notification);
                    else
                        _owedGroupCancel = transaction;
                }
                else
                {
                    _transformation?.EnsureValue(_matrixAtStart);
                }
            }

            _dragGranted = false;
            _pinchGranted = false;
            _drag.Cancel();
            _pinch.Cancel();
        }

        // A pinch's claim holds both touches, so releasing through either of them lets go of both.
        private void SettleOwed(INotification notification)
        {
            if (_owedRelease is not { } first || !PointerIds.TryGet(notification, out var id))
                return;

            if (id != first && id != _owedSecond)
                return;

            _owedRelease = null;
            _owedSecond = null;
            if (_owedGroupCancel is { } transaction)
            {
                _owedGroupCancel = null;
                transaction.Cancel(notification);
            }

            notification.ReleasePointerCapture(this);
        }

        /// <summary>
        /// Remembers the transformation and the pivot a gesture starts from.
        /// </summary>
        private void Freeze(IInteractive node)
        {
            _matrixAtStart = _transformation?.Value ?? Matrix.Identity;
            _pivotAtStart = PivotOf(node);
        }

        /// <summary>
        /// <paramref name="matrix"/> changed by <paramref name="deltaInParent"/>, which works in the parent's space,
        /// compensated for the pivot the transformation applies about (v1 §7.3): <c>P⁻¹ · D · P · M</c>.
        /// Z stays as it was.
        /// </summary>
        private static Matrix Compose(in Matrix matrix, Vector2 pivotInParent, in SKMatrix deltaInParent)
        {
            var aboutPivot = SKMatrix
                .CreateTranslation(-pivotInParent.X, -pivotInParent.Y)
                .PreConcat(deltaInParent)
                .PreConcat(SKMatrix.CreateTranslation(pivotInParent.X, pivotInParent.Y));

            // Stride applies the left matrix first: the transformation, then the change.
            return matrix * aboutPivot.ToMatrix();
        }

        private static Vector2 PivotOf(IInteractive node) =>
            node is IInteractionNode interaction ? interaction.TransformPivotInParent : new Vector2(node.Layout.X, node.Layout.Y);

        private void WarnIfMovable(IInteractive node)
        {
            if (_warnedMovable)
                return;

            var behaviors = node.Behaviors;
            for (var i = 0; i < behaviors.Count; i++)
            {
                if (behaviors[i] is Movable)
                {
                    _warnedMovable = true;
                    this.LogWarning("This node also has a Movable. Both move it, so use one of them.");
                    return;
                }
            }
        }
    }
}
