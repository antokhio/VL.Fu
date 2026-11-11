using Microsoft.Extensions.Logging;
using Stride.Core.Mathematics;
using VL.Core;
using VL.Fu.Core.Interaction;
using VL.Fu.Core.Styles;
using VL.Lib.Collections;
using VL.Lib.IO;
using VL.Lib.IO.Notifications;
using VL.Skia;
using SkiaSharp;
using YogaSharp;

namespace VL.Fu.Tests.Core.Interaction
{
    /// <summary>
    /// Claims (N4): pointer routes, requests, resolution, the claim route, lifetime, the sweep and the behaviors diff.
    /// The scene is a 100 × 100 root with Back at (10, 10) and Front at (30, 30), both 40 × 40,
    /// and Child at Front's top-left corner, 10 × 10, so (35, 35) hits Child and (15, 15) hits Back.
    /// Every node has a probe behavior that logs "name:Kind".
    /// </summary>
    [TestClass]
    public sealed unsafe class ClaimsTests
    {
        private sealed class Node : Interactive<Node>
        {
            public Node()
                : base(null!) { }

            public YGNode* Native => Handle;

            public List<string> Warnings { get; } = new();

            public override void Log(LogLevel level, string message, TimeSpan? lifetime = null)
            {
                if (level >= LogLevel.Warning)
                    Warnings.Add(message);
                base.Log(level, message, lifetime);
            }
        }

        private sealed class Probe(string name, List<string> log) : IBehavior
        {
            /// <summary>
            /// Runs for every notification the probe receives, with the probe as the owner to request or release with.
            /// </summary>
            public Action<Probe, INotification>? OnNotify { get; set; }

            /// <summary>
            /// Which notifications the probe reports as processed. None by default.
            /// </summary>
            public Predicate<INotification>? Processes { get; set; }

            public List<INotification> Received { get; } = new();

            public bool? Requested { get; set; }

            public bool Notify(INotification notification, CallerInfo caller)
            {
                log.Add($"{name}:{notification.GetType().Name.Replace("Notification", "")}");
                Received.Add(notification);
                OnNotify?.Invoke(this, notification);
                return Processes?.Invoke(notification) ?? false;
            }

            public IEnumerable<T> Of<T>() => Received.OfType<T>();
        }

        private sealed class Collect(object from) : NotificationBase(from, Keys.None)
        {
            public Collect()
                : this(null!) { }

            public override INotification WithSender(object sender) => new Collect(sender);

            public override INotification Transform(INotificationSpaceTransformer transformer) => this;
        }

        private sealed class Scene : IDisposable
        {
            private readonly List<Node> _nodes = new();

            public List<string> Log { get; } = new();
            public Dictionary<Node, Probe> Probes { get; } = new();
            public Node Root { get; } = new();
            public Node Back { get; } = new();
            public Node Front { get; } = new();
            public Node Child { get; } = new();

            public Node Add(Node node, string name)
            {
                _nodes.Add(node);
                var probe = new Probe(name, Log);
                Probes[node] = probe;
                node.SetBehaviors(Spread.Create(new IBehavior[] { probe }));
                return node;
            }

            public Probe this[Node node] => Probes[node];

            public PointerClaims Claims => Root.Dispatcher.Claims;

            /// <summary>
            /// The log without hover, which follows every pointer pass.
            /// </summary>
            public string[] Delivered => Log.Where(e => !e.EndsWith(":PointerEnter") && !e.EndsWith(":PointerLeave")).ToArray();

            /// <summary>
            /// Only the captures and cancels of the log.
            /// </summary>
            public string[] ClaimLog =>
                Log.Where(e => e.EndsWith(":PointerCapture") || e.EndsWith(":PointerCancel")).ToArray();

            public string[] Hover => Log.Where(e => e.EndsWith(":PointerEnter") || e.EndsWith(":PointerLeave")).ToArray();

            public bool Notify(INotification notification) => Root.Notify(notification, CallerInfo.Default);

            public void Dispose()
            {
                foreach (var node in _nodes)
                    node.Dispose();
            }
        }

