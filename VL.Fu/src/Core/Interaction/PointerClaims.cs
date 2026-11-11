using VL.Lib.IO;
using VL.Lib.IO.Notifications;
using VL.Skia;

namespace VL.Fu.Core.Interaction
{
    /// <summary>
    /// A claim request made during a pass (v2 §5.2 step 1). <paramref name="Pointers"/> starts with the pointer of the notification
    /// it was made with.
    /// </summary>
    internal readonly record struct ClaimRequest(object Owner, IInteractive Node, GestureKind Kind, IReadOnlyList<int> Pointers);

    /// <summary>
    /// Which route a pointer notification takes, chosen when its pass starts.
    /// </summary>
    internal enum PointerRouteChoice
    {
        /// <summary>
        /// The nodes under its position: the pointer isn't pressed, or this is the press.
        /// </summary>
        Hit,

        /// <summary>
        /// The path the pointer was pressed on, bubbling like a hit path.
        /// </summary>
        Recorded,

        /// <summary>
        /// The claim node only; its ancestors forward.
        /// </summary>
        Claim,
    }

    /// <summary>
    /// A mouse or a touch between its press and its release: the buttons held, the path it was pressed on, and its claim.
    /// </summary>
    internal sealed class PressedPointer
    {
        private readonly int _id;
        private readonly List<IInteractive> _path = new();

        public PressedPointer(int id)
        {
            _id = id;
        }

        public int Id => _id;

        /// <summary>
        /// The mouse buttons held, for the mouse. The press ends when the last one goes up (O1).
        /// </summary>
        public MouseButtons Buttons { get; set; }

        /// <summary>
        /// The hit path of the press, root first. Empty when the press hit nothing.
        /// </summary>
        public List<IInteractive> Path => _path;

        public PointerClaim? Claim { get; set; }
    }

    /// <summary>
    /// The pressed pointers of one root and their claims (v2 §5). Chooses the route of pointer input, records paths at the press,
    /// resolves the claim requests of a pass, and ends claims and paths when their time is over (§5.4).
    /// </summary>
    /// <remarks>
    /// It never calls behaviors: everything it wants delivered, captures and cancels, is handed back to the dispatcher as
    /// pending notifications, which it queues or runs before the next pass. Gesture sequences have recorded paths, but they
    /// aren't pressed pointers and can't be claimed.
    /// </remarks>
    internal sealed class PointerClaims
    {
        private readonly List<PressedPointer> _pressed = new();
        private readonly Dictionary<int, IInteractive[]> _gestures = new();
        private readonly List<(IInteractive Node, IBehavior Behavior)> _removed = new();
        private readonly List<PointerClaim> _granted = new();
        private readonly List<(PointerClaim Claim, object Owner)> _joined = new();
        private readonly List<int> _changed = new();
        private readonly List<IInteractive> _buffer = new();
        private IInteractive[]? _clickPath;

        /// <summary>
        /// Whether a mouse button or a touch is down.
        /// </summary>
        public bool HasPressed => _pressed.Count > 0;

        public IReadOnlyList<PressedPointer> Pressed => _pressed;

        public PressedPointer? Find(int pointerId)
        {
            for (var i = 0; i < _pressed.Count; i++)
            {
                if (_pressed[i].Id == pointerId)
                    return _pressed[i];
            }

            return null;
        }

        public PointerClaim? ClaimOf(int pointerId) => Find(pointerId)?.Claim;

