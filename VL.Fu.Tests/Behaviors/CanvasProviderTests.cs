using Stride.Core.Mathematics;
using VL.Fu.Behaviors;
using VL.Fu.Core.Interaction;
using VL.Fu.Core.Interaction.Recognizers;
using VL.Lib.IO;

namespace VL.Fu.Tests.Behaviors
{
    /// <summary>
    /// <see cref="CanvasProvider"/> on Front (at (30, 30), 40 × 40) with Child (at Front's corner, 10 × 10) as the content:
    /// the right button pans, the wheel zooms about the cursor, two touches zoom about their center and pan with it.
    /// </summary>
    /// <remarks>
    /// The view isn't applied to Child, so hits don't change during a test. Results are checked where Child would show a point
    /// of its own with the view applied about its pivot, and compared with where the cursor or the touch on it is.
    /// </remarks>
    [TestClass]
    public sealed class CanvasProviderTests
    {
        private static readonly Vector2 ChildPosition = new(30, 30);
        private const float ChildSize = 10;

        [TestMethod]
        public void RightDrag_Pans_InTheCanvasSpace()
        {
            using var scene = new BehaviorScene();
            using var view = new Recorder<Matrix>(Matrix.Identity);
            scene.Attach(scene.Front, Connect(new CanvasProvider(null!), view));

            scene.Down(60, 60, MouseButtons.Right);
            scene.Move(70, 65);
            Assert.AreEqual(Matrix.Translation(10, 5, 0), view.Value);

            Assert.IsTrue(scene.Up(70, 65, MouseButtons.Right));
            Assert.IsNull(scene.Claims.ClaimOf(PointerIds.Mouse));
        }

        [TestMethod]
        public void RightDrag_WithinTheThreshold_WritesNothing()
        {
            using var scene = new BehaviorScene();
            using var view = new Recorder<Matrix>(Matrix.Identity);
            scene.Attach(scene.Front, Connect(new CanvasProvider(null!), view));

            scene.Down(60, 60, MouseButtons.Right);
            scene.Move(62, 60);
            scene.Up(62, 60, MouseButtons.Right);

            Assert.AreEqual(0, view.Values.Count);
        }

        [TestMethod]
        [DataRow(MouseButtons.Right, MouseButtons.Left)]
        [DataRow(MouseButtons.None, MouseButtons.Right)]
        public void DragWithAButtonThatDoesntPan_WritesNothing_AndLeavesThePointer(MouseButtons pan, MouseButtons pressed)
        {
            using var scene = new BehaviorScene();
            using var view = new Recorder<Matrix>(Matrix.Identity);
            var canvas = Connect(new CanvasProvider(null!), view);
            canvas.SetPan(pan);
            scene.Attach(scene.Front, canvas);

            scene.Down(60, 60, pressed);
            scene.Move(70, 65);

            Assert.AreEqual(0, view.Values.Count);
            Assert.IsNull(scene.Claims.ClaimOf(PointerIds.Mouse));
        }

        [TestMethod]
        public void Pan_UnderAScaledCanvas_IsInTheCanvasUnits()
        {
            using var scene = new BehaviorScene();
            using var view = new Recorder<Matrix>(Matrix.Identity);
            scene.Attach(scene.Front, Connect(new CanvasProvider(null!), view));

            // Front is scaled by half about its center (50, 50), so 10 pixels are 20 of its units.
            scene.Front.SetTransformation(Matrix.Scaling(0.5f, 0.5f, 1));

            scene.Down(55, 55, MouseButtons.Right);
            scene.Move(65, 55);

            Assert.AreEqual(Matrix.Translation(20, 0, 0), view.Value);
        }

        [TestMethod]
        [DataRow(0f, 0f)]
        [DataRow(-1f, -1f)]
        public void Wheel_ZoomsAboutTheCursor_WhateverTheOrigin(float originX, float originY)
        {
            using var scene = new BehaviorScene();
            using var view = new Recorder<Matrix>(Matrix.Identity);
            var origin = new Vector2(originX, originY);
            scene.Child.SetTransformOrigin(origin);
            scene.Attach(scene.Front, Connect(new CanvasProvider(null!), view));

            Assert.IsTrue(scene.Wheel(45, 45, 1));

            Assert.AreEqual(1.2f, GestureMath.ScaleOf(view.Value), 1e-5f);
            AssertClose(new Vector2(45, 45), Shown(view.Value, origin, new Vector2(15, 15)));
            AssertClose(new Vector2(27, 27), Shown(view.Value, origin, Vector2.Zero));
        }