        private static Scene CreateScene()
        {
            var scene = new Scene();
            scene.Add(scene.Root, "root");
            scene.Add(scene.Back, "back");
            scene.Add(scene.Front, "front");
            scene.Add(scene.Child, "child");

            scene.Root.Native->SetWidth(100);
            scene.Root.Native->SetHeight(100);
            Place(scene.Back, 10, 10, 40);
            Place(scene.Front, 30, 30, 40);
            Place(scene.Child, 0, 0, 10);
            scene.Front.SetChildren(Spread.Create(new[] { scene.Child }));
            scene.Root.SetChildren(Spread.Create(new[] { scene.Back, scene.Front }));
            scene.Root.CalculateLayout();
            return scene;
        }

        private static void Place(Node node, float left, float top, float size)
        {
            node.Native->SetPositionType(YGPositionType.Absolute);
            node.Native->SetPosition(YGEdge.Left, left);
            node.Native->SetPosition(YGEdge.Top, top);
            node.Native->SetWidth(size);
            node.Native->SetHeight(size);
        }

        private static readonly Vector2 Area = new(100, 100);

        private static MouseDownNotification Down(float x, float y, MouseButtons button = MouseButtons.Left) =>
            new(new Vector2(x, y), Area, button, Keys.None, null!);

        private static MouseUpNotification Up(float x, float y, MouseButtons button = MouseButtons.Left) =>
            new(new Vector2(x, y), Area, button, Keys.None, null!);

        private static MouseMoveNotification Move(float x, float y) => new(new Vector2(x, y), Area, Keys.None, null!);

        private static MouseClickNotification Click(float x, float y) =>
            new(new Vector2(x, y), Area, MouseButtons.Left, 1, Keys.None, null!);

        private static MouseLostNotification MouseLost() => new(new Vector2(45, 45), Area, Keys.None, null!);

        private static KeyDownNotification Key(Keys key = Keys.A) => new(key, Keys.None, null!);

        private static TouchNotification Touch(TouchNotificationKind kind, float x, float y, int id = 7, bool primary = true) =>
            new(kind, new Vector2(x, y), Area, id, primary, Vector2.Zero, 0, Keys.None, null!);

        private static GestureNotification Gesture(GestureNotificationKind kind, float x, float y, int sequenceId = 3) =>
            new(kind, new Vector2(x, y), Area, 0, sequenceId, 0, 0, 0, 0, Keys.None, null!);

        private static string[] Entries(string kind, params string[] names) => names.Select(n => $"{n}:{kind}").ToArray();

        /// <summary>
        /// Makes <paramref name="probe"/> request the pointer for <paramref name="kind"/> whenever it receives a <typeparamref name="T"/>.
        /// </summary>
        private static void RequestOn<T>(Probe probe, GestureKind kind)
            where T : INotification
        {
            probe.OnNotify = (owner, n) =>
            {
                if (n is T)
                    owner.Requested = n.RequestPointerCapture(owner, kind);
            };
        }

        // Routes

        [TestMethod]
        public void Move_WhilePressed_FollowsTheRecordedPathOffTheNode()
        {
            using var scene = CreateScene();
            scene.Notify(Down(35, 35));
            scene.Log.Clear();

            scene.Notify(Move(15, 15));

            CollectionAssert.AreEqual(Entries("MouseMove", "child", "front", "root"), scene.Delivered);
        }

        [TestMethod]
        public void SecondButton_FollowsTheExistingRoute_AndTheLastUpReleases()
        {
            using var scene = CreateScene();
            scene.Notify(Down(35, 35));
            scene.Log.Clear();

            scene.Notify(Down(15, 15, MouseButtons.Right));
            scene.Notify(Up(15, 15));
            CollectionAssert.AreEqual(
                Entries("MouseDown", "child", "front", "root").Concat(Entries("MouseUp", "child", "front", "root")).ToArray(),
                scene.Delivered
            );

            scene.Log.Clear();
            scene.Notify(Up(15, 15, MouseButtons.Right));
            CollectionAssert.AreEqual(Entries("MouseUp", "child", "front", "root"), scene.Delivered);
            Assert.IsFalse(scene.Claims.HasPressed);

            scene.Log.Clear();
            scene.Notify(Move(15, 15));
            CollectionAssert.AreEqual(Entries("MouseMove", "back", "root"), scene.Delivered);
        }