        /// <summary>
        /// Chooses where pointer input goes (v2 §3.1): the claim node while the pointer is claimed, the recorded path while it is
        /// pressed, the Up's path for the click VL injects after it, a gesture's recorded path after its Begin; otherwise a hit path.
        /// </summary>
        public PointerRouteChoice Choose(
            INotification notification,
            out IInteractive? claimNode,
            out IReadOnlyList<IInteractive>? path
        )
        {
            claimNode = null;
            path = null;

            switch (notification)
            {
                case MouseClickNotification:
                    if (_clickPath is null)
                        return PointerRouteChoice.Hit;

                    path = _clickPath;
                    return PointerRouteChoice.Recorded;

                case MouseNotification:
                    return ChooseFor(Find(PointerIds.Mouse), out claimNode, out path);

                case TouchNotification touch:
                    return touch.Kind == TouchNotificationKind.TouchDown
                        ? PointerRouteChoice.Hit
                        : ChooseFor(Find(touch.Id), out claimNode, out path);

                case GestureNotification gesture:
                    if (gesture.Kind == GestureNotificationKind.GestureBegin || !_gestures.TryGetValue(gesture.SequenceId, out var recorded))
                        return PointerRouteChoice.Hit;

                    path = recorded;
                    return PointerRouteChoice.Recorded;

                default:
                    return PointerRouteChoice.Hit;
            }
        }

        /// <summary>
        /// Ends the presses a notification makes obsolete before it is delivered (§5.4 row 4): a lost mouse ends the mouse's press,
        /// a lost focus ends every press, and a touch going down with the id of one still pressed ends that one, whose Up was lost.
        /// Their cancels go to <paramref name="output"/>, to run before the notification.
        /// </summary>
        public void Prelude(INotification notification, List<PendingNotification> output)
        {
            var sender = notification.Sender;
            var modifiers = ModifiersOf(notification);

            switch (notification)
            {
                case MouseLostNotification:
                    if (Find(PointerIds.Mouse) is { } mouse)
                        Drop(mouse, output, sender, modifiers);
                    _clickPath = null;
                    break;

                case LostFocusNotification:
                    while (_pressed.Count > 0)
                        Drop(_pressed[0], output, sender, modifiers);
                    _gestures.Clear();
                    _clickPath = null;
                    break;

                case TouchNotification { Kind: TouchNotificationKind.TouchDown } touch:
                    if (Find(touch.Id) is { } stale)
                        Drop(stale, output, sender, modifiers);
                    break;
            }
        }

        /// <summary>
        /// The bookkeeping after a pass (v2 §2.2 steps 3–4): records the path at a press, releases at the last Up (§5.4 row 1),
        /// cancels claims on an Esc nobody processed, then resolves the pass's requests. What it sends goes to <paramref name="output"/>,
        /// cancels before captures.
        /// </summary>
        /// <param name="choice">The route the pass's pointer notification took.</param>
        /// <param name="reached">Whether the route was built, so <see cref="InteractionPass.Route"/> holds it.</param>
        /// <param name="handled">Whether a receiver processed the notification.</param>
        public void AfterPass(
            InteractionPass pass,
            PointerRouteChoice choice,
            bool reached,
            bool handled,
            List<PendingNotification> output
        )
        {
            var notification = pass.Notification;
            var sender = notification.Sender;
            var modifiers = ModifiersOf(notification);
            IReadOnlyList<IInteractive> hitPath =
                choice == PointerRouteChoice.Hit && reached ? pass.Route.Path : Array.Empty<IInteractive>();

            if (pass.Class == DeliveryClass.Input)
            {
                switch (notification)
                {
                    case MouseDownNotification down:
                        var pressed = Find(PointerIds.Mouse);
                        if (pressed is null)
                        {
                            pressed = Press(PointerIds.Mouse, hitPath);
                            _clickPath = null;
                        }

                        pressed.Buttons |= down.Buttons;
                        break;

                    case MouseUpNotification up when Find(PointerIds.Mouse) is { } mouse:
                        mouse.Buttons &= ~up.Buttons;
                        if (mouse.Buttons == MouseButtons.None)
                        {
                            _clickPath = mouse.Path.ToArray();
                            Release(mouse, handled, output, sender, modifiers);
                        }
                        break;

                    case TouchNotification { Kind: TouchNotificationKind.TouchDown } touchDown:
                        Press(touchDown.Id, hitPath);
                        break;

                    case TouchNotification { Kind: TouchNotificationKind.TouchUp } touchUp when Find(touchUp.Id) is { } touch:
                        Release(touch, handled, output, sender, modifiers);
                        break;

                    case GestureNotification { Kind: GestureNotificationKind.GestureBegin } begin:
                        _gestures[begin.SequenceId] = hitPath.ToArray();
                        break;

                    case GestureNotification { Kind: GestureNotificationKind.GestureEnd } end:
                        _gestures.Remove(end.SequenceId);
                        break;

                    case KeyDownNotification { KeyCode: Keys.Escape } when !handled:
                        CancelClaims(output, sender, modifiers);
                        break;
                }
            }

            Resolve(pass.Requests, output, sender, modifiers);
        }

