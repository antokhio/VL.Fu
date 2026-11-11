using SkiaSharp;
using Stride.Core.Mathematics;
using VL.Fu.Core.Interaction.Recognizers;
using VL.Lib.IO;

namespace VL.Fu.Tests.Core.Interaction.Recognizers
{
    /// <summary>
    /// Pinch recognizer (N5): pairing, the threshold, scale, unwrapped rotation, translation, the frozen parent space,
    /// ending on either lift, filtering, no allocations.
    /// </summary>
    [TestClass]
    public sealed class PinchGestureRecognizerTests
    {
        [TestMethod]
        public void TwoPresses_PairAndStartPastTheThreshold()
        {
            using var node = new TestNode();
            var a = new TestPointer(node, 1);
            var b = new TestPointer(node, 2);
            var pinch = new PinchGestureRecognizer();

            Assert.AreEqual(PinchPhase.Pressed, pinch.Process(a.Down(0, 0)).Phase);

            var paired = pinch.Process(b.Down(10, 0));
            Assert.AreEqual(PinchPhase.Paired, paired.Phase);
            Assert.AreEqual(1, paired.FirstPointerId);
            Assert.AreEqual(2, paired.SecondPointerId);
            Assert.AreEqual(new Vector2(5, 0), paired.CenterInParent);
            Assert.AreEqual(1f, paired.Scale);
            Assert.AreEqual(0f, paired.Rotation);
            Assert.AreEqual(Vector2.Zero, paired.TranslationInParent);

            Assert.AreEqual(PinchPhase.Unchanged, pinch.Process(b.Move(13, 0)).Phase);
            Assert.AreEqual(PinchPhase.Paired, pinch.Current.Phase);

            var started = pinch.Process(b.Move(14, 0));
            Assert.AreEqual(PinchPhase.Started, started.Phase);
            Assert.AreEqual(1.4f, started.Scale, 1e-5f);
            Assert.AreEqual(started, pinch.Current);

            Assert.AreEqual(PinchPhase.Moved, pinch.Process(a.Move(0, 1)).Phase);
        }

        [TestMethod]
        public void Spreading_ScalesAndTranslatesTheCenter()
        {
            using var node = new TestNode();
            var a = new TestPointer(node, 1);
            var b = new TestPointer(node, 2);
            var pinch = new PinchGestureRecognizer();

            pinch.Process(a.Down(0, 0));
            pinch.Process(b.Down(10, 0));
            var spread = pinch.Process(b.Move(20, 0));

            Assert.AreEqual(PinchPhase.Started, spread.Phase);
            Assert.AreEqual(2f, spread.Scale, 1e-5f);
            AssertClose(new Vector2(10, 0), spread.CenterInParent);
            AssertClose(new Vector2(5, 0), spread.TranslationInParent);
            Assert.AreEqual(0f, spread.Rotation, 1e-5f);
        }

        [TestMethod]
        public void Rotation_CountsPastHalfATurn()
        {
            using var node = new TestNode();
            var a = new TestPointer(node, 1);
            var b = new TestPointer(node, 2);
            var pinch = new PinchGestureRecognizer();

            pinch.Process(a.Down(0, 0));
            pinch.Process(b.Down(10, 0));
            Assert.AreEqual(MathF.PI / 2, pinch.Process(b.Move(0, 10)).Rotation, 1e-5f);
            Assert.AreEqual(MathF.PI, pinch.Process(b.Move(-10, 0)).Rotation, 1e-5f);
            var state = pinch.Process(b.Move(0, -10));

            Assert.AreEqual(PinchPhase.Moved, state.Phase);
            Assert.AreEqual(3 * MathF.PI / 2, state.Rotation, 1e-5f);
            Assert.AreEqual(1f, state.Scale, 1e-5f);
        }