        [TestMethod]
        public void Wheel_StopsAtTheLimits_AndIsStillProcessed()
        {
            using var scene = new BehaviorScene();
            using var view = new Recorder<Matrix>(Matrix.Identity);
            var canvas = Connect(new CanvasProvider(null!), view);
            canvas.SetZoom(0.5f, 1.5f, 2f);
            scene.Attach(scene.Front, canvas);

            scene.Wheel(45, 45, 1);
            Assert.AreEqual(1.5f, GestureMath.ScaleOf(view.Value), 1e-5f);
            Assert.IsTrue(scene.Wheel(45, 45, 1));
            Assert.AreEqual(1.5f, GestureMath.ScaleOf(view.Value), 1e-5f);

            scene.Wheel(45, 45, -1);
            scene.Wheel(45, 45, -1);
            Assert.AreEqual(0.5f, GestureMath.ScaleOf(view.Value), 1e-5f);
            Assert.IsTrue(scene.Wheel(45, 45, -1));
            Assert.AreEqual(0.5f, GestureMath.ScaleOf(view.Value), 1e-5f);
        }

        [TestMethod]
        public void Wheel_OutsideTheLimits_DoesntJump_AndMovesBackTowardsThem()
        {
            using var scene = new BehaviorScene();
            using var view = new Recorder<Matrix>(Matrix.Scaling(20, 20, 1));
            var canvas = Connect(new CanvasProvider(null!), view);
            canvas.SetZoom(0.1f, 10f, 2f);
            scene.Attach(scene.Front, canvas);

            scene.Wheel(45, 45, 1);
            Assert.AreEqual(20f, GestureMath.ScaleOf(view.Value), 1e-4f);

            scene.Wheel(45, 45, -1);
            Assert.AreEqual(10f, GestureMath.ScaleOf(view.Value), 1e-4f);
        }

        [TestMethod]
        public void Wheel_OverAnInnerCanvas_ZoomsOnlyTheInnerOne()
        {
            using var scene = new BehaviorScene();
            using var outer = new Recorder<Matrix>(Matrix.Identity);
            using var inner = new Recorder<Matrix>(Matrix.Identity);
            scene.Attach(scene.Front, Connect(new CanvasProvider(null!), outer));
            scene.Attach(scene.Child, Connect(new CanvasProvider(null!), inner));

            Assert.IsTrue(scene.Wheel(35, 35, 1));

            Assert.AreEqual(0, outer.Values.Count);
            Assert.AreEqual(1.2f, GestureMath.ScaleOf(inner.Value), 1e-5f);
        }

        [TestMethod]
        public void HorizontalWheel_AndAWheelStepOfOne_AreNotProcessed()
        {
            using var scene = new BehaviorScene();
            using var view = new Recorder<Matrix>(Matrix.Identity);
            var canvas = Connect(new CanvasProvider(null!), view);
            scene.Attach(scene.Front, canvas);

            Assert.IsFalse(scene.HorizontalWheel(45, 45, 1));

            canvas.SetZoom(wheelStep: 1f);
            Assert.IsFalse(scene.Wheel(45, 45, 1));

            Assert.AreEqual(0, view.Values.Count);
        }

        [TestMethod]
        [DataRow(0f, 0f)]
        [DataRow(-1f, -1f)]
        public void Pinch_KeepsWhatIsUnderTheTouchesUnderThem_WhateverTheOrigin(float originX, float originY)
        {
            using var scene = new BehaviorScene();
            using var view = new Recorder<Matrix>(Matrix.Identity);
            var origin = new Vector2(originX, originY);
            scene.Child.SetTransformOrigin(origin);
            scene.Attach(scene.Front, Connect(new CanvasProvider(null!), view));

            scene.TouchDown(7, 45, 45);
            scene.TouchDown(8, 65, 65);
            scene.TouchMove(8, 75, 75);

            // Scaled by 1.5 about (55, 55), which moved to (60, 60).
            AssertClose(new Vector2(45, 45), Shown(view.Value, origin, new Vector2(15, 15)));
            AssertClose(new Vector2(75, 75), Shown(view.Value, origin, new Vector2(35, 35)));

            Assert.IsTrue(scene.TouchUp(8, 75, 75));
            Assert.IsNull(scene.Claims.ClaimOf(7));
            Assert.IsNull(scene.Claims.ClaimOf(8));
        }