        [TestMethod]
        public void Click_TakesTheUpsPath()
        {
            using var scene = CreateScene();
            scene.Notify(Down(35, 35));
            scene.Notify(Up(15, 15));
            scene.Log.Clear();

            scene.Notify(Click(15, 15));

            CollectionAssert.AreEqual(Entries("MouseClick", "child", "front", "root"), scene.Delivered);
        }

        [TestMethod]
        public void PressOnNothing_DeliversItsMovesToNothing_UntilReleased()
        {
            using var scene = CreateScene();
            scene.Notify(Down(150, 150));

            Assert.IsFalse(scene.Notify(Move(35, 35)));
            Assert.AreEqual(0, scene.Delivered.Length);

            scene.Notify(Up(35, 35));
            scene.Notify(Move(35, 35));
            CollectionAssert.AreEqual(Entries("MouseMove", "child", "front", "root"), scene.Delivered);
        }

        [TestMethod]
        public void Gesture_BeginRecords_LaterKindsFollow_EndDrops()
        {
            using var scene = CreateScene();
            scene.Notify(Gesture(GestureNotificationKind.GestureBegin, 35, 35));
            scene.Log.Clear();

            scene.Notify(Gesture(GestureNotificationKind.GesturePan, 15, 15));
            scene.Notify(Gesture(GestureNotificationKind.GestureEnd, 15, 15));
            CollectionAssert.AreEqual(
                Entries("Gesture", "child", "front", "root").Concat(Entries("Gesture", "child", "front", "root")).ToArray(),
                scene.Delivered
            );

            scene.Log.Clear();
            scene.Notify(Gesture(GestureNotificationKind.GesturePan, 15, 15));
            CollectionAssert.AreEqual(Entries("Gesture", "back", "root"), scene.Delivered);
        }

        // Requests

        [TestMethod]
        public void Request_OutsideAPass_IsRejected()
        {
            var owner = new object();

            Assert.IsFalse(Move(35, 35).RequestPointerCapture(owner, GestureKind.Drag));
        }

        [TestMethod]
        public void Request_ForAPointerThatIsNotPressed_IsRejectedWithAWarning()
        {
            using var scene = CreateScene();
            RequestOn<MouseMoveNotification>(scene[scene.Child], GestureKind.Drag);

            scene.Notify(Move(35, 35));

            Assert.IsFalse(scene[scene.Child].Requested);
            Assert.AreEqual(1, scene.Child.Warnings.Count);
        }

        [TestMethod]
        public void Request_ForAPointerPressedOnAnotherNode_IsRejectedWhole()
        {
            using var scene = CreateScene();
            scene.Notify(Touch(TouchNotificationKind.TouchDown, 15, 15, id: 7));
            scene[scene.Front].OnNotify = (owner, n) =>
            {
                if (n is TouchNotification { Kind: TouchNotificationKind.TouchDown })
                    owner.Requested = n.RequestPointerCapture(owner, GestureKind.Pinch, new[] { 7 });
            };

            scene.Notify(Touch(TouchNotificationKind.TouchDown, 45, 45, id: 8, primary: false));

            Assert.IsFalse(scene[scene.Front].Requested);
            Assert.AreEqual(1, scene.Front.Warnings.Count);
            Assert.IsNull(scene.Claims.ClaimOf(8));
        }

        [TestMethod]
        public void Request_WithAGesture_IsRejectedWithAWarning()
        {
            using var scene = CreateScene();
            RequestOn<GestureNotification>(scene[scene.Child], GestureKind.Pinch);

            scene.Notify(Gesture(GestureNotificationKind.GestureBegin, 35, 35));

            Assert.IsFalse(scene[scene.Child].Requested);
            Assert.AreEqual(1, scene.Child.Warnings.Count);
        }

        [TestMethod]
        public void Request_WithoutOwnerOrWithAnUnknownKind_IsRejectedWithAWarning()
        {
            using var scene = CreateScene();
            var results = new List<bool>();
            scene[scene.Child].OnNotify = (_, n) =>
            {
                if (n is MouseDownNotification)
                {
                    results.Add(n.RequestPointerCapture(null!, GestureKind.Drag));
                    results.Add(n.RequestPointerCapture(new object(), (GestureKind)42));
                }
            };

            scene.Notify(Down(35, 35));

            CollectionAssert.AreEqual(new[] { false, false }, results);
            Assert.AreEqual(2, scene.Child.Warnings.Count);
            Assert.IsNull(scene.Claims.ClaimOf(PointerIds.Mouse));
        }