        [TestMethod]
        public void BothPointers_MapThroughTheParentSpaceOfTheFirstPress()
        {
            using var node = new TestNode();
            Assert.IsTrue(SKMatrix.CreateScale(2, 2).TryInvert(out var windowToParent));
            var a = new TestPointer(node, 1) { WindowToParent = windowToParent };
            var b = new TestPointer(node, 2) { WindowToParent = SKMatrix.Identity };
            var pinch = new PinchGestureRecognizer();

            pinch.Process(a.Down(0, 0));
            var paired = pinch.Process(b.Down(20, 0));
            AssertClose(new Vector2(5, 0), paired.CenterInParent);

            a.WindowToParent = null;
            var moved = pinch.Process(b.Move(40, 0));
            AssertClose(new Vector2(10, 0), moved.CenterInParent);
            AssertClose(new Vector2(5, 0), moved.TranslationInParent);
            Assert.AreEqual(2f, moved.Scale, 1e-5f);
        }

        [TestMethod]
        public void PointersPressedAtOnePoint_KeepScaleOne()
        {
            using var node = new TestNode();
            var a = new TestPointer(node, 1);
            var b = new TestPointer(node, 2);
            var pinch = new PinchGestureRecognizer();

            pinch.Process(a.Down(5, 5));
            pinch.Process(b.Down(5, 5));
            var state = pinch.Process(b.Move(25, 5));

            Assert.AreEqual(PinchPhase.Started, state.Phase);
            Assert.AreEqual(1f, state.Scale);
            Assert.IsTrue(float.IsFinite(state.Rotation));
        }

        [TestMethod]
        public void EitherLift_EndsThePinch_AndTheOtherPointerIsNotAdopted()
        {
            using var node = new TestNode();
            var a = new TestPointer(node, 1);
            var b = new TestPointer(node, 2);
            var pinch = new PinchGestureRecognizer();

            pinch.Process(a.Down(0, 0));
            pinch.Process(b.Down(10, 0));
            pinch.Process(b.Move(20, 0));

            var ended = pinch.Process(a.Up(-10, 0));
            Assert.AreEqual(PinchPhase.Ended, ended.Phase);
            Assert.AreEqual(3f, ended.Scale, 1e-5f, "the up position is the last one");
            Assert.AreEqual(new PinchGestureRecognizer().Current, pinch.Current);

            Assert.AreEqual(PinchPhase.Unchanged, pinch.Process(b.Move(30, 0)).Phase);
            Assert.AreEqual(PinchPhase.Unchanged, pinch.Process(b.Up(30, 0)).Phase);
            Assert.AreEqual(PinchPhase.Idle, pinch.Current.Phase);
        }

        [TestMethod]
        public void LiftBeforeStart_Releases()
        {
            using var node = new TestNode();
            var a = new TestPointer(node, 1);
            var b = new TestPointer(node, 2);
            var pinch = new PinchGestureRecognizer();

            pinch.Process(a.Down(0, 0));
            Assert.AreEqual(PinchPhase.Released, pinch.Process(a.Up(0, 0)).Phase);

            pinch.Process(a.Down(0, 0));
            pinch.Process(b.Down(10, 0));
            Assert.AreEqual(PinchPhase.Released, pinch.Process(b.Up(10, 0)).Phase);
            Assert.AreEqual(PinchPhase.Idle, pinch.Current.Phase);
        }

        [TestMethod]
        public void FirstPointerMovingAlone_FollowsTheCenter_AndPairsFromWhereItIs()
        {
            using var node = new TestNode();
            var a = new TestPointer(node, 1);
            var b = new TestPointer(node, 2);
            var pinch = new PinchGestureRecognizer();

            pinch.Process(a.Down(0, 0));
            Assert.AreEqual(PinchPhase.Unchanged, pinch.Process(a.Move(30, 0)).Phase);
            Assert.AreEqual(new Vector2(30, 0), pinch.Current.CenterInParent);

            var paired = pinch.Process(b.Down(40, 0));
            Assert.AreEqual(new Vector2(35, 0), paired.CenterInParent);
            Assert.AreEqual(PinchPhase.Unchanged, pinch.Process(a.Move(32, 0)).Phase, "the anchors are where the pointers were when paired");
        }