        [TestMethod]
        public void Pinch_IgnoresTheRotation()
        {
            using var scene = new BehaviorScene();
            using var view = new Recorder<Matrix>(Matrix.Identity);
            scene.Attach(scene.Front, Connect(new CanvasProvider(null!), view));

            scene.TouchDown(7, 45, 50);
            scene.TouchDown(8, 65, 50);
            scene.TouchMove(8, 45, 70);

            // A quarter turn at the same distance: the center moved from (55, 50) to (45, 60), and that is all.
            AssertClose(new Vector2(-10, 10), new Vector2(view.Value.M41, view.Value.M42));
            Assert.AreEqual(1f, view.Value.M11, 1e-5f);
            Assert.AreEqual(0f, view.Value.M12, 1e-5f);
        }

        [TestMethod]
        public void Pinch_StopsAtTheLimit()
        {
            using var scene = new BehaviorScene();
            using var view = new Recorder<Matrix>(Matrix.Identity);
            var canvas = Connect(new CanvasProvider(null!), view);
            canvas.SetZoom(0.1f, 1.2f);
            scene.Attach(scene.Front, canvas);

            scene.TouchDown(7, 45, 45);
            scene.TouchDown(8, 65, 65);
            scene.TouchMove(8, 75, 75);

            Assert.AreEqual(1.2f, GestureMath.ScaleOf(view.Value), 1e-5f);
        }

        [TestMethod]
        public void Pinch_TakesATouchFromAnItemsMove_AndTheItemReturns()
        {
            using var scene = new BehaviorScene();
            using var view = new Recorder<Matrix>(Matrix.Identity);
            using var position = new Recorder<Vector2>(new Vector2(5, 5));
            var movable = new Movable(null!);
            movable.SetPosition(position.Channel);
            var canvas = Connect(new CanvasProvider(null!), view);
            scene.Attach(scene.Child, movable);
            scene.Attach(scene.Front, canvas);

            scene.TouchDown(7, 35, 35);
            scene.TouchMove(7, 45, 35);
            Assert.AreEqual(new Vector2(15, 5), position.Value);

            scene.TouchDown(8, 60, 60);
            scene.TouchMove(8, 66, 66);

            Assert.AreEqual(new Vector2(5, 5), position.Value);
            Assert.IsTrue(scene.Claims.ClaimOf(7)!.HasMember(canvas));
            Assert.IsTrue(view.Values.Count > 0);
        }

        [TestMethod]
        public void Pinch_DuringAPan_DoesntStart()
        {
            using var scene = new BehaviorScene();
            using var view = new Recorder<Matrix>(Matrix.Identity);
            scene.Attach(scene.Front, Connect(new CanvasProvider(null!), view));
            scene.Down(60, 60, MouseButtons.Right);
            scene.Move(70, 65);

            scene.TouchDown(7, 45, 45);
            scene.TouchDown(8, 65, 65);
            scene.TouchMove(8, 75, 75);

            Assert.AreEqual(Matrix.Translation(10, 5, 0), view.Value);
            Assert.IsNull(scene.Claims.ClaimOf(8));
        }

        [TestMethod]
        public void Pan_DuringAPinch_DoesntStart()
        {
            using var scene = new BehaviorScene();
            using var view = new Recorder<Matrix>(Matrix.Identity);
            scene.Attach(scene.Front, Connect(new CanvasProvider(null!), view));
            scene.TouchDown(7, 45, 45);
            scene.TouchDown(8, 65, 65);
            scene.TouchMove(8, 75, 75);
            var pinched = view.Value;

            scene.Down(60, 60, MouseButtons.Right);
            scene.Move(70, 65);

            Assert.AreEqual(pinched, view.Value);
            Assert.IsNull(scene.Claims.ClaimOf(PointerIds.Mouse));
        }

