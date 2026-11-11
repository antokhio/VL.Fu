using VL.Fu.Core.Interaction;
using VL.Fu.Core.Interaction.Recognizers;
using VL.Lib.IO;

namespace VL.Fu.Tests.Core.Interaction.Recognizers
{
    /// <summary>
    /// Click recognizer (N5): press and release within the threshold, slipping, frozen settings, filtering, no allocations.
    /// </summary>
    [TestClass]
    public sealed class ClickGestureRecognizerTests
    {
        [TestMethod]
        public void PressAndRelease_InPlace_Clicks()
        {
            using var node = new TestNode();
            var mouse = new TestPointer(node);
            var click = new ClickGestureRecognizer();

            var pressed = click.Process(mouse.Down(10, 20));
            Assert.AreEqual(ClickPhase.Pressed, pressed.Phase);
            Assert.AreEqual(PointerIds.Mouse, pressed.PointerId);
            Assert.AreEqual(new Stride.Core.Mathematics.Vector2(10, 20), pressed.PressInWindow);
            Assert.AreEqual(pressed, click.Current);

            Assert.AreEqual(ClickPhase.Unchanged, click.Process(mouse.Move(12, 20)).Phase);
            Assert.AreEqual(ClickPhase.Clicked, click.Process(mouse.Up(12, 20)).Phase);
            Assert.AreEqual(new ClickGestureRecognizer().Current, click.Current);
        }

        [TestMethod]
        public void MovePastThreshold_Slips_AndReleasesWithoutClick_EvenAfterComingBack()
        {
            using var node = new TestNode();
            var mouse = new TestPointer(node);
            var click = new ClickGestureRecognizer();

            click.Process(mouse.Down(0, 0));
            Assert.AreEqual(ClickPhase.Slipped, click.Process(mouse.Move(6, 0)).Phase);
            Assert.AreEqual(ClickPhase.Unchanged, click.Process(mouse.Move(20, 0)).Phase);
            Assert.AreEqual(ClickPhase.Unchanged, click.Process(mouse.Move(0, 0)).Phase);
            Assert.AreEqual(ClickPhase.Released, click.Process(mouse.Up(0, 0)).Phase);
            Assert.AreEqual(ClickPhase.Idle, click.Current.Phase);
        }

        [TestMethod]
        public void UpPastThreshold_WithoutMove_Releases()
        {
            using var node = new TestNode();
            var mouse = new TestPointer(node);
            var click = new ClickGestureRecognizer();

            click.Process(mouse.Down(0, 0));
            Assert.AreEqual(ClickPhase.Released, click.Process(mouse.Up(0, 30)).Phase);
        }

        [TestMethod]
        public void Threshold_IsInDip()
        {
            using var node = new TestNode();
            var mouse = new TestPointer(node) { Scaling = 2f };
            var click = new ClickGestureRecognizer();

            click.Process(mouse.Down(0, 0));
            Assert.AreEqual(ClickPhase.Unchanged, click.Process(mouse.Move(10, 0)).Phase, "10 px are 5 DIP at scaling 2");
            Assert.AreEqual(ClickPhase.Clicked, click.Process(mouse.Up(10, 0)).Phase);

            click.Process(mouse.Down(0, 0));
            Assert.AreEqual(ClickPhase.Slipped, click.Process(mouse.Move(12, 0)).Phase, "12 px are 6 DIP at scaling 2");
        }

        [TestMethod]
        public void InvalidScaling_CountsAsOne()
        {
            using var node = new TestNode();
            var mouse = new TestPointer(node) { Scaling = float.NaN };
            var click = new ClickGestureRecognizer();

            click.Process(mouse.Down(0, 0));
            Assert.AreEqual(ClickPhase.Slipped, click.Process(mouse.Move(6, 0)).Phase);
        }

        [TestMethod]
        public void OtherButton_DoesNotPress()
        {
            using var node = new TestNode();
            var mouse = new TestPointer(node);
            var click = new ClickGestureRecognizer();

            Assert.AreEqual(ClickPhase.Unchanged, click.Process(mouse.Down(0, 0, MouseButtons.Right)).Phase);
            Assert.AreEqual(ClickPhase.Idle, click.Current.Phase);

            click.SetButtons(MouseButtons.Left | MouseButtons.Right);
            Assert.AreEqual(ClickPhase.Pressed, click.Process(mouse.Down(0, 0, MouseButtons.Right)).Phase);
            Assert.AreEqual(ClickPhase.Clicked, click.Process(mouse.Up(0, 0, MouseButtons.Right)).Phase);
        }