        /// <summary>
        /// Why <paramref name="node"/> can't request <paramref name="pointerId"/> in <paramref name="pass"/>, or null when it can:
        /// the pointer must be pressed, or being pressed by this pass, and the node must be on the path it was pressed on.
        /// </summary>
        public string? CheckRequest(InteractionPass pass, int pointerId, IInteractive node)
        {
            if (Find(pointerId) is { } pressed)
            {
                return IndexOf(pressed.Path, node) >= 0
                    ? null
                    : $"pointer {pointerId} was pressed on another node, so this node can't claim it";
            }

            if (IsPress(pass.Notification, pointerId))
                return IndexOf(pass.Route.Path, node) >= 0 ? null : $"pointer {pointerId} isn't pressed on this node";

            return $"pointer {pointerId} isn't pressed";
        }

        /// <summary>
        /// Removes <paramref name="owner"/> from the claim on <paramref name="pointerId"/> (§5.4 row 2). A claim without members ends,
        /// and its pointers fall back to their recorded paths. Nobody is notified: the owner chose to leave.
        /// </summary>
        public void Release(int pointerId, object owner)
        {
            if (ClaimOf(pointerId) is not { } claim || !claim.RemoveMember(owner) || claim.Members.Count > 0)
                return;

            EndClaim(claim);
            _changed.Clear();
        }

        /// <summary>
        /// Remembers that <paramref name="behavior"/> was removed from <paramref name="node"/> while pointers were pressed.
        /// The next <see cref="Sweep"/> takes it out of every claim and sends it a cancel of its own (§5.4 row 6).
        /// </summary>
        public void NoteRemoved(IInteractive node, IBehavior behavior) => _removed.Add((node, behavior));

        /// <summary>
        /// Checks the pressed pointers against the tree as it is now (§5.4 rows 5 and 6), and sends the cancels this causes to
        /// <paramref name="output"/>. A recorded path is cut where it stops being reachable, and the nodes cut off get a cancel.
        /// A claim whose node is unreachable ends, with a cancel along the whole old path; a claim node that moved inside
        /// the root keeps its claim, and its path is rebuilt.
        /// </summary>
        public void Sweep(IInteractive root, List<PendingNotification> output)
        {
            if (_pressed.Count == 0 && _removed.Count == 0 && _gestures.Count == 0)
                return;

            SweepRemovedBehaviors(output);

            for (var i = 0; i < _pressed.Count; i++)
            {
                var pressed = _pressed[i];
                var path = pressed.Path;
                var valid = ValidPrefix(root, path);
                var claim = pressed.Claim;
                var at = claim is null ? -1 : IndexOf(path, claim.Node);

                if (claim is null || (at >= 0 && at < valid))
                {
                    CutTo(pressed, valid, output);
                }
                else if (NotificationRoute.TryFillPath(root, claim.Node, _buffer) && ValidPrefix(root, _buffer) == _buffer.Count)
                {
                    // The claim node moved inside the root: it keeps its claim, the nodes that dropped off its path are cancelled.
                    var common = CommonPrefix(path, _buffer);
                    var end = at >= 0 ? at : path.Count;
                    if (common < end)
                        AddCancel(output, pressed.Id, null, path, end, common, null, Keys.None);

                    path.Clear();
                    path.AddRange(_buffer);
                }
                else
                {
                    EndClaim(claim);
                    FlushChanged(output, null, Keys.None);
                    path.RemoveRange(valid, path.Count - valid);
                }
            }

            _buffer.Clear();
            SweepGestures(root);
        }