        [TestMethod]
        public void Wheel_DuringAPan_IsProcessed_AndChangesNothing()
        {
            using var scene = new BehaviorScene();
            using var view = new Recorder<Matrix>(Matrix.Identity);
            scene.Attach(scene.Front, Connect(new CanvasProvider(null!), view));
            scene.Down(60, 60, MouseButtons.Right);
            scene.Move(70, 65);

            Assert.IsTrue(scene.Wheel(70, 65, 1));

            Assert.AreEqual(Matrix.Translation(10, 5, 0), view.Value);
        }

        [TestMethod]
        public void Escape_ReturnsToTheViewAtTheStart()
        {
            using var scene = new BehaviorScene();
            var start = Matrix.Scaling(2, 2, 1);
            using var view = new Recorder<Matrix>(start);
            scene.Attach(scene.Front, Connect(new CanvasProvider(null!), view));
            scene.Down(60, 60, MouseButtons.Right);
            scene.Move(70, 65);
            Assert.AreNotEqual(start, view.Value);

            scene.Escape();

            Assert.AreEqual(start, view.Value);
            scene.Move(80, 65);
            Assert.AreEqual(start, view.Value);
        }

        [TestMethod]
        public void Disabling_DuringAPinch_ReturnsToTheViewAtTheStart_AndLetsGoOfBothTouches()
        {
            using var scene = new BehaviorScene();
            using var view = new Recorder<Matrix>(Matrix.Identity);
            var canvas = Connect(new CanvasProvider(null!), view);
            scene.Attach(scene.Front, canvas);
            scene.TouchDown(7, 45, 45);
            scene.TouchDown(8, 65, 65);
            scene.TouchMove(8, 75, 75);
            Assert.AreNotEqual(Matrix.Identity, view.Value);

            canvas.SetEnabled(false);

            Assert.AreEqual(Matrix.Identity, view.Value);
            scene.TouchMove(8, 80, 80);
            Assert.IsNull(scene.Claims.ClaimOf(7));
            Assert.IsNull(scene.Claims.ClaimOf(8));
            Assert.AreEqual(Matrix.Identity, view.Value);
        }

        [TestMethod]
        public void SwappingTheChannel_DuringAPan_RestoresTheOldOne_AndLeavesTheNewOneUntouched()
        {
            using var scene = new BehaviorScene();
            using var view = new Recorder<Matrix>(Matrix.Identity);
            using var next = new Recorder<Matrix>(Matrix.Scaling(3, 3, 1));
            var canvas = Connect(new CanvasProvider(null!), view);
            scene.Attach(scene.Front, canvas);
            scene.Down(60, 60, MouseButtons.Right);
            scene.Move(70, 65);

            canvas.SetView(next.Channel);

            Assert.AreEqual(Matrix.Identity, view.Value);
            scene.Move(80, 65);
            Assert.IsNull(scene.Claims.ClaimOf(PointerIds.Mouse));
            Assert.AreEqual(0, next.Values.Count);
        }

        [TestMethod]
        [DataRow(0f, 10f, 1.2f)]
        [DataRow(2f, 1f, 1.2f)]
        [DataRow(0.1f, float.PositiveInfinity, 1.2f)]
        [DataRow(0.1f, 10f, 0f)]
        [DataRow(0.1f, 10f, float.NaN)]
        public void InvalidZoom_IsIgnored(float min, float max, float wheelStep)
        {
            var canvas = new CanvasProvider(null!);
            canvas.SetZoom(0.5f, 4f, 2f);

            canvas.SetZoom(min, max, wheelStep);

            Assert.AreEqual(0.5f, canvas.MinZoom);
            Assert.AreEqual(4f, canvas.MaxZoom);
            Assert.AreEqual(2f, canvas.WheelStep);
        }

        private static CanvasProvider Connect(CanvasProvider canvas, Recorder<Matrix> view)
        {
            canvas.SetView(view.Channel);
            return canvas;
        }

        /// <summary>
        /// Where Child shows <paramref name="pointInChild"/> on screen with <paramref name="view"/> applied about the pivot
        /// that <paramref name="origin"/> picks.
        /// </summary>
        private static Vector2 Shown(Matrix view, Vector2 origin, Vector2 pointInChild) =>
            TransformableTests.Shown(ChildPosition, ChildSize, view, origin, pointInChild);

        private static void AssertClose(Vector2 expected, Vector2 actual) => TransformableTests.AssertClose(expected, actual);
    }
}
