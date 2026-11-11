using Microsoft.Extensions.Logging;
using Stride.Core.Mathematics;
using VL.Core;
using VL.Fu.Core.Interaction;
using VL.Fu.Core.Rendering;
using VL.Fu.Core.Styles;
using VL.Lib.Collections;
using VL.Lib.IO;
using VL.Lib.IO.Notifications;
using VL.Skia;
using YogaSharp;

namespace VL.Fu.Tests.Core.Interaction
{
    /// <summary>
    /// Orchestration (N3): passes and the outbox, the snapshot rule, Dispatch and Query, hover, touch promotion and time.
    /// The scene is a 100 × 100 root with Back at (10, 10) and Front at (30, 30), both 40 × 40,
    /// and Child at Front's top-left corner, 10 × 10. Every node has a probe behavior that logs "name:Kind".
    /// </summary>
    [TestClass]
    public sealed unsafe class DispatcherTests
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
            public Action<INotification>? OnNotify { get; set; }

            public List<INotification> Received { get; } = new();

            public bool Notify(INotification notification, CallerInfo caller)
            {
                if (notification is Collect collect)
                    collect.Names.Add(name);

                log.Add($"{name}:{notification.GetType().Name.Replace("Notification", "")}");
                Received.Add(notification);
                OnNotify?.Invoke(notification);
                return false;
            }
        }

        private sealed class Ping(object from) : NotificationBase(from, Keys.None)
        {
            public override INotification WithSender(object sender) => new Ping(sender);

            public override INotification Transform(INotificationSpaceTransformer transformer) => this;
        }

        private sealed class Collect(object from, List<string> names) : NotificationBase(from, Keys.None)
        {
            public Collect()
                : this(null!, new List<string>()) { }

            public List<string> Names => names;

            public override INotification WithSender(object sender) => new Collect(sender, names);

            public override INotification Transform(INotificationSpaceTransformer transformer) => this;
        }

        private sealed class ManualClock : TimeProvider
        {
            private long _now;

            public override long TimestampFrequency => 1000;

            public override long GetTimestamp() => _now;

            public void Advance(long ms) => _now += ms;
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

            public string[] Hover => Log.Where(e => e.Contains(":Pointer")).ToArray();

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

        private static KeyDownNotification Key() => new(Keys.A, Keys.None, null!);

        private static MouseMoveNotification Move(float x, float y) =>
            new(new Vector2(x, y), new Vector2(100, 100), Keys.None, null!);

        private static MouseLostNotification MouseLost() =>
            new(new Vector2(45, 45), new Vector2(100, 100), Keys.None, null!);

        private static TouchNotification Touch(TouchNotificationKind kind, float x, float y, int id = 7) =>
            new(kind, new Vector2(x, y), new Vector2(100, 100), id, true, Vector2.Zero, 0, Keys.None, null!);

        private static string[] Entries(string kind, params string[] names) =>
            names.Select(n => $"{n}:{kind}").ToArray();

        // Passes

        [TestMethod]
        public void Current_IsNullOutsideAPass_AndNamesTheReceivingNodeInside()
        {
            using var scene = CreateScene();
            IInteractive? acting = null;
            scene[scene.Front].OnNotify = _ => acting = InteractionPass.Current?.Node;

            Assert.IsNull(InteractionPass.Current);
            scene.Root.Notify(Key(), CallerInfo.Default);

            Assert.AreSame(scene.Front, acting);
            Assert.IsNull(InteractionPass.Current);
        }

        [TestMethod]
        public void NotifyFromABehavior_IsQueuedAfterThePass_AndReturnsFalse()
        {
            using var scene = CreateScene();
            bool? inner = null;
            scene[scene.Front].OnNotify = n =>
            {
                if (n is KeyDownNotification)
                    inner = scene.Root.Notify(new Ping(null!), CallerInfo.Default);
            };

            scene.Root.Notify(Key(), CallerInfo.Default);

            Assert.AreEqual(false, inner);
            CollectionAssert.AreEqual(
                Entries("KeyDown", "child", "front", "back", "root").Concat(Entries("Ping", "child", "front", "back", "root")).ToArray(),
                scene.Log
            );
        }

        [TestMethod]
        public void RootInsideALayer_NestsItsPass_AndRestoresTheOuterOne()
        {
            using var scene = CreateScene();
            var nested = scene.Add(new Node(), "nested");
            nested.Native->SetWidth(10);
            nested.Native->SetHeight(10);
            nested.CalculateLayout();
            scene.Front.SetLayer(nested);

            InteractionPass? outer = null;
            InteractionPass? innerPass = null;
            IInteractive? innerRoot = null;
            IInteractive? innerNode = null;
            InteractionPass? afterLayer = null;
            IInteractive? afterLayerNode = null;
            scene[scene.Child].OnNotify = _ => outer = InteractionPass.Current;
            scene[nested].OnNotify = _ =>
            {
                innerPass = InteractionPass.Current;
                innerRoot = innerPass?.Root;
                innerNode = innerPass?.Node;
            };
            scene[scene.Front].OnNotify = _ =>
            {
                afterLayer = InteractionPass.Current;
                afterLayerNode = afterLayer?.Node;
            };

            scene.Root.Notify(Key(), CallerInfo.Default);

            Assert.IsNotNull(outer);
            Assert.AreNotSame(outer, innerPass);
            Assert.AreSame(nested, innerRoot);
            Assert.AreSame(nested, innerNode);
            Assert.AreSame(outer, afterLayer);
            Assert.AreSame(scene.Front, afterLayerNode);
            Assert.IsNull(InteractionPass.Current);
        }

        [TestMethod]
        public void ThrowingBehavior_EndsThePass_DropsTheOutbox_AndTheNextNotificationIsDelivered()
        {
            using var scene = CreateScene();
            scene[scene.Front].OnNotify = n =>
            {
                scene.Root.Notify(new Ping(null!), CallerInfo.Default);
                throw new InvalidOperationException("boom");
            };

            Assert.ThrowsExactly<InvalidOperationException>(() => scene.Root.Notify(Key(), CallerInfo.Default));
            Assert.IsNull(InteractionPass.Current);

            scene[scene.Front].OnNotify = null;
            scene.Log.Clear();
            scene.Root.Notify(Key(), CallerInfo.Default);

            CollectionAssert.AreEqual(Entries("KeyDown", "child", "front", "back", "root"), scene.Log);
        }

        // Snapshot

        [TestMethod]
        public void NodeDetachedByAnEarlierSibling_IsSkipped()
        {
            using var scene = CreateScene();
            scene[scene.Front].OnNotify = _ => scene.Root.SetChildren(Spread.Create(new[] { scene.Front }));

            scene.Root.Notify(Key(), CallerInfo.Default);

            CollectionAssert.AreEqual(Entries("KeyDown", "child", "front", "root"), scene.Log);
        }

        [TestMethod]
        public void NodeDetachedByItsChild_SkipsItsOwnReceivers_AndItsAncestorsStillRun()
        {
            using var scene = CreateScene();
            scene[scene.Child].OnNotify = _ => scene.Root.SetChildren(Spread.Create(new[] { scene.Back }));

            scene.Root.Notify(Key(), CallerInfo.Default);

            CollectionAssert.AreEqual(Entries("KeyDown", "child", "back", "root"), scene.Log);
        }

        // Outbox guard

        [TestMethod]
        public void DispatchingOnEveryNotification_StopsAfterTheGuard_WithAWarning()
        {
            using var scene = CreateScene();
            scene[scene.Front].OnNotify = n => n.Dispatch(new Ping(null!));

            scene.Root.Notify(Key(), CallerInfo.Default);

            Assert.AreEqual(NotificationDispatcher.MaxQueuedPasses, scene.Log.Count(e => e == "front:Ping"));
            Assert.AreEqual(1, scene.Root.Warnings.Count);
            StringAssert.Contains(scene.Root.Warnings[0], NotificationDispatcher.MaxQueuedPasses.ToString());
        }

        // Dispatch

        [TestMethod]
        public void Dispatch_IsQueuedAfterThePass_IntoTheNodesSubtree()
        {
            using var scene = CreateScene();
            scene[scene.Front].OnNotify = n =>
            {
                if (n is KeyDownNotification)
                    n.Dispatch(new Ping(null!));
            };

            scene.Root.Notify(Key(), CallerInfo.Default);

            CollectionAssert.AreEqual(
                Entries("KeyDown", "child", "front", "back", "root").Concat(Entries("Ping", "child", "front")).ToArray(),
                scene.Log
            );
        }

        [TestMethod]
        public void Dispatch_SkipsHiddenNodes()
        {
            using var scene = CreateScene();
            scene.Child.Native->SetDisplay(YGDisplay.None);
            scene.Root.CalculateLayout();
            scene[scene.Front].OnNotify = n =>
            {
                if (n is KeyDownNotification)
                    n.Dispatch(new Ping(null!));
            };

            scene.Root.Notify(Key(), CallerInfo.Default);

            CollectionAssert.AreEqual(Entries("Ping", "front"), scene.Log.Where(e => e.EndsWith(":Ping")).ToArray());
        }

        [TestMethod]
        public void Dispatch_OutsideAPass_DoesNothing_WithAWarningOnTheSendersNode()
        {
            using var scene = CreateScene();
            scene.Root.Notify(Key(), CallerInfo.Default);
            var received = scene[scene.Front].Received.Single();

            received.Dispatch(new Ping(null!));
            scene.Root.Notify(Key(), CallerInfo.Default);

            Assert.AreEqual(1, scene.Front.Warnings.Count);
            Assert.IsFalse(scene.Log.Any(e => e.EndsWith(":Ping")));
        }

        [TestMethod]
        public void Dispatch_WithANotificationThePassIsNotDelivering_DoesNothing_WithAWarning()
        {
            using var scene = CreateScene();
            scene[scene.Front].OnNotify = n =>
            {
                if (n is KeyDownNotification)
                    new Ping(null!).Dispatch(new Ping(null!));
            };

            scene.Root.Notify(Key(), CallerInfo.Default);

            Assert.AreEqual(1, scene.Front.Warnings.Count);
            Assert.IsFalse(scene.Log.Any(e => e.EndsWith(":Ping")));
        }

        [TestMethod]
        public void Dispatch_WithNothingToSend_DoesNothing_WithAWarning()
        {
            using var scene = CreateScene();
            scene[scene.Front].OnNotify = n =>
            {
                if (n is KeyDownNotification)
                    n.Dispatch(null!);
            };

            scene.Root.Notify(Key(), CallerInfo.Default);

            Assert.AreEqual(1, scene.Front.Warnings.Count);
            CollectionAssert.AreEqual(Entries("KeyDown", "child", "front", "back", "root"), scene.Log);
        }

        // Query

        [TestMethod]
        public void Query_IsDeliveredBeforeItReturns_IntoTheSubtreeOnly()
        {
            using var scene = CreateScene();
            string[]? answers = null;
            scene[scene.Front].OnNotify = n =>
            {
                if (n is not KeyDownNotification)
                    return;

                var query = new Collect();
                n.Query(query);
                answers = query.Names.ToArray();
            };

            scene.Root.Notify(Key(), CallerInfo.Default);

            CollectionAssert.AreEqual(new[] { "child", "front" }, answers);
            Assert.IsFalse(scene.Log.Contains("back:Collect"));
            Assert.IsFalse(scene.Log.Contains("root:Collect"));
        }

        [TestMethod]
        public void Query_RestoresTheAskingPass()
        {
            using var scene = CreateScene();
            InteractionPass? before = null;
            InteractionPass? after = null;
            IInteractive? afterNode = null;
            scene[scene.Front].OnNotify = n =>
            {
                if (n is not KeyDownNotification)
                    return;

                before = InteractionPass.Current;
                n.Query(new Collect());
                after = InteractionPass.Current;
                afterNode = after?.Node;
            };

            scene.Root.Notify(Key(), CallerInfo.Default);

            Assert.IsNotNull(before);
            Assert.AreSame(before, after);
            Assert.AreSame(scene.Front, afterNode);
        }

        [TestMethod]
        public void DispatchAndQuery_FromAQueryHandler_AreRejected()
        {
            using var scene = CreateScene();
            scene[scene.Front].OnNotify = n =>
            {
                if (n is KeyDownNotification)
                    n.Query(new Collect());
            };
            scene[scene.Child].OnNotify = n =>
            {
                if (n is not Collect)
                    return;

                n.Dispatch(new Ping(null!));
                n.Query(new Collect());
            };

            scene.Root.Notify(Key(), CallerInfo.Default);

            Assert.AreEqual(2, scene.Child.Warnings.Count);
            Assert.IsFalse(scene.Log.Any(e => e.EndsWith(":Ping")));
            Assert.AreEqual(2, scene.Log.Count(e => e.EndsWith(":Collect")));
        }

        // Hover

        [TestMethod]
        public void Hover_EntersOutermostFirst_AndOnlyTheTargetReceives()
        {
            using var scene = CreateScene();

            scene.Root.Notify(Move(35, 35), CallerInfo.Default);

            CollectionAssert.AreEqual(Entries("PointerEnter", "root", "front", "child"), scene.Hover);
            var enter = scene[scene.Child].Received.OfType<PointerEnterNotification>().Single();
            Assert.AreEqual(PointerIds.Mouse, enter.PointerId);
        }

        [TestMethod]
        public void Hover_MovingToAnotherNode_LeavesDeepestFirst()
        {
            using var scene = CreateScene();
            scene.Root.Notify(Move(35, 35), CallerInfo.Default);
            scene.Log.Clear();

            scene.Root.Notify(Move(15, 15), CallerInfo.Default);

            CollectionAssert.AreEqual(
                Entries("PointerLeave", "child", "front").Concat(Entries("PointerEnter", "back")).ToArray(),
                scene.Hover
            );
        }

        [TestMethod]
        public void Hover_MovingOffTheTree_LeavesEverything()
        {
            using var scene = CreateScene();
            scene.Root.Notify(Move(35, 35), CallerInfo.Default);
            scene.Log.Clear();

            scene.Root.Notify(Move(150, 150), CallerInfo.Default);

            CollectionAssert.AreEqual(Entries("PointerLeave", "child", "front", "root"), scene.Hover);
            Assert.AreEqual(0, scene.Root.Dispatcher.HoverPath.Count);
        }

        [TestMethod]
        public void Hover_MouseLost_LeavesEverything()
        {
            using var scene = CreateScene();
            scene.Root.Notify(Move(35, 35), CallerInfo.Default);
            scene.Log.Clear();

            scene.Root.Notify(MouseLost(), CallerInfo.Default);

            CollectionAssert.AreEqual(Entries("PointerLeave", "child", "front", "root"), scene.Hover);
        }

        [TestMethod]
        public void Hover_NodeHiddenWhileHovered_GetsItsLeave()
        {
            using var scene = CreateScene();
            scene.Root.Notify(Move(35, 35), CallerInfo.Default);
            scene.Front.Native->SetDisplay(YGDisplay.None);
            scene.Root.CalculateLayout();
            scene.Log.Clear();

            scene.Root.Notify(Move(35, 35), CallerInfo.Default);

            CollectionAssert.AreEqual(
                Entries("PointerLeave", "child", "front").Concat(Entries("PointerEnter", "back")).ToArray(),
                scene.Hover
            );
        }

        [TestMethod]
        public void Hover_PointerEventsNone_IsNotHovered()
        {
            using var scene = CreateScene();
            scene.Front.SetPointerEvents(PointerEvents.None);

            scene.Root.Notify(Move(35, 35), CallerInfo.Default);

            CollectionAssert.AreEqual(Entries("PointerEnter", "root", "back"), scene.Hover);
        }

        [TestMethod]
        public void Hover_PrimaryTouch_HoversUntilItLifts()
        {
            using var scene = CreateScene();

            scene.Root.Notify(Touch(TouchNotificationKind.TouchDown, 35, 35), CallerInfo.Default);
            CollectionAssert.AreEqual(Entries("PointerEnter", "root", "front", "child"), scene.Hover);
            Assert.AreEqual(7, scene[scene.Child].Received.OfType<PointerEnterNotification>().Single().PointerId);
            scene.Log.Clear();

            scene.Root.Notify(Touch(TouchNotificationKind.TouchUp, 35, 35), CallerInfo.Default);
            CollectionAssert.AreEqual(Entries("PointerLeave", "child", "front", "root"), scene.Hover);
        }

        // Touch promotion

        [TestMethod]
        public void Touch_OnTheTree_IsProcessed_OutsideItIsNot()
        {
            using var scene = CreateScene();

            Assert.IsTrue(scene.Root.Notify(Touch(TouchNotificationKind.TouchDown, 35, 35), CallerInfo.Default));
            Assert.IsFalse(scene.Root.Notify(Touch(TouchNotificationKind.TouchDown, 150, 150, id: 8), CallerInfo.Default));
        }

        [TestMethod]
        public void Mouse_DuringATouch_AndShortlyAfter_IsIgnored()
        {
            using var scene = CreateScene();
            var clock = new ManualClock();
            scene.Root.Dispatcher.SetClock(clock);

            scene.Root.Notify(Touch(TouchNotificationKind.TouchDown, 35, 35), CallerInfo.Default);
            Assert.IsFalse(scene.Root.Notify(Move(15, 15), CallerInfo.Default));

            scene.Root.Notify(Touch(TouchNotificationKind.TouchUp, 35, 35), CallerInfo.Default);
            clock.Advance(50);
            scene.Root.Notify(Move(15, 15), CallerInfo.Default);
            Assert.IsFalse(scene.Log.Any(e => e.EndsWith(":MouseMove")));

            clock.Advance(50);
            scene.Root.Notify(Move(15, 15), CallerInfo.Default);
            CollectionAssert.AreEqual(Entries("MouseMove", "back", "root"), scene.Log.Where(e => e.EndsWith(":MouseMove")).ToArray());
        }

        [TestMethod]
        public void MouseLost_IsNeverIgnored_AndForgetsTheTouches()
        {
            using var scene = CreateScene();
            scene.Root.Dispatcher.SetClock(new ManualClock());

            scene.Root.Notify(Touch(TouchNotificationKind.TouchDown, 35, 35), CallerInfo.Default);
            scene.Root.Notify(MouseLost(), CallerInfo.Default);
            scene.Root.Notify(Move(15, 15), CallerInfo.Default);

            Assert.IsTrue(scene.Log.Contains("root:MouseLost"));
            CollectionAssert.AreEqual(Entries("MouseMove", "back", "root"), scene.Log.Where(e => e.EndsWith(":MouseMove")).ToArray());
        }

        // Sender

        [TestMethod]
        public void Sender_TimeMs_FollowsTheDispatcherClock()
        {
            using var scene = CreateScene();
            var clock = new ManualClock();
            scene.Root.Dispatcher.SetClock(clock);
            clock.Advance(250);

            scene.Root.Notify(Key(), CallerInfo.Default);

            var sender = (FuNotificationSender)scene[scene.Front].Received.Single().Sender;
            Assert.AreEqual(250L, sender.TimeMs);
        }

        [TestMethod]
        public void Dispatcher_IsDroppedWhenTheRootGetsAParent()
        {
            using var scene = CreateScene();
            var other = scene.Add(new Node(), "other");
            var dispatcher = other.Dispatcher;

            scene.Root.SetChildren(Spread.Create(new[] { scene.Back, scene.Front, other }));
            other.SetParent(null);

            Assert.AreNotSame(dispatcher, other.Dispatcher);
        }
    }
}
