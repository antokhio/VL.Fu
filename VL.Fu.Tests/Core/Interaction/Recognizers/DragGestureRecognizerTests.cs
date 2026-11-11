using SkiaSharp;
using Stride.Core.Mathematics;
using VL.Fu.Core.Interaction.Recognizers;
using VL.Lib.IO;

namespace VL.Fu.Tests.Core.Interaction.Recognizers
{
    /// <summary>
    /// Drag recognizer (N5): the phases, the offset from the press, the frozen parent space, filtering, no allocations.
    /// </summary>
    [TestClass]
    public sealed class DragGestureRecognizerTests
    {
        [TestMethod]
        public void PressMoveRelease_RunsThroughThePhases()
        {
            using var node = new TestNode();
            var mouse = new TestPointer(node);
            var drag = new DragGestureRecognizer();

            var pressed = drag.Process(mouse.Down(10, 10));
            Assert.AreEqual(DragPhase.Pressed, pressed.Phase);
            Assert.AreEqual(new Vector2(10, 10), pressed.StartInParent);
            Assert.AreEqual(Vector2.Zero, pressed.OffsetInParent);

            Assert.AreEqual(DragPhase.Unchanged, drag.Process(mouse.Move(13, 10)).Phase);
            Assert.AreEqual(DragPhase.Pressed, drag.Current.Phase);

            var started = drag.Process(mouse.Move(15, 10));
            Assert.AreEqual(DragPhase.Started, started.Phase);
            Assert.AreEqual(new Vector2(5, 0), started.OffsetInParent, "the offset is measured from the press");
            Assert.AreEqual(started, drag.Current);

            var moved = drag.Process(mouse.Move(16, 12));
            Assert.AreEqual(DragPhase.Moved, moved.Phase);
            Assert.AreEqual(new Vector2(16, 12), moved.PositionInParent);
            Assert.AreEqual(DragPhase.Moved, drag.Process(mouse.Move(16, 12)).Phase);

            var ended = drag.Process(mouse.Up(20, 30));
            Assert.AreEqual(DragPhase.Ended, ended.Phase);
            Assert.AreEqual(new Vector2(10, 20), ended.OffsetInParent, "the up position is the last one");
            Assert.AreEqual(new DragGestureRecognizer().Current, drag.Current);
        }

        [TestMethod]
        public void ReleaseBeforeThreshold_Releases()
        {
            using var node = new TestNode();
            var mouse = new TestPointer(node);
            var drag = new DragGestureRecognizer();

            drag.Process(mouse.Down(0, 0));
            drag.Process(mouse.Move(2, 0));
            Assert.AreEqual(DragPhase.Released, drag.Process(mouse.Up(2, 0)).Phase);
            Assert.AreEqual(DragPhase.Idle, drag.Current.Phase);
        }

        [TestMethod]
        public void Threshold_IsInDip()
        {
            using var node = new TestNode();
            var mouse = new TestPointer(node) { Scaling = 2f };
            var drag = new DragGestureRecognizer();

            drag.Process(mouse.Down(0, 0));
            Assert.AreEqual(DragPhase.Unchanged, drag.Process(mouse.Move(7, 0)).Phase, "7 px are 3.5 DIP at scaling 2");
            Assert.AreEqual(DragPhase.Started, drag.Process(mouse.Move(8, 0)).Phase, "8 px are 4 DIP at scaling 2");
        }

        [TestMethod]
        public void PositionsMapThroughTheParentSpaceFrozenAtPress()
        {
            using var node = new TestNode();
            var parentToWindow = SKMatrix.CreateRotationDegrees(90).PostConcat(SKMatrix.CreateScale(2, 2)).PostConcat(SKMatrix.CreateTranslation(100, 0));
            Assert.IsTrue(parentToWindow.TryInvert(out var windowToParent));
            var mouse = new TestPointer(node) { WindowToParent = windowToParent };
            var drag = new DragGestureRecognizer();

            var pressed = drag.Process(mouse.Down(100, 0));
            AssertClose(Vector2.Zero, pressed.StartInParent);

            mouse.WindowToParent = SKMatrix.Identity;
            var started = drag.Process(mouse.Move(100, 20));
            Assert.AreEqual(DragPhase.Started, started.Phase);
            AssertClose(new Vector2(10, 0), started.PositionInParent);
            AssertClose(new Vector2(10, 0), started.OffsetInParent);

            var moved = drag.Process(mouse.Move(80, 20));
            AssertClose(new Vector2(10, 10), moved.OffsetInParent);
        }

        [TestMethod]
        public void PressWhileParentIsScaledToZero_DoesNotPress()
        {
            using var node = new TestNode();
            var mouse = new TestPointer(node) { WindowToParent = null };
            var drag = new DragGestureRecognizer();

            Assert.AreEqual(DragPhase.Unchanged, drag.Process(mouse.Down(0, 0)).Phase);
            Assert.AreEqual(DragPhase.Idle, drag.Current.Phase);
        }