        [TestMethod]
        public void Chord_OnlyThePressedButtonsUpClicks()
        {
            using var node = new TestNode();
            var mouse = new TestPointer(node);
            var click = new ClickGestureRecognizer();
            click.SetButtons(MouseButtons.Left | MouseButtons.Right);

            click.Process(mouse.Down(0, 0, MouseButtons.Left));
            Assert.AreEqual(ClickPhase.Unchanged, click.Process(mouse.Down(0, 0, MouseButtons.Right)).Phase);
            Assert.AreEqual(ClickPhase.Unchanged, click.Process(mouse.Up(0, 0, MouseButtons.Right)).Phase);
            Assert.AreEqual(ClickPhase.Clicked, click.Process(mouse.Up(0, 0, MouseButtons.Left)).Phase);
        }

        [TestMethod]
        public void OtherPointerOrNode_IsUnchanged()
        {
            using var node = new TestNode();
            using var other = new TestNode();
            var mouse = new TestPointer(node);
            var touch = new TestPointer(node, 7);
            var elsewhere = new TestPointer(other);
            var click = new ClickGestureRecognizer();

            click.Process(mouse.Down(0, 0));
            Assert.AreEqual(ClickPhase.Unchanged, click.Process(touch.Move(50, 0)).Phase);
            Assert.AreEqual(ClickPhase.Unchanged, click.Process(touch.Up(50, 0)).Phase);
            Assert.AreEqual(ClickPhase.Unchanged, click.Process(elsewhere.Move(50, 0)).Phase);
            Assert.AreEqual(ClickPhase.Unchanged, click.Process(elsewhere.Up(50, 0)).Phase);
            Assert.AreEqual(ClickPhase.Pressed, click.Current.Phase);
            Assert.AreEqual(ClickPhase.Clicked, click.Process(mouse.Up(0, 0)).Phase);
        }

        [TestMethod]
        public void Wheel_IsUnchanged()
        {
            using var node = new TestNode();
            var mouse = new TestPointer(node);
            var click = new ClickGestureRecognizer();

            Assert.AreEqual(ClickPhase.Unchanged, click.Process(mouse.Wheel(0, 0, 1)).Phase);
            click.Process(mouse.Down(0, 0));
            Assert.AreEqual(ClickPhase.Unchanged, click.Process(mouse.Wheel(0, 0, 1)).Phase);
            Assert.AreEqual(ClickPhase.Pressed, click.Current.Phase);
        }

        [TestMethod]
        public void SettingsChangedDuringPress_ApplyFromTheNextPress()
        {
            using var node = new TestNode();
            var mouse = new TestPointer(node);
            var click = new ClickGestureRecognizer();

            click.Process(mouse.Down(0, 0));
            click.SetThresholdDip(100f);
            click.SetButtons(MouseButtons.Right);
            Assert.AreEqual(ClickPhase.Slipped, click.Process(mouse.Move(10, 0)).Phase);
            Assert.AreEqual(ClickPhase.Released, click.Process(mouse.Up(10, 0)).Phase);

            Assert.AreEqual(ClickPhase.Unchanged, click.Process(mouse.Down(0, 0)).Phase);
            click.Process(mouse.Down(0, 0, MouseButtons.Right));
            Assert.AreEqual(ClickPhase.Unchanged, click.Process(mouse.Move(10, 0)).Phase);
            Assert.AreEqual(ClickPhase.Clicked, click.Process(mouse.Up(10, 0, MouseButtons.Right)).Phase);
        }

        [TestMethod]
        public void InvalidThreshold_IsIgnored()
        {
            var click = new ClickGestureRecognizer();

            click.SetThresholdDip(-1f);
            click.SetThresholdDip(float.NaN);
            click.SetThresholdDip(float.PositiveInfinity);
            Assert.AreEqual(6f, click.ThresholdDip);

            click.SetThresholdDip(0f);
            Assert.AreEqual(0f, click.ThresholdDip);
        }

        [TestMethod]
        public void Cancel_ReturnsToTheStateOfANewRecognizer_AndKeepsSettings()
        {
            using var node = new TestNode();
            var mouse = new TestPointer(node, 3);
            var click = new ClickGestureRecognizer();
            click.SetThresholdDip(2f);

            click.Process(mouse.Down(40, 50));
            click.Process(mouse.Move(60, 50));
            click.Cancel();

            Assert.AreEqual(new ClickGestureRecognizer().Current, click.Current);
            Assert.AreEqual(2f, click.ThresholdDip);
            Assert.AreEqual(ClickPhase.Unchanged, click.Process(mouse.Up(60, 50)).Phase);
        }

        [TestMethod]
        public void Process_DoesNotAllocate()
        {
            using var node = new TestNode();
            var mouse = new TestPointer(node);
            var click = new ClickGestureRecognizer();
            var samples = new[] { mouse.Down(0, 0), mouse.Move(2, 0), mouse.Move(20, 0), mouse.Up(20, 0), mouse.Down(0, 0), mouse.Up(0, 0) };

            Run();
            var before = GC.GetAllocatedBytesForCurrentThread();
            Run();
            Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before);

            void Run()
            {
                for (var i = 0; i < samples.Length; i++)
                    click.Process(in samples[i]);
                _ = click.Current;
            }
        }
    }
}
