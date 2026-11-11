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
    /// Notification delivery through a Fu tree. A 100 x 100 root holds two 40 x 40 siblings:
    /// Back at (10, 10) and Front at (30, 30), drawn after Back, so Front covers it where they overlap.
    /// Every node has a recording layer and one recording behavior.
    /// </summary>
    [TestClass]
    public sealed unsafe class DeliveryTests
    {
        private sealed class Node : Renderable<Node>
        {
            public Node()
                : base(null!) { }

            public YGNode* Native => Handle;
        }

        private sealed class Recorder(string name, List<string> log) : ILayer
        {
            public bool Result { get; set; }

            public INotification? Received { get; private set; }

            public RectangleF? Bounds => null;

            public void Render(CallerInfo caller) { }

            public bool Notify(INotification notification, CallerInfo caller)
            {
                log.Add(name);
                Received = notification;
                return Result;
            }
        }

        private sealed class Ping(object from) : NotificationBase(from, Keys.None)
        {
            public override INotification WithSender(object sender) => new Ping(sender);

            public override INotification Transform(INotificationSpaceTransformer transformer) => this;
        }

        private sealed class PingAt(Vector2 at, object from) : NotificationWithPosition(at, new Vector2(100, 100), Keys.None, from)
        {
            public override INotification WithSender(object sender) => new PingAt(Position, sender);

            public override INotification Transform(INotificationSpaceTransformer transformer) => this;
        }

        private sealed class Scene : IDisposable
        {
            private readonly List<Node> _nodes = new();

            public List<string> Log { get; } = new();
            public Dictionary<string, Recorder> Recorders { get; } = new();
            public Node Root { get; } = new();
            public Node Back { get; } = new();
            public Node Front { get; } = new();

            public Node Add(Node node, string name)
            {
                _nodes.Add(node);
                var layer = new Recorder($"{name}.layer", Log);
                var behavior = new Recorder($"{name}.behavior", Log);
                Recorders[$"{name}.layer"] = layer;
                Recorders[$"{name}.behavior"] = behavior;
                node.SetLayer(layer);
                node.SetBehaviors(Spread.Create(new IBehavior[] { behavior }));
                return node;
            }

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

            scene.Root.Native->SetWidth(100);
            scene.Root.Native->SetHeight(100);
            Place(scene.Back, 10, 10, 40);
            Place(scene.Front, 30, 30, 40);
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

        private static MouseDownNotification Down(float x, float y) =>
            new(new Vector2(x, y), new Vector2(100, 100), MouseButtons.Left, Keys.None, null!);

        private static KeyDownNotification Key() => new(Keys.A, Keys.None, null!);

        private static MouseLostNotification MouseLost() => new(new Vector2(45, 45), new Vector2(100, 100), Keys.None, null!);

        private static Vector2 WorldPosition(INotification? notification) =>
            ((INotificationWithSpacePositions)notification!).PositionInWorldSpace;

        private static string[] All(params string[] names) =>
            names.SelectMany(n => new[] { $"{n}.layer", $"{n}.behavior" }).ToArray();

        // Hit path

        [TestMethod]
        public void Press_ReachesFrontMostNode_ThenBubblesToRoot()
        {
            using var scene = CreateScene();

            var processed = scene.Root.Notify(Down(45, 45), CallerInfo.Default);

            Assert.IsFalse(processed);
            CollectionAssert.AreEqual(All("front", "root"), scene.Log);
        }

        [TestMethod]
        public void Press_OnlyOverBack_ReachesBack()
        {
            using var scene = CreateScene();

            scene.Root.Notify(Down(15, 15), CallerInfo.Default);

            CollectionAssert.AreEqual(All("back", "root"), scene.Log);
        }

        [TestMethod]
        public void Press_OutsideEveryNode_DeliversNothing()
        {
            using var scene = CreateScene();

            var processed = scene.Root.Notify(Down(150, 150), CallerInfo.Default);

            Assert.IsFalse(processed);
            Assert.AreEqual(0, scene.Log.Count);
        }

        [TestMethod]
        public void Press_OnRightOrBottomEdge_Misses()
        {
            using var scene = CreateScene();

            scene.Root.Notify(Down(100, 80), CallerInfo.Default);
            scene.Root.Notify(Down(80, 100), CallerInfo.Default);
            Assert.AreEqual(0, scene.Log.Count);

            scene.Root.Notify(Down(99.5f, 80), CallerInfo.Default);
            CollectionAssert.AreEqual(All("root"), scene.Log);
        }

        [TestMethod]
        public void Press_OnOverflowingChild_HitsItAndBubblesToParent()
        {
            using var scene = CreateScene();
            Place(scene.Front, 80, 80, 40);
            scene.Root.CalculateLayout();

            scene.Root.Notify(Down(110, 110), CallerInfo.Default);

            CollectionAssert.AreEqual(All("front", "root"), scene.Log);
        }

        [TestMethod]
        public void Press_OnClippedPartOfChild_Misses()
        {
            using var scene = CreateScene();
            Place(scene.Front, 80, 80, 40);
            scene.Root.Native->SetOverflow(YGOverflow.Hidden);
            scene.Root.CalculateLayout();

            var processed = scene.Root.Notify(Down(110, 110), CallerInfo.Default);

            Assert.IsFalse(processed);
            Assert.AreEqual(0, scene.Log.Count);
        }

        [TestMethod]
        public void PointerEventsNone_PassesThroughToNodeBehind()
        {
            using var scene = CreateScene();
            scene.Front.SetPointerEvents(PointerEvents.None);

            scene.Root.Notify(Down(45, 45), CallerInfo.Default);

            CollectionAssert.AreEqual(All("back", "root"), scene.Log);
        }

        [TestMethod]
        public void PointerEventsNone_SkipsTheSubtree()
        {
            using var scene = CreateScene();
            scene.Root.SetPointerEvents(PointerEvents.None);

            scene.Root.Notify(Down(45, 45), CallerInfo.Default);

            Assert.AreEqual(0, scene.Log.Count);
        }

        [TestMethod]
        public void DisplayNone_IsNotHit()
        {
            using var scene = CreateScene();
            scene.Front.Native->SetDisplay(YGDisplay.None);

            scene.Root.Notify(Down(45, 45), CallerInfo.Default);

            CollectionAssert.AreEqual(All("back", "root"), scene.Log);
        }

        [TestMethod]
        public void NodeNotLaidOut_IsNotHit()
        {
            using var scene = CreateScene();
            var late = scene.Add(new Node(), "late");
            Place(late, 0, 0, 100);
            scene.Root.SetChildren(Spread.Create(new[] { scene.Back, scene.Front, late }));

            scene.Root.Notify(Down(45, 45), CallerInfo.Default);

            CollectionAssert.AreEqual(All("front", "root"), scene.Log);
        }

        [TestMethod]
        public void ScaledNode_IsHitInItsOwnSpace()
        {
            using var scene = CreateScene();
            scene.Front.SetTransformation(Matrix.Scaling(0.5f, 0.5f, 1));

            scene.Root.Notify(Down(35, 35), CallerInfo.Default);
            CollectionAssert.AreEqual(All("back", "root"), scene.Log);

            scene.Log.Clear();
            scene.Root.Notify(Down(45, 45), CallerInfo.Default);
            CollectionAssert.AreEqual(All("front", "root"), scene.Log);
            Assert.AreEqual(new Vector2(10, 10), WorldPosition(scene.Recorders["front.layer"].Received));
        }

        [TestMethod]
        public void RotatedNode_IsHitInItsOwnSpace()
        {
            using var scene = CreateScene();
            scene.Front.SetTransformation(Matrix.RotationZ(MathUtil.PiOverFour));

            scene.Root.Notify(Down(25, 50), CallerInfo.Default);
            CollectionAssert.AreEqual(All("front", "root"), scene.Log);

            scene.Log.Clear();
            scene.Root.Notify(Down(32, 32), CallerInfo.Default);
            CollectionAssert.AreEqual(All("back", "root"), scene.Log);
        }

        // Input semantics

        [TestMethod]
        public void ChildLayerProcessing_StopsTheNodeAndItsAncestors()
        {
            using var scene = CreateScene();
            scene.Recorders["front.layer"].Result = true;

            var processed = scene.Root.Notify(Down(45, 45), CallerInfo.Default);

            Assert.IsTrue(processed);
            CollectionAssert.AreEqual(new[] { "front.layer" }, scene.Log);
        }

        [TestMethod]
        public void LayerProcessing_SkipsTheNodesBehaviors()
        {
            using var scene = CreateScene();
            scene.Recorders["root.layer"].Result = true;

            var processed = scene.Root.Notify(Down(45, 45), CallerInfo.Default);

            Assert.IsTrue(processed);
            CollectionAssert.AreEqual(new[] { "front.layer", "front.behavior", "root.layer" }, scene.Log);
        }

        [TestMethod]
        public void BehaviorProcessing_StillRunsTheOtherBehaviors_AndStopsAncestors()
        {
            using var scene = CreateScene();
            var first = new Recorder("front.first", scene.Log) { Result = true };
            var second = new Recorder("front.second", scene.Log);
            scene.Front.SetBehaviors(Spread.Create(new IBehavior[] { first, second }));

            var processed = scene.Root.Notify(Down(45, 45), CallerInfo.Default);

            Assert.IsTrue(processed);
            CollectionAssert.AreEqual(new[] { "front.layer", "front.first", "front.second" }, scene.Log);
        }

        // Broadcast

        [TestMethod]
        public void Key_IsOfferedToEveryNode_FrontToBack()
        {
            using var scene = CreateScene();

            var processed = scene.Root.Notify(Key(), CallerInfo.Default);

            Assert.IsFalse(processed);
            CollectionAssert.AreEqual(All("front", "back", "root"), scene.Log);
        }

        [TestMethod]
        public void Key_StopsAtTheFirstChildThatProcessesIt()
        {
            using var scene = CreateScene();
            scene.Recorders["front.behavior"].Result = true;

            var processed = scene.Root.Notify(Key(), CallerInfo.Default);

            Assert.IsTrue(processed);
            CollectionAssert.AreEqual(All("front"), scene.Log);
        }

        [TestMethod]
        public void Key_SkipsHiddenNodes_ButNotPointerEventsNone()
        {
            using var scene = CreateScene();
            scene.Front.Native->SetDisplay(YGDisplay.None);
            scene.Back.SetPointerEvents(PointerEvents.None);

            scene.Root.Notify(Key(), CallerInfo.Default);

            CollectionAssert.AreEqual(All("back", "root"), scene.Log);
        }

        // Control

        [TestMethod]
        public void DeviceLoss_ReachesEveryNode_AndIsNeverProcessed()
        {
            using var scene = CreateScene();
            scene.Front.Native->SetDisplay(YGDisplay.None);
            scene.Back.SetPointerEvents(PointerEvents.None);
            scene.Recorders["front.layer"].Result = true;
            scene.Recorders["back.behavior"].Result = true;

            var processed = scene.Root.Notify(MouseLost(), CallerInfo.Default);

            Assert.IsFalse(processed);
            CollectionAssert.AreEqual(All("front", "back", "root"), scene.Log);
        }

        [TestMethod]
        public void FocusAndKeyboardLoss_AreControl()
        {
            using var scene = CreateScene();
            scene.Recorders["front.layer"].Result = true;

            Assert.IsFalse(scene.Root.Notify(new LostFocusNotification(null!, Keys.None), CallerInfo.Default));
            Assert.IsFalse(scene.Root.Notify(new KeyboardLostNotification(null!), CallerInfo.Default));
            CollectionAssert.AreEqual(All("front", "back", "root").Concat(All("front", "back", "root")).ToArray(), scene.Log);
        }

        // Target and fan-out

        [TestMethod]
        public void Target_ReachesOnlyTheTarget()
        {
            using var scene = CreateScene();
            var inner = scene.Add(new Node(), "inner");
            Place(inner, 0, 0, 10);
            scene.Front.SetChildren(Spread.Create(new[] { inner }));
            scene.Root.CalculateLayout();

            scene.Root.Deliver(Key(), CallerInfo.Default, new Delivery(DeliveryClass.Control, NotificationRoute.Target(scene.Front)));

            CollectionAssert.AreEqual(All("front"), scene.Log);
        }

        [TestMethod]
        public void FanOut_IgnoresProcessing_AndSkipsHiddenNodes()
        {
            using var scene = CreateScene();
            scene.Back.Native->SetDisplay(YGDisplay.None);
            scene.Recorders["front.layer"].Result = true;

            var processed = scene.Root.Deliver(
                Key(),
                CallerInfo.Default,
                new Delivery(DeliveryClass.FanOut, NotificationRoute.Broadcast(scene.Root))
            );

            Assert.IsFalse(processed);
            CollectionAssert.AreEqual(All("front", "root"), scene.Log);
        }

        // Sender

        [TestMethod]
        public void Sender_MapsIntoTheReceivingNodesSpace()
        {
            using var scene = CreateScene();
            var notification = Down(45, 45);

            scene.Root.Notify(notification, CallerInfo.Default with { Scaling = 2 });

            var received = scene.Recorders["front.behavior"].Received;
            var sender = (FuNotificationSender)received!.Sender;
            Assert.AreSame(scene.Front, sender.Node);
            Assert.AreEqual(2f, sender.Scaling);
            Assert.AreEqual(Matrix.Translation(-30, -30, 0), sender.WindowToNode.Value);
            Assert.AreEqual(Matrix.Identity, sender.WindowToParent.Value);
            Assert.AreEqual(new Vector2(15, 15), WorldPosition(received));
            Assert.AreEqual(new Vector2(45, 45), WorldPosition(scene.Recorders["root.layer"].Received));

            var spaces = (INotificationWithSpacePositions)received;
            Assert.AreEqual(notification.PositionInProjectionSpace, spaces.PositionInProjectionSpace);
            Assert.AreEqual(notification.PositionInNormalizedProjectionSpace, spaces.PositionInNormalizedProjectionSpace);
        }

        [TestMethod]
        public void Sender_OfNodeScaledToZero_HasNoMapping()
        {
            using var scene = CreateScene();
            scene.Front.SetTransformation(Matrix.Scaling(0));

            scene.Root.Notify(MouseLost(), CallerInfo.Default);

            var received = scene.Recorders["front.layer"].Received;
            var sender = (FuNotificationSender)received!.Sender;
            Assert.IsFalse(sender.WindowToNode.HasValue);
            Assert.IsTrue(sender.WindowToParent.HasValue);
            Assert.IsTrue(float.IsNaN(WorldPosition(received).X));
        }

        // Classification and entry

        [TestMethod]
        public void CustomPositionalNotification_TakesTheHitPath()
        {
            using var scene = CreateScene();

            scene.Root.Notify(new PingAt(new Vector2(15, 15), null!), CallerInfo.Default);

            CollectionAssert.AreEqual(All("back", "root"), scene.Log);
        }

        [TestMethod]
        public void CustomNotification_IsBroadcast()
        {
            using var scene = CreateScene();

            scene.Root.Notify(new Ping(null!), CallerInfo.Default);

            CollectionAssert.AreEqual(All("front", "back", "root"), scene.Log);
        }

        [TestMethod]
        public void NotifyOnChild_IsDeliveredFromTheRoot()
        {
            using var scene = CreateScene();

            scene.Front.Notify(Down(15, 15), CallerInfo.Default);

            CollectionAssert.AreEqual(All("back", "root"), scene.Log);
        }
    }
}