        [TestMethod]
        public void ThirdPointerOtherNodeAndOtherButton_AreUnchanged()
        {
            using var node = new TestNode();
            using var other = new TestNode();
            var a = new TestPointer(node, 1);
            var b = new TestPointer(node, 2);
            var c = new TestPointer(node, 3);
            var elsewhere = new TestPointer(other, 2);
            var pinch = new PinchGestureRecognizer();

            pinch.Process(a.Down(0, 0));
            Assert.AreEqual(PinchPhase.Unchanged, pinch.Process(elsewhere.Down(10, 0)).Phase);
            Assert.AreEqual(PinchPhase.Pressed, pinch.Current.Phase);

            pinch.Process(b.Down(10, 0));
            Assert.AreEqual(PinchPhase.Unchanged, pinch.Process(c.Down(50, 50)).Phase);
            Assert.AreEqual(PinchPhase.Unchanged, pinch.Process(c.Move(90, 90)).Phase);
            Assert.AreEqual(PinchPhase.Unchanged, pinch.Process(elsewhere.Move(90, 90)).Phase);
            Assert.AreEqual(PinchPhase.Unchanged, pinch.Process(a.Up(0, 0, MouseButtons.Right)).Phase);
            Assert.AreEqual(PinchPhase.Paired, pinch.Current.Phase);

            pinch.Process(b.Move(20, 0));
            Assert.AreEqual(PinchPhase.Unchanged, pinch.Process(c.Up(90, 90)).Phase);
            Assert.AreEqual(PinchPhase.Started, pinch.Current.Phase);
        }

        [TestMethod]
        public void PressWhileParentIsScaledToZero_DoesNotPress()
        {
            using var node = new TestNode();
            var a = new TestPointer(node, 1) { WindowToParent = null };
            var pinch = new PinchGestureRecognizer();

            Assert.AreEqual(PinchPhase.Unchanged, pinch.Process(a.Down(0, 0)).Phase);
            Assert.AreEqual(PinchPhase.Idle, pinch.Current.Phase);
        }

        [TestMethod]
        public void ThresholdChangedDuringPress_AppliesFromTheNextPress()
        {
            using var node = new TestNode();
            var a = new TestPointer(node, 1);
            var b = new TestPointer(node, 2);
            var pinch = new PinchGestureRecognizer();

            pinch.Process(a.Down(0, 0));
            pinch.SetThresholdDip(50f);
            pinch.Process(b.Down(10, 0));
            Assert.AreEqual(PinchPhase.Started, pinch.Process(b.Move(14, 0)).Phase);
            pinch.Process(b.Up(14, 0));

            pinch.Process(a.Down(0, 0));
            pinch.Process(b.Down(10, 0));
            Assert.AreEqual(PinchPhase.Unchanged, pinch.Process(b.Move(14, 0)).Phase);
        }

        [TestMethod]
        public void Cancel_ReturnsToTheStateOfANewRecognizer_AndKeepsSettings()
        {
            using var node = new TestNode();
            var a = new TestPointer(node, 1);
            var b = new TestPointer(node, 2);
            var pinch = new PinchGestureRecognizer();
            pinch.SetThresholdDip(1f);

            pinch.Process(a.Down(0, 0));
            pinch.Process(b.Down(10, 0));
            pinch.Process(b.Move(0, 30));
            pinch.Cancel();

            Assert.AreEqual(new PinchGestureRecognizer().Current, pinch.Current);
            Assert.AreEqual(1f, pinch.Current.Scale);
            Assert.AreEqual(1f, pinch.ThresholdDip);
            Assert.AreEqual(PinchPhase.Unchanged, pinch.Process(b.Move(0, 40)).Phase);
        }

        [TestMethod]
        public void Process_DoesNotAllocate()
        {
            using var node = new TestNode();
            var a = new TestPointer(node, 1);
            var b = new TestPointer(node, 2);
            var pinch = new PinchGestureRecognizer();
            var samples = new[] { a.Down(0, 0), b.Down(10, 0), b.Move(12, 0), b.Move(20, 5), a.Move(-5, 0), a.Up(-5, 0), b.Up(20, 5) };

            Run();
            var before = GC.GetAllocatedBytesForCurrentThread();
            Run();
            Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before);

            void Run()
            {
                for (var i = 0; i < samples.Length; i++)
                    pinch.Process(in samples[i]);
                _ = pinch.Current;
            }
        }

        private static void AssertClose(Vector2 expected, Vector2 actual)
        {
            Assert.AreEqual(expected.X, actual.X, 1e-4f, $"X of {actual}");
            Assert.AreEqual(expected.Y, actual.Y, 1e-4f, $"Y of {actual}");
        }
    }
}