        /// <summary>
        /// How many nodes of <paramref name="path"/>, from the start, a hit test could reach now: the first is
        /// <paramref name="root"/>, each one is the parent of the next, and each is laid out, displayed and takes pointer events.
        /// </summary>
        public static int ValidPrefix(IInteractive root, IReadOnlyList<IInteractive> path)
        {
            var i = 0;
            for (; i < path.Count; i++)
            {
                var node = path[i];
                var linked = i == 0 ? ReferenceEquals(node, root) : ReferenceEquals(node.Parent, path[i - 1]);
                if (!linked || node is not IInteractionNode { IsHitTestable: true })
                    break;
            }

            return i;
        }

        private static PointerRouteChoice ChooseFor(
            PressedPointer? pressed,
            out IInteractive? claimNode,
            out IReadOnlyList<IInteractive>? path
        )
        {
            claimNode = null;
            path = null;

            if (pressed is null)
                return PointerRouteChoice.Hit;

            if (pressed.Claim is { } claim)
            {
                claimNode = claim.Node;
                return PointerRouteChoice.Claim;
            }

            path = pressed.Path;
            return PointerRouteChoice.Recorded;
        }

        private PressedPointer Press(int pointerId, IReadOnlyList<IInteractive> path)
        {
            var pressed = new PressedPointer(pointerId);
            for (var i = 0; i < path.Count; i++)
                pressed.Path.Add(path[i]);

            _pressed.Add(pressed);
            return pressed;
        }

        /// <summary>
        /// The pointer's last button or touch went up (§5.4 row 1). A processed Up sends a cancel along the path, carrying the claim
        /// so its members keep their facts, to reset ancestors whose Up never arrived. The pointer leaves its claim afterwards.
        /// </summary>
        /// <remarks>
        /// A claimed pointer's Up reaches the claim node only, so it sends the cancel even when nobody processed it:
        /// ancestors that kept a pending gesture under the claim would never hear of the Up otherwise.
        /// The claim has ended by the time the cancel is delivered, so nobody keeps a gesture pending under it.
        /// </remarks>
        private void Release(
            PressedPointer pressed,
            bool handled,
            List<PendingNotification> output,
            object? sender,
            Keys modifiers
        )
        {
            if (handled || pressed.Claim is not null)
                AddCancel(output, pressed.Id, pressed.Claim, pressed.Path, pressed.Path.Count, 0, sender, modifiers);

            _pressed.Remove(pressed);

            if (pressed.Claim is { } claim)
            {
                claim.RemovePointer(pressed.Id);
                if (claim.Pointers.Count == 0)
                    claim.End();
            }
        }

        /// <summary>
        /// Ends a press that was lost: everyone on its path resets, and the claim it was part of ends.
        /// The claim's other pointers fall back to their recorded paths, with a cancel each.
        /// </summary>
        private void Drop(PressedPointer pressed, List<PendingNotification> output, object? sender, Keys modifiers)
        {
            AddCancel(output, pressed.Id, null, pressed.Path, pressed.Path.Count, 0, sender, modifiers);
            _pressed.Remove(pressed);

            if (pressed.Claim is { } claim)
            {
                EndClaim(claim);
                FlushChanged(output, sender, modifiers);
            }
        }

        /// <summary>
        /// Ends every claim; the pointers stay pressed on their recorded paths.
        /// </summary>
        private void CancelClaims(List<PendingNotification> output, object? sender, Keys modifiers)
        {
            for (var i = 0; i < _pressed.Count; i++)
            {
                if (_pressed[i].Claim is { } claim)
                    EndClaim(claim);
            }

            FlushChanged(output, sender, modifiers);
        }