        [TestMethod]
        public void Request_WhileAQueryIsAnswered_IsRejectedWithAWarning()
        {
            using var scene = CreateScene();
            MouseDownNotification? down = null;
            bool? requested = null;
            scene[scene.Child].OnNotify = (owner, n) =>
            {
                if (n is MouseDownNotification d)
                {
                    down = d;
                    n.Query(new Collect());
                }
                else if (n is Collect && down is not null)
                    requested = down.RequestPointerCapture(owner, GestureKind.Drag);
            };

            scene.Notify(Down(35, 35));

            Assert.IsFalse(requested);
            Assert.IsNull(scene.Claims.ClaimOf(PointerIds.Mouse));
        }

        [TestMethod]
        public void ValidRequest_ReturnsTrue_AndChangesNothingUntilThePassEnds()
        {
            using var scene = CreateScene();
            var during = new List<PointerClaim?>();
            RequestOn<MouseDownNotification>(scene[scene.Child], GestureKind.Drag);
            scene[scene.Front].OnNotify = (_, n) =>
            {
                if (n is MouseDownNotification)
                    during.Add(scene.Claims.ClaimOf(PointerIds.Mouse));
            };

            scene.Notify(Down(35, 35));

            Assert.IsTrue(scene[scene.Child].Requested);
            CollectionAssert.AreEqual(new PointerClaim?[] { null }, during);
            Assert.AreSame(scene.Child, scene.Claims.ClaimOf(PointerIds.Mouse)?.Node);
            Assert.AreEqual(0, scene.Child.Warnings.Count);
        }

        // Resolution

        [TestMethod]
        public void EqualKinds_InnermostWins_AndCancelComesBeforeCapture_AfterThePass()
        {
            using var scene = CreateScene();
            RequestOn<MouseDownNotification>(scene[scene.Child], GestureKind.Drag);
            RequestOn<MouseDownNotification>(scene[scene.Front], GestureKind.Drag);

            scene.Notify(Down(35, 35));

            CollectionAssert.AreEqual(
                Entries("MouseDown", "child", "front", "root")
                    .Concat(Entries("PointerCancel", "child", "front", "root"))
                    .Concat(Entries("PointerCapture", "child"))
                    .ToArray(),
                scene.Delivered
            );

            var claim = scene.Claims.ClaimOf(PointerIds.Mouse)!;
            Assert.AreSame(scene.Child, claim.Node);
            Assert.IsTrue(scene[scene.Front].Requested);
            Assert.IsFalse(scene[scene.Front].Of<PointerCancelNotification>().Single().IsMember(scene[scene.Front]));
            Assert.IsTrue(scene[scene.Child].Of<PointerCaptureNotification>().Single().Grants(scene[scene.Child]));
        }

        [TestMethod]
        public void HigherKindOnAnAncestor_ReplacesTheChildsClaim()
        {
            using var scene = CreateScene();
            RequestOn<TouchNotification>(scene[scene.Child], GestureKind.Drag);
            scene.Notify(Touch(TouchNotificationKind.TouchDown, 35, 35, id: 7));
            var drag = scene.Claims.ClaimOf(7)!;

            scene[scene.Front].OnNotify = (owner, n) =>
            {
                if (n is TouchNotification { Kind: TouchNotificationKind.TouchDown, Id: 8 })
                    owner.Requested = n.RequestPointerCapture(owner, GestureKind.Pinch, new[] { 7 });
            };
            scene.Log.Clear();
            scene.Notify(Touch(TouchNotificationKind.TouchDown, 45, 45, id: 8, primary: false));

            var pinch = scene.Claims.ClaimOf(7)!;
            Assert.IsTrue(scene[scene.Front].Requested);
            Assert.AreSame(scene.Front, pinch.Node);
            Assert.AreSame(pinch, scene.Claims.ClaimOf(8));
            Assert.IsFalse(drag.IsActive);

            var cancel = scene[scene.Child].Of<PointerCancelNotification>().Last();
            Assert.AreSame(pinch, cancel.Claim);
            Assert.IsFalse(cancel.IsMember(scene[scene.Child]));
            Assert.AreEqual("front:PointerCapture", scene.ClaimLog.Last());
            Assert.AreEqual(1, scene.ClaimLog.Count(e => e.EndsWith(":PointerCapture")));
        }