        [TestMethod]
        public void LaterSamplesWithoutParentSpace_StillMapThroughTheFrozenOne()
        {
            using var node = new TestNode();
            var mouse = new TestPointer(node);
            var drag = new DragGestureRecognizer();

            drag.Process(mouse.Down(0, 0));
            mouse.WindowToParent = null;
            Assert.AreEqual(new Vector2(10, 0), drag.Process(mouse.Move(10, 0)).PositionInParent);
            Assert.AreEqual(DragPhase.Ended, drag.Process(mouse.Up(10, 0)).Phase);
        }

        [TestMethod]
        public void OtherButtonPointerOrNode_IsUnchanged()
        {
            using var node = new TestNode();
            using var other = new TestNode();
            var mouse = new TestPointer(node);
            var touch = new TestPointer(node, 4);
            var elsewhere = new TestPointer(other);
            var drag = new DragGestureRecognizer();

            Assert.AreEqual(DragPhase.Unchanged, drag.Process(mouse.Down(0, 0, MouseButtons.Middle)).Phase);

            drag.Process(mouse.Down(0, 0));
            Assert.AreEqual(DragPhase.Unchanged, drag.Process(mouse.Down(0, 0, MouseButtons.Right)).Phase);
            Assert.AreEqual(DragPhase.Unchanged, drag.Process(mouse.Up(0, 0, MouseButtons.Right)).Phase);
            Assert.AreEqual(DragPhase.Unchanged, drag.Process(touch.Move(50, 0)).Phase);
            Assert.AreEqual(DragPhase.Unchanged, drag.Process(elsewhere.Move(50, 0)).Phase);
            Assert.AreEqual(DragPhase.Pressed, drag.Current.Phase);

            drag.Process(mouse.Move(10, 0));
            Assert.AreEqual(DragPhase.Unchanged, drag.Process(touch.Move(50, 0)).Phase);
            Assert.AreEqual(DragPhase.Unchanged, drag.Process(elsewhere.Up(50, 0)).Phase);
            Assert.AreEqual(DragPhase.Unchanged, drag.Process(mouse.Wheel(10, 0, 1)).Phase);
            Assert.AreEqual(DragPhase.Started, drag.Current.Phase);
        }

        [TestMethod]
        public void SettingsChangedDuringPress_ApplyFromTheNextPress()
        {
            using var node = new TestNode();
            var mouse = new TestPointer(node);
            var drag = new DragGestureRecognizer();

            drag.Process(mouse.Down(0, 0));
            drag.SetThresholdDip(50f);
            Assert.AreEqual(DragPhase.Started, drag.Process(mouse.Move(4, 0)).Phase);
            drag.Process(mouse.Up(4, 0));

            drag.Process(mouse.Down(0, 0));
            Assert.AreEqual(DragPhase.Unchanged, drag.Process(mouse.Move(40, 0)).Phase);
            Assert.AreEqual(DragPhase.Started, drag.Process(mouse.Move(50, 0)).Phase);
        }

        [TestMethod]
        public void Cancel_ReturnsToTheStateOfANewRecognizer_AndKeepsSettings()
        {
            using var node = new TestNode();
            var mouse = new TestPointer(node);
            var drag = new DragGestureRecognizer();
            drag.SetButtons(MouseButtons.Right);

            drag.Process(mouse.Down(5, 5, MouseButtons.Right));
            drag.Process(mouse.Move(50, 5));
            drag.Cancel();

            Assert.AreEqual(new DragGestureRecognizer().Current, drag.Current);
            Assert.AreEqual(MouseButtons.Right, drag.Buttons);
            Assert.AreEqual(DragPhase.Unchanged, drag.Process(mouse.Move(60, 5)).Phase);
            Assert.AreEqual(DragPhase.Unchanged, drag.Process(mouse.Up(60, 5, MouseButtons.Right)).Phase);
        }

        [TestMethod]
        public void Process_DoesNotAllocate()
        {
            using var node = new TestNode();
            var mouse = new TestPointer(node);
            var drag = new DragGestureRecognizer();
            var samples = new[] { mouse.Down(0, 0), mouse.Move(2, 0), mouse.Move(20, 0), mouse.Move(30, 0), mouse.Up(30, 0), mouse.Down(0, 0), mouse.Up(0, 0) };

            Run();
            var before = GC.GetAllocatedBytesForCurrentThread();
            Run();
            Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before);

            void Run()
            {
                for (var i = 0; i < samples.Length; i++)
                    drag.Process(in samples[i]);
                _ = drag.Current;
            }
        }

        private static void AssertClose(Vector2 expected, Vector2 actual)
        {
            Assert.AreEqual(expected.X, actual.X, 1e-4f, $"X of {actual}");
            Assert.AreEqual(expected.Y, actual.Y, 1e-4f, $"Y of {actual}");
        }
    }
}