        /// <summary>
        /// Resolves the requests of a pass (v2 §5.2 step 2): the highest kind first, in pass order within a kind.
        /// Then queues a cancel along each pointer whose claim changed, and a capture for each new claim and each late join.
        /// </summary>
        private void Resolve(
            IReadOnlyList<ClaimRequest> requests,
            List<PendingNotification> output,
            object? sender,
            Keys modifiers
        )
        {
            if (requests.Count == 0)
                return;

            for (var kind = (int)GestureKind.Pinch; kind >= (int)GestureKind.Click; kind--)
            {
                for (var i = 0; i < requests.Count; i++)
                {
                    if ((int)requests[i].Kind == kind)
                        Resolve(requests[i]);
                }
            }

            FlushChanged(output, sender, modifiers);

            foreach (var claim in _granted)
                AddCapture(output, claim, claim.Members.ToArray(), sender, modifiers);

            foreach (var (claim, owner) in _joined)
                AddCapture(output, claim, new[] { owner }, sender, modifiers);

            _granted.Clear();
            _joined.Clear();
        }

        private void Resolve(ClaimRequest request)
        {
            var pointers = request.Pointers;
            for (var i = 0; i < pointers.Count; i++)
            {
                if (Find(pointers[i]) is null)
                    return;
            }

            // Same node and kind as a claim granted in this pass: join it.
            foreach (var granted in _granted)
            {
                if (IsSameClaim(granted, request))
                {
                    granted.AddMember(request.Owner);
                    return;
                }
            }

            // Same node and kind as the claim that holds the pointers already: a late join.
            if (ClaimOf(pointers[0]) is { } held && IsSameClaim(held, request) && HeldByOnly(held, pointers))
            {
                if (!held.HasMember(request.Owner))
                {
                    held.AddMember(request.Owner);
                    _joined.Add((held, request.Owner));
                }

                return;
            }

            // All or nothing: every pointer must be free or held by a lower kind.
            for (var i = 0; i < pointers.Count; i++)
            {
                if (ClaimOf(pointers[i]) is { } other && other.Kind >= request.Kind)
                    return;
            }

            var claim = new PointerClaim(request.Node, request.Kind, pointers, request.Owner);
            for (var i = 0; i < pointers.Count; i++)
            {
                var pressed = Find(pointers[i])!;
                if (pressed.Claim is { } replaced)
                    EndClaim(replaced);

                pressed.Claim = claim;
                MarkChanged(pressed.Id);
            }

            _granted.Add(claim);
        }

        private static bool IsSameClaim(PointerClaim claim, ClaimRequest request)
        {
            if (!ReferenceEquals(claim.Node, request.Node) || claim.Kind != request.Kind)
                return false;

            for (var i = 0; i < request.Pointers.Count; i++)
            {
                if (!claim.Holds(request.Pointers[i]))
                    return false;
            }

            return true;
        }