        [TestMethod]
        public void TwoRequestsOfOneNodeAndKind_BothBecomeMembers()
        {
            using var scene = CreateScene();
            var second = new Probe("child2", scene.Log);
            scene.Child.SetBehaviors(Spread.Create(new IBehavior[] { scene[scene.Child], second }));
            RequestOn<MouseDownNotification>(scene[scene.Child], GestureKind.Drag);
            RequestOn<MouseDownNotification>(second, GestureKind.Drag);

            scene.Notify(Down(35, 35));

            var claim = scene.Claims.ClaimOf(PointerIds.Mouse)!;
            CollectionAssert.AreEqual(new object[] { scene[scene.Child], second }, claim.Members.ToArray());
            var capture = second.Of<PointerCaptureNotification>().Single();
            Assert.IsTrue(capture.Grants(second));
            Assert.IsTrue(capture.Grants(scene[scene.Child]));
        }

        [TestMethod]
        public void LateJoin_GetsACaptureOfItsOwn()
        {
            using var scene = CreateScene();
            var second = new Probe("child2", scene.Log);
            scene.Child.SetBehaviors(Spread.Create(new IBehavior[] { scene[scene.Child], second }));
            RequestOn<MouseDownNotification>(scene[scene.Child], GestureKind.Drag);
            RequestOn<MouseMoveNotification>(second, GestureKind.Drag);
            scene.Notify(Down(35, 35));
            scene.Log.Clear();

            scene.Notify(Move(36, 36));

            CollectionAssert.AreEqual(Entries("PointerCapture", "child", "child2"), scene.ClaimLog);
            var capture = second.Of<PointerCaptureNotification>().Last();
            Assert.IsTrue(capture.Grants(second));
            Assert.IsFalse(capture.Grants(scene[scene.Child]));
            Assert.AreEqual(2, scene.Claims.ClaimOf(PointerIds.Mouse)!.Members.Count);
        }

        [TestMethod]
        public void PinchOverAPointerHeldByAnEqualClaim_IsRejectedWhole()
        {
            using var scene = CreateScene();
            RequestOn<TouchNotification>(scene[scene.Child], GestureKind.Pinch);
            scene.Notify(Touch(TouchNotificationKind.TouchDown, 35, 35, id: 7));
            var held = scene.Claims.ClaimOf(7);

            scene[scene.Front].OnNotify = (owner, n) =>
            {
                if (n is TouchNotification { Kind: TouchNotificationKind.TouchDown, Id: 8 })
                    owner.Requested = n.RequestPointerCapture(owner, GestureKind.Pinch, new[] { 7 });
            };
            scene.Notify(Touch(TouchNotificationKind.TouchDown, 45, 45, id: 8, primary: false));

            Assert.IsTrue(scene[scene.Front].Requested);
            Assert.AreSame(held, scene.Claims.ClaimOf(7));
            Assert.IsNull(scene.Claims.ClaimOf(8));
        }

        // Claim route

        [TestMethod]
        public void ClaimedInput_ReachesOnlyTheClaimNode_EvenOutsideItsBounds()
        {
            using var scene = CreateScene();
            RequestOn<MouseDownNotification>(scene[scene.Child], GestureKind.Drag);
            scene.Notify(Down(35, 35));
            scene.Log.Clear();

            scene.Notify(Move(90, 90));

            CollectionAssert.AreEqual(Entries("MouseMove", "child"), scene.Delivered);
        }

        [TestMethod]
        public void Hover_FollowsTheClaimNode_AndLeavesWithoutAClaim()
        {
            using var claimed = CreateScene();
            RequestOn<MouseDownNotification>(claimed[claimed.Child], GestureKind.Drag);
            claimed.Notify(Down(35, 35));
            claimed.Log.Clear();
            claimed.Notify(Move(90, 90));
            Assert.AreEqual(0, claimed.Hover.Length);

            using var pressed = CreateScene();
            pressed.Notify(Down(35, 35));
            pressed.Log.Clear();
            pressed.Notify(Move(90, 90));
            CollectionAssert.AreEqual(Entries("PointerLeave", "child", "front"), pressed.Hover);
        }

        // Lifetime

