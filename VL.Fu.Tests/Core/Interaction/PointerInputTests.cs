using SkiaSharp;
using Stride.Core.Mathematics;
using VL.Fu.Core.Interaction;
using VL.Fu.Tests.Core.Interaction.Recognizers;
using VL.Lib.IO;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Tests.Core.Interaction
{
    /// <summary>
    /// Pointer input (N5): which notifications <see cref="PointerInput.TryRead"/> turns into samples, and what the samples carry.
    /// The node sits at (10, 20) in a parent that is scaled by 2 in the window.
    /// </summary>
    [TestClass]
    public sealed class PointerInputTests
    {
        private static readonly Vector2 ClientArea = new(200, 100);

        private static readonly SKMatrix ParentToWindow = SKMatrix.CreateScale(2, 2);

        private static readonly SKMatrix NodeToWindow = SKMatrix.CreateTranslation(10, 20).PostConcat(ParentToWindow);

        [TestMethod]
        public void MouseDown_CarriesEverything()
        {
            using var node = new TestNode();
            var sender = Sender(node, scaling: 1.5f, timeMs: 1234);

            Assert.IsTrue(PointerInput.TryRead(new MouseDownNotification(new Vector2(40, 60), ClientArea, MouseButtons.Right, Keys.Shift, sender), out var sample));

            Assert.AreEqual(PointerSampleKind.Down, sample.Kind);
            Assert.AreEqual(PointerIds.Mouse, sample.PointerId);
            Assert.AreEqual(MouseButtons.Right, sample.Button);
            Assert.AreEqual(Keys.Shift, sample.Modifiers);
            Assert.AreEqual(new Vector2(40, 60), sample.PositionInWindow);
            Assert.AreEqual(Vector2.Zero, sample.WheelDelta);
            Assert.AreSame(node, sample.Node);
            Assert.AreEqual(1.5f, sample.Scaling);
            Assert.AreEqual(1234L, sample.TimeMs);

            Assert.IsTrue(sample.TryGetPositionInParent(out var inParent));
            AssertClose(new Vector2(20, 30), inParent);
            Assert.IsTrue(sample.TryGetPositionInNode(out var inNode));
            AssertClose(new Vector2(10, 10), inNode);
            Assert.AreEqual(4f, sample.ToDip(6f));
        }

        [TestMethod]
        public void MouseMoveAndUp_MapToMoveAndUp()
        {
            using var node = new TestNode();
            var sender = Sender(node);

            Assert.IsTrue(PointerInput.TryRead(new MouseMoveNotification(new Vector2(1, 2), ClientArea, Keys.None, sender), out var move));
            Assert.AreEqual(PointerSampleKind.Move, move.Kind);
            Assert.AreEqual(MouseButtons.None, move.Button, "moves don't say which buttons are held");

            Assert.IsTrue(PointerInput.TryRead(new MouseUpNotification(new Vector2(1, 2), ClientArea, MouseButtons.Middle, Keys.None, sender), out var up));
            Assert.AreEqual(PointerSampleKind.Up, up.Kind);
            Assert.AreEqual(MouseButtons.Middle, up.Button);
            Assert.AreEqual(PointerIds.Mouse, up.PointerId);
        }

        [TestMethod]
        public void Wheels_AreInNotches()
        {
            using var node = new TestNode();
            var sender = Sender(node);

            Assert.IsTrue(PointerInput.TryRead(new MouseWheelNotification(Vector2.Zero, ClientArea, 240, Keys.None, sender), out var vertical));
            Assert.AreEqual(PointerSampleKind.Wheel, vertical.Kind);
            Assert.AreEqual(new Vector2(0, 2), vertical.WheelDelta);
            Assert.AreEqual(MouseButtons.None, vertical.Button);

            Assert.IsTrue(PointerInput.TryRead(new MouseHorizontalWheelNotification(Vector2.Zero, ClientArea, -60, Keys.None, sender), out var horizontal));
            Assert.AreEqual(PointerSampleKind.Wheel, horizontal.Kind);
            Assert.AreEqual(new Vector2(-0.5f, 0), horizontal.WheelDelta);
        }

        [TestMethod]
        public void Touches_MapByKind_WithTheirId()
        {
            using var node = new TestNode();
            var sender = Sender(node);

            var expected = new[]
            {
                (TouchNotificationKind.TouchDown, PointerSampleKind.Down, MouseButtons.Left),
                (TouchNotificationKind.TouchMove, PointerSampleKind.Move, MouseButtons.None),
                (TouchNotificationKind.TouchUp, PointerSampleKind.Up, MouseButtons.Left),
            };

            foreach (var (touchKind, kind, button) in expected)
            {
                var touch = new TouchNotification(touchKind, new Vector2(3, 4), ClientArea, 17, true, Vector2.One, 1, Keys.None, sender);
                Assert.IsTrue(PointerInput.TryRead(touch, out var sample), touchKind.ToString());
                Assert.AreEqual(kind, sample.Kind, touchKind.ToString());
                Assert.AreEqual(button, sample.Button, touchKind.ToString());
                Assert.AreEqual(17, sample.PointerId, touchKind.ToString());
                Assert.AreEqual(new Vector2(3, 4), sample.PositionInWindow, touchKind.ToString());
            }
        }

        [TestMethod]
        public void SenderOtherThanFu_IsNotRead()
        {
            Assert.IsFalse(PointerInput.TryRead(new MouseDownNotification(Vector2.Zero, ClientArea, MouseButtons.Left, Keys.None, new object()), out var sample));
            Assert.AreEqual(default, sample.Kind);
            Assert.IsNull(sample.Node);
        }

        [TestMethod]
        public void ClicksAndKeys_AreNotRead()
        {
            using var node = new TestNode();
            var sender = Sender(node);

            Assert.IsFalse(PointerInput.TryRead(new MouseClickNotification(Vector2.Zero, ClientArea, MouseButtons.Left, 2, Keys.None, sender), out _));
            Assert.IsFalse(PointerInput.TryRead(new KeyDownNotification(Keys.A, Keys.None, sender), out _));
        }

        [TestMethod]
        public void NodeScaledToZero_HasNoNodeSpace()
        {
            using var node = new TestNode();
            var original = new MouseMoveNotification(Vector2.Zero, ClientArea, Keys.None, null!);
            var sender = new FuNotificationSender(node, SKMatrix.CreateScale(0, 0), ParentToWindow, 1f, original, 0);

            Assert.IsTrue(PointerInput.TryRead(new MouseMoveNotification(new Vector2(4, 4), ClientArea, Keys.None, sender), out var sample));
            Assert.IsNull(sample.WindowToNode);
            Assert.IsFalse(sample.TryGetPositionInNode(out _));
            Assert.IsTrue(sample.TryGetPositionInParent(out var inParent));
            AssertClose(new Vector2(2, 2), inParent);
        }

        [TestMethod]
        public void InvalidScaling_CountsAsOneForDip()
        {
            Assert.AreEqual(1f, PointerSample.DipScaling(0f));
            Assert.AreEqual(1f, PointerSample.DipScaling(-2f));
            Assert.AreEqual(1f, PointerSample.DipScaling(float.NaN));
            Assert.AreEqual(1f, PointerSample.DipScaling(float.PositiveInfinity));
            Assert.AreEqual(2f, PointerSample.DipScaling(2f));
            Assert.AreEqual(6f, new PointerSample { Scaling = float.NaN }.ToDip(6f));
        }

        private static FuNotificationSender Sender(IInteractive node, float scaling = 1f, long timeMs = 0)
        {
            var original = new MouseMoveNotification(Vector2.Zero, ClientArea, Keys.None, null!);
            return new FuNotificationSender(node, NodeToWindow, ParentToWindow, scaling, original, timeMs);
        }

        private static void AssertClose(Vector2 expected, Vector2 actual)
        {
            Assert.AreEqual(expected.X, actual.X, 1e-4f, $"X of {actual}");
            Assert.AreEqual(expected.Y, actual.Y, 1e-4f, $"Y of {actual}");
        }
    }
}