        private bool HeldByOnly(PointerClaim claim, IReadOnlyList<int> pointers)
        {
            for (var i = 0; i < pointers.Count; i++)
            {
                if (!ReferenceEquals(ClaimOf(pointers[i]), claim))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Ends <paramref name="claim"/> and frees its pointers, which are marked as changed for <see cref="FlushChanged"/>.
        /// </summary>
        private void EndClaim(PointerClaim claim)
        {
            claim.End();
            for (var i = 0; i < claim.Pointers.Count; i++)
            {
                if (Find(claim.Pointers[i]) is { } pressed && ReferenceEquals(pressed.Claim, claim))
                {
                    pressed.Claim = null;
                    MarkChanged(pressed.Id);
                }
            }
        }

        private void MarkChanged(int pointerId)
        {
            if (!_changed.Contains(pointerId))
                _changed.Add(pointerId);
        }

        /// <summary>
        /// Sends a cancel along the path of each pointer whose claim changed, carrying the claim that holds it now.
        /// </summary>
        private void FlushChanged(List<PendingNotification> output, object? sender, Keys modifiers)
        {
            foreach (var pointerId in _changed)
            {
                if (Find(pointerId) is { } pressed)
                    AddCancel(output, pointerId, pressed.Claim, pressed.Path, pressed.Path.Count, 0, sender, modifiers);
            }

            _changed.Clear();
        }

        /// <summary>
        /// Takes removed behaviors out of every claim, ending claims left without members, and sends each one a cancel of its own
        /// on every pressed path through its node.
        /// </summary>
        private void SweepRemovedBehaviors(List<PendingNotification> output)
        {
            foreach (var (node, behavior) in _removed)
            {
                for (var i = 0; i < _pressed.Count; i++)
                {
                    var pressed = _pressed[i];
                    if (pressed.Claim is { } claim && claim.RemoveMember(behavior) && claim.Members.Count == 0)
                        EndClaim(claim);

                    var at = IndexOf(pressed.Path, node);
                    if (at >= 0)
                    {
                        output.Add(
                            new PendingNotification(
                                new PointerCancelNotification(pressed.Id, null, Keys.None, null!),
                                Delivery.Along(pressed.Path, at + 1, at, behavior)
                            )
                        );
                    }
                }

                FlushChanged(output, null, Keys.None);
            }

            _removed.Clear();
        }

        private void CutTo(PressedPointer pressed, int valid, List<PendingNotification> output)
        {
            var path = pressed.Path;
            if (valid >= path.Count)
                return;

            AddCancel(output, pressed.Id, null, path, path.Count, valid, null, Keys.None);
            path.RemoveRange(valid, path.Count - valid);
        }

        /// <summary>
        /// Cuts gesture paths where they stop being reachable. Gestures aren't pointers, so nothing is sent.
        /// </summary>
        private void SweepGestures(IInteractive root)
        {
            if (_gestures.Count == 0)
                return;

            foreach (var sequenceId in _gestures.Keys.ToArray())
            {
                var path = _gestures[sequenceId];
                var valid = ValidPrefix(root, path);
                if (valid < path.Length)
                    _gestures[sequenceId] = path[..valid];
            }
        }

        private static void AddCancel(
            List<PendingNotification> output,
            int pointerId,
            PointerClaim? claim,
            IReadOnlyList<IInteractive> path,
            int count,
            int from,
            object? sender,
            Keys modifiers
        )
        {
            if (from >= count)
                return;

            output.Add(
                new PendingNotification(
                    new PointerCancelNotification(pointerId, claim, modifiers, sender!),
                    Delivery.Along(path, count, from)
                )
            );
        }

        private static void AddCapture(
            List<PendingNotification> output,
            PointerClaim claim,
            IReadOnlyList<object> granted,
            object? sender,
            Keys modifiers
        )
        {
            if (claim.Pointers.Count == 0)
                return;

            output.Add(
                new PendingNotification(
                    new PointerCaptureNotification(claim.Pointers[0], claim, granted, modifiers, sender!),
                    new Delivery(DeliveryClass.Control, RouteKind.Target, claim.Node)
                )
            );
        }

        private bool IsPress(INotification notification, int pointerId) =>
            notification switch
            {
                MouseDownNotification => pointerId == PointerIds.Mouse && Find(PointerIds.Mouse) is null,
                TouchNotification { Kind: TouchNotificationKind.TouchDown } touch => touch.Id == pointerId,
                _ => false,
            };

        private static int IndexOf(IReadOnlyList<IInteractive> path, IInteractive node)
        {
            for (var i = 0; i < path.Count; i++)
            {
                if (ReferenceEquals(path[i], node))
                    return i;
            }

            return -1;
        }

        private static int CommonPrefix(IReadOnlyList<IInteractive> a, IReadOnlyList<IInteractive> b)
        {
            var i = 0;
            while (i < a.Count && i < b.Count && ReferenceEquals(a[i], b[i]))
                i++;

            return i;
        }

        private static Keys ModifiersOf(INotification notification) =>
            notification is NotificationBase withKeys ? withKeys.ModifierKeys : Keys.None;
    }
}