        [TestMethod]
        public void ProcessedUp_SendsACancelAlongThePath_AnUnprocessedOneDoesNot()
        {
            using var scene = CreateScene();
            scene.Notify(Down(35, 35));
            scene.Notify(Up(35, 35));
            Assert.AreEqual(0, scene.ClaimLog.Length);

            scene[scene.Child].Processes = n => n is MouseUpNotification;
            scene.Notify(Down(35, 35));
            scene.Notify(Up(35, 35));
            CollectionAssert.AreEqual(Entries("PointerCancel", "child", "front", "root"), scene.ClaimLog);
        }

        [TestMethod]
        public void ClaimedUp_SendsACancelAlongThePath_EvenWhenNobodyProcessedIt()
        {
            using var scene = CreateScene();
            RequestOn<MouseDownNotification>(scene[scene.Child], GestureKind.Drag);
            scene.Notify(Down(35, 35));
            scene.Log.Clear();

            scene.Notify(Up(35, 35));

            CollectionAssert.AreEqual(Entries("PointerCancel", "child", "front", "root"), scene.ClaimLog);
            Assert.IsNull(scene.Claims.ClaimOf(PointerIds.Mouse));
        }

        [TestMethod]
        public void Release_FallsBackToTheRecordedPath_WithoutNotifying()
        {
            using var scene = CreateScene();
            scene[scene.Child].OnNotify = (owner, n) =>
            {
                if (n is MouseDownNotification)
                    n.RequestPointerCapture(owner, GestureKind.Drag);
                else if (n is MouseMoveNotification)
                    n.ReleasePointerCapture(owner);
            };
            scene.Notify(Down(35, 35));
            scene.Notify(Move(90, 90));
            Assert.IsNull(scene.Claims.ClaimOf(PointerIds.Mouse));
            scene.Log.Clear();

            scene.Notify(Move(15, 15));

            CollectionAssert.AreEqual(Entries("MouseMove", "child", "front", "root"), scene.Delivered);
            Assert.AreEqual(0, scene.ClaimLog.Length);
        }

        [TestMethod]
        public void MouseLost_CancelsBeforeItsBroadcast_AndDropsTheClickPath()
        {
            using var scene = CreateScene();
            RequestOn<MouseDownNotification>(scene[scene.Child], GestureKind.Drag);
            scene.Notify(Down(35, 35));
            scene.Log.Clear();

            scene.Notify(MouseLost());

            var delivered = scene.Delivered;
            CollectionAssert.AreEqual(Entries("PointerCancel", "child", "front", "root"), delivered.Take(3).ToArray());
            Assert.IsTrue(delivered.Skip(3).All(e => e.EndsWith(":MouseLost")));
            Assert.IsFalse(scene.Claims.HasPressed);
            Assert.IsNull(scene[scene.Child].Of<PointerCancelNotification>().Last().Claim);

            scene.Log.Clear();
            scene.Notify(Click(15, 15));
            CollectionAssert.AreEqual(Entries("MouseClick", "back", "root"), scene.Delivered);
        }

        [TestMethod]
        public void LostFocus_CancelsEveryPointer()
        {
            using var scene = CreateScene();
            scene.Notify(Touch(TouchNotificationKind.TouchDown, 35, 35, id: 7));
            scene.Notify(Touch(TouchNotificationKind.TouchDown, 15, 15, id: 8, primary: false));
            scene.Log.Clear();

            scene.Notify(new LostFocusNotification(null!, Keys.None));

            CollectionAssert.AreEquivalent(
                Entries("PointerCancel", "child", "front", "root", "back", "root"),
                scene.ClaimLog
            );
            Assert.IsFalse(scene.Claims.HasPressed);
        }

        [TestMethod]
        public void UnprocessedEsc_CancelsTheClaims_AndKeepsThePointersPressed()
        {
            using var scene = CreateScene();
            RequestOn<MouseDownNotification>(scene[scene.Child], GestureKind.Drag);
            scene.Notify(Down(35, 35));
            scene.Log.Clear();

            scene.Notify(Key(Keys.Escape));

            CollectionAssert.AreEqual(Entries("PointerCancel", "child", "front", "root"), scene.ClaimLog);
            Assert.IsNull(scene.Claims.ClaimOf(PointerIds.Mouse));
            Assert.IsNotNull(scene.Claims.Find(PointerIds.Mouse));

            scene.Log.Clear();
            scene.Notify(Move(15, 15));
            CollectionAssert.AreEqual(Entries("MouseMove", "child", "front", "root"), scene.Delivered);
        }

        [TestMethod]
        public void ProcessedEsc_ChangesNothing()
        {
            using var scene = CreateScene();
            RequestOn<MouseDownNotification>(scene[scene.Child], GestureKind.Drag);
            scene[scene.Back].Processes = n => n is KeyDownNotification;
            scene.Notify(Down(35, 35));
            var claim = scene.Claims.ClaimOf(PointerIds.Mouse);
            scene.Log.Clear();

            scene.Notify(Key(Keys.Escape));

            Assert.AreEqual(0, scene.ClaimLog.Length);
            Assert.AreSame(claim, scene.Claims.ClaimOf(PointerIds.Mouse));
        }

        [TestMethod]
        [DataRow("detach")]
        [DataRow("hide ancestor")]
        [DataRow("pointer events none")]
        [DataRow("dispose")]
        public void ClaimNodeLeaving_EndsTheClaim_WithACancelAlongTheWholePath(string how)
        {
            using var scene = CreateScene();
            var childProbe = scene[scene.Child];
            RequestOn<MouseDownNotification>(childProbe, GestureKind.Drag);
            scene.Notify(Down(35, 35));
            scene.Log.Clear();

            switch (how)
            {
                case "detach":
                    scene.Front.SetChildren(Spread<Node>.Empty);
                    break;
                case "hide ancestor":
                    scene.Front.Native->SetDisplay(YGDisplay.None);
                    break;
                case "pointer events none":
                    scene.Child.SetPointerEvents(PointerEvents.None);
                    break;
                case "dispose":
                    scene.Child.Dispose();
                    break;
            }

            scene.Notify(Key());

            CollectionAssert.AreEqual(Entries("PointerCancel", "child", "front", "root"), scene.ClaimLog);
            Assert.IsNull(childProbe.Of<PointerCancelNotification>().Last().Claim);
            Assert.IsNull(scene.Claims.ClaimOf(PointerIds.Mouse));
            Assert.IsNotNull(scene.Claims.Find(PointerIds.Mouse));
        }

        [TestMethod]
        public void Render_SweepsBeforeTheNextNotification()
        {
            using var scene = CreateScene();
            RequestOn<MouseDownNotification>(scene[scene.Child], GestureKind.Drag);
            scene.Notify(Down(35, 35));
            scene.Front.SetChildren(Spread<Node>.Empty);
            scene.Log.Clear();

            using var bitmap = new SKBitmap(100, 100);
            using var canvas = new SKCanvas(bitmap);
            scene.Root.Render(CallerInfo.InRenderer(100, 100, canvas, null!, 1f));

            CollectionAssert.AreEqual(Entries("PointerCancel", "child", "front", "root"), scene.ClaimLog);
            Assert.IsNull(scene.Claims.ClaimOf(PointerIds.Mouse));
        }

        [TestMethod]
        public void NodeBelowTheClaimNodeLeaving_KeepsTheClaim_AndCancelsOnlyThatNode()
        {
            using var scene = CreateScene();
            RequestOn<MouseDownNotification>(scene[scene.Front], GestureKind.Drag);
            scene.Notify(Down(35, 35));
            var claim = scene.Claims.ClaimOf(PointerIds.Mouse);
            scene.Log.Clear();

            scene.Front.SetChildren(Spread<Node>.Empty);
            scene.Notify(Key());

            CollectionAssert.AreEqual(Entries("PointerCancel", "child"), scene.ClaimLog);
            Assert.AreSame(claim, scene.Claims.ClaimOf(PointerIds.Mouse));
            Assert.IsTrue(claim!.IsActive);
        }

        [TestMethod]
        public void ClaimNodeMovedInsideTheRoot_KeepsTheClaim_AndCancelsTheNodesThatDroppedOff()
        {
            using var scene = CreateScene();
            RequestOn<MouseDownNotification>(scene[scene.Child], GestureKind.Drag);
            scene.Notify(Down(35, 35));
            var claim = scene.Claims.ClaimOf(PointerIds.Mouse);
            scene.Log.Clear();

            scene.Front.SetChildren(Spread<Node>.Empty);
            scene.Back.SetChildren(Spread.Create(new[] { scene.Child }));
            scene.Root.CalculateLayout();
            scene.Notify(Key());

            CollectionAssert.AreEqual(Entries("PointerCancel", "front"), scene.ClaimLog);
            Assert.AreSame(claim, scene.Claims.ClaimOf(PointerIds.Mouse));
            CollectionAssert.AreEqual(
                new IInteractive[] { scene.Root, scene.Back, scene.Child },
                scene.Claims.Find(PointerIds.Mouse)!.Path.ToArray()
            );
        }

        [TestMethod]
        public void HoveredNodeDetached_GetsItsLeave_DeepestFirst()
        {
            using var scene = CreateScene();
            scene.Notify(Move(35, 35));
            scene.Log.Clear();

            scene.Root.SetChildren(Spread.Create(new[] { scene.Back }));
            scene.Notify(Key());

            CollectionAssert.AreEqual(Entries("PointerLeave", "child", "front"), scene.Hover);
            CollectionAssert.AreEqual(new IInteractive[] { scene.Root }, scene.Root.Dispatcher.HoverPath.ToArray());
        }

        // SetBehaviors diff

        [TestMethod]
        public void RemovedMember_LeavesTheClaim_AndTheLastOneEndsIt()
        {
            using var scene = CreateScene();
            var first = scene[scene.Child];
            var second = new Probe("child2", scene.Log);
            scene.Child.SetBehaviors(Spread.Create(new IBehavior[] { first, second }));
            RequestOn<MouseDownNotification>(first, GestureKind.Drag);
            RequestOn<MouseDownNotification>(second, GestureKind.Drag);
            scene.Notify(Down(35, 35));
            var claim = scene.Claims.ClaimOf(PointerIds.Mouse)!;
            scene.Log.Clear();

            scene.Child.SetBehaviors(Spread.Create(new IBehavior[] { second }));
            scene.Notify(Key());

            CollectionAssert.AreEqual(Entries("PointerCancel", "child"), scene.ClaimLog);
            CollectionAssert.AreEqual(new object[] { second }, claim.Members.ToArray());
            Assert.IsTrue(claim.IsActive);

            scene.Log.Clear();
            scene.Child.SetBehaviors(Spread<IBehavior>.Empty);
            scene.Notify(Key());

            Assert.IsFalse(claim.IsActive);
            Assert.IsNull(scene.Claims.ClaimOf(PointerIds.Mouse));
            CollectionAssert.AreEqual(Entries("PointerCancel", "child2", "front", "root"), scene.ClaimLog);
        }

        [TestMethod]
        public void RemovedBehaviorOnAPressedPath_GetsOneDirectCancel()
        {
            using var scene = CreateScene();
            var removed = scene[scene.Child];
            scene.Notify(Down(35, 35));
            scene.Log.Clear();

            scene.Child.SetBehaviors(Spread<IBehavior>.Empty);
            scene.Notify(Key());

            CollectionAssert.AreEqual(Entries("PointerCancel", "child"), scene.ClaimLog);
            Assert.AreEqual(1, removed.Of<PointerCancelNotification>().Count());
        }

        [TestMethod]
        public void ReorderedBehaviors_GetNothing()
        {
            using var scene = CreateScene();
            var first = scene[scene.Child];
            var second = new Probe("child2", scene.Log);
            scene.Child.SetBehaviors(Spread.Create(new IBehavior[] { first, second }));
            scene.Notify(Down(35, 35));
            scene.Log.Clear();

            scene.Child.SetBehaviors(Spread.Create(new IBehavior[] { second, first }));
            scene.Notify(Key());

            Assert.AreEqual(0, scene.ClaimLog.Length);
        }

        // Outbox

        [TestMethod]
        public void CapturesAndCancels_AreNeverDeliveredInsideAnotherPass()
        {
            using var scene = CreateScene();
            var outer = new List<bool>();
            scene[scene.Child].OnNotify = (owner, n) =>
            {
                if (n is MouseDownNotification)
                    n.RequestPointerCapture(owner, GestureKind.Drag);
                else if (n is PointerCaptureNotification or PointerCancelNotification)
                    outer.Add(InteractionPass.Current!.Notification is MouseDownNotification);
            };

            scene.Notify(Down(35, 35));

            CollectionAssert.AreEqual(new[] { false, false }, outer);
        }
    }
}
