using Stride.Core.Mathematics;
using VL.Core;
using VL.Fu.Behaviors;
using VL.Lib.Collections;
using VL.Lib.IO;
using VL.Skia;

namespace VL.Fu.Tests.Behaviors
{
    /// <summary>
    /// <see cref="Selectable"/> and <see cref="SelectionProvider"/>: the left-button rows of the canvas interaction table,
    /// the marquee, the bounds, and the scope rules (S1, S3).
    /// </summary>
    /// <remarks>
    /// The provider is on Root; Back (10–50) and Front (30–70) are its items. (15, 15) presses Back, (60, 60) presses Front,
    /// and (90, 10) and (60, 20) are background, the second inside the bounds of Back and Front together.
    /// </remarks>
    [TestClass]
    public sealed class SelectionTests
    {
        private sealed class Selection : IDisposable
        {
            public Selection(bool attach = true)
            {
                Provider.SetSelectionBounds(Bounds.Channel);
                Provider.SetMarqueeRect(Marquee.Channel);
                BackItem.SetIsSelected(Back.Channel);
                FrontItem.SetIsSelected(Front.Channel);
                if (!attach)
                    return;

                Scene.Attach(Scene.Root, Provider);
                Scene.Attach(Scene.Back, BackItem);
                Scene.Attach(Scene.Front, FrontItem);
            }

            public BehaviorScene Scene { get; } = new();
            public SelectionProvider Provider { get; } = new(null!);
            public Selectable BackItem { get; } = new(null!);
            public Selectable FrontItem { get; } = new(null!);
            public Recorder<bool> Back { get; } = new(false);
            public Recorder<bool> Front { get; } = new(false);
            public Recorder<Optional<RectangleF>> Bounds { get; } = new(default);
            public Recorder<Optional<RectangleF>> Marquee { get; } = new(default);

            public void Select(bool back, bool front)
            {
                Back.Channel.Value = back;
                Front.Channel.Value = front;
            }

            public bool Click(float x, float y, Keys modifiers = Keys.None, MouseButtons button = MouseButtons.Left)
            {
                Scene.Down(x, y, button, modifiers);
                return Scene.Up(x, y, button, modifiers);
            }

            public void Drag(float fromX, float fromY, float toX, float toY, Keys modifiers = Keys.None)
            {
                Scene.Down(fromX, fromY, MouseButtons.Left, modifiers);
                Scene.Move((fromX + toX) / 2, (fromY + toY) / 2, modifiers);
                Scene.Move(toX, toY, modifiers);
                Scene.Up(toX, toY, MouseButtons.Left, modifiers);
            }

            public void Dispose()
            {
                Scene.Dispose();
                Back.Dispose();
                Front.Dispose();
                Bounds.Dispose();
                Marquee.Dispose();
            }
        }

        [TestMethod]
        public void Press_OnUnselectedItem_SelectsIt_AndDeselectsTheOthers()
        {
            using var s = new Selection();
            s.Select(back: false, front: true);

            Assert.IsFalse(s.Scene.Down(15, 15));

            Assert.IsTrue(s.Back.Value);
            Assert.IsFalse(s.Front.Value);
        }

        [TestMethod]
        public void ShiftPress_Adds_AndCtrlPress_Toggles()
        {
            using var s = new Selection();
            s.Select(back: false, front: true);

            s.Scene.Down(15, 15, MouseButtons.Left, Keys.Shift);
            Assert.IsTrue(s.Back.Value);
            Assert.IsTrue(s.Front.Value);
            s.Scene.Up(15, 15);

            s.Select(back: false, front: true);
            s.Scene.Down(15, 15, MouseButtons.Left, Keys.Control);
            Assert.IsTrue(s.Back.Value);
            Assert.IsTrue(s.Front.Value);
        }

        [TestMethod]
        public void SingleMode_ShiftPress_Replaces()
        {
            using var s = new Selection();
            s.Provider.SetMode(SelectionMode.Single);
            s.Select(back: false, front: true);

            s.Scene.Down(15, 15, MouseButtons.Left, Keys.Shift);

            Assert.IsTrue(s.Back.Value);
            Assert.IsFalse(s.Front.Value);
        }

        [TestMethod]
        public void PressAndRelease_OnUnselectedItem_SelectsOnce_AndIsProcessed()
        {
            using var s = new Selection();
            s.Select(back: false, front: true);
            s.Back.Values.Clear();

            Assert.IsTrue(s.Click(15, 15));

            CollectionAssert.AreEqual(new[] { true }, s.Back.Values);
            Assert.IsFalse(s.Front.Value);
        }

        [TestMethod]
        public void Click_OnSelectedItem_CollapsesTheSelectionOnRelease()
        {
            using var s = new Selection();
            s.Select(back: true, front: true);

            s.Scene.Down(15, 15);
            Assert.IsTrue(s.Front.Value, "A press on a selected item keeps the selection, so a drag could move it.");

            Assert.IsTrue(s.Scene.Up(15, 15));
            Assert.IsTrue(s.Back.Value);
            Assert.IsFalse(s.Front.Value);
        }

        [TestMethod]
        public void CtrlClick_OnSelectedItem_DeselectsIt_AndShiftClick_KeepsTheSelection()
        {
            using var s = new Selection();
            s.Select(back: true, front: true);

            s.Click(15, 15, Keys.Shift);
            Assert.IsTrue(s.Back.Value);
            Assert.IsTrue(s.Front.Value);

            s.Click(15, 15, Keys.Control);
            Assert.IsFalse(s.Back.Value);
            Assert.IsTrue(s.Front.Value);
        }

        [TestMethod]
        public void BackgroundClick_Clears_AlsoInsideTheBounds()
        {
            using var s = new Selection();
            s.Select(back: true, front: true);

            Assert.IsTrue(s.Click(60, 20));
            Assert.IsFalse(s.Back.Value);
            Assert.IsFalse(s.Front.Value);

            s.Select(back: true, front: true);
            s.Click(90, 10);
            Assert.IsFalse(s.Back.Value);
            Assert.IsFalse(s.Front.Value);
        }

        [TestMethod]
        public void RightClick_OnBackground_DoesNothing()
        {
            using var s = new Selection();
            s.Select(back: true, front: true);

            s.Click(90, 10, button: MouseButtons.Right);

            Assert.IsTrue(s.Back.Value);
            Assert.IsTrue(s.Front.Value);
        }

        [TestMethod]
        public void SelectionBounds_FollowTheSelectedItems_InProviderSpace()
        {
            using var s = new Selection();

            s.Click(15, 15);
            Assert.AreEqual(new RectangleF(10, 10, 40, 40), s.Bounds.Value.Value);

            s.Click(60, 60, Keys.Shift);
            Assert.AreEqual(new RectangleF(10, 10, 60, 60), s.Bounds.Value.Value);

            s.Click(90, 10);
            Assert.IsFalse(s.Bounds.Value.HasValue);
        }

        [TestMethod]
        public void Marquee_SelectsOnlyFullyContainedItems_AndShowsItsRectWhileDragging()
        {
            using var s = new Selection();
            s.Select(back: false, front: true);

            s.Scene.Down(5, 5);
            s.Scene.Move(20, 20);
            s.Scene.Move(55, 55);
            Assert.AreEqual(new RectangleF(5, 5, 50, 50), s.Marquee.Value.Value);
            Assert.IsTrue(s.Front.Value, "The marquee selects on release.");

            Assert.IsTrue(s.Scene.Up(55, 55));
            Assert.IsTrue(s.Back.Value);
            Assert.IsFalse(s.Front.Value);
            Assert.IsFalse(s.Marquee.Value.HasValue);
        }

        [TestMethod]
        public void ShiftMarquee_Adds_AndCtrlMarquee_Toggles()
        {
            using var s = new Selection();
            s.Select(back: false, front: true);
            s.Drag(5, 5, 55, 55, Keys.Shift);
            Assert.IsTrue(s.Back.Value);
            Assert.IsTrue(s.Front.Value);

            s.Select(back: true, front: false);
            s.Drag(5, 5, 75, 75, Keys.Control);
            Assert.IsFalse(s.Back.Value);
            Assert.IsTrue(s.Front.Value);
        }

        [TestMethod]
        public void SingleMode_HasNoMarquee()
        {
            using var s = new Selection();
            s.Provider.SetMode(SelectionMode.Single);

            s.Drag(5, 5, 55, 55);

            Assert.AreEqual(0, s.Marquee.Values.Count);
            Assert.IsFalse(s.Back.Value);
        }

        [TestMethod]
        public void Drag_InsideTheBounds_DoesNothingYet()
        {
            using var s = new Selection();
            s.Select(back: true, front: true);

            s.Drag(60, 20, 95, 95);

            Assert.AreEqual(0, s.Marquee.Values.Count);
            Assert.IsTrue(s.Back.Value);
            Assert.IsTrue(s.Front.Value);
        }

        [TestMethod]
        public void Escape_CancelsTheMarquee_AndSelectsNothing()
        {
            using var s = new Selection();

            s.Scene.Down(5, 5);
            s.Scene.Move(20, 20);
            s.Scene.Move(55, 55);
            Assert.IsTrue(s.Marquee.Value.HasValue);

            s.Scene.Escape();
            Assert.IsFalse(s.Marquee.Value.HasValue);

            s.Scene.Up(55, 55);
            Assert.IsFalse(s.Back.Value);
        }

        [TestMethod]
        public void DraggingAnItem_DoesNotStartTheMarquee()
        {
            using var s = new Selection();
            s.Select(back: false, front: true);

            s.Drag(15, 15, 15, 60);

            Assert.AreEqual(0, s.Marquee.Values.Count);
            Assert.IsTrue(s.Back.Value);
            Assert.IsFalse(s.Front.Value);
        }

        [TestMethod]
        public void OuterClear_DoesNotTouchTheItemsOfANestedProvider()
        {
            using var s = new Selection();
            using var child = new Recorder<bool>(true);
            var childItem = new Selectable(null!);
            childItem.SetIsSelected(child.Channel);
            s.Scene.Attach(s.Scene.Front, s.FrontItem, new SelectionProvider(null!));
            s.Scene.Attach(s.Scene.Child, childItem);
            s.Select(back: true, front: true);

            s.Click(90, 10);

            Assert.IsFalse(s.Back.Value);
            Assert.IsFalse(s.Front.Value);
            Assert.IsTrue(child.Value);
        }

        [TestMethod]
        public void NestedProvider_SelectsWithinItsOwnScope()
        {
            using var s = new Selection();
            using var child = new Recorder<bool>(false);
            var childItem = new Selectable(null!);
            childItem.SetIsSelected(child.Channel);
            s.Scene.Attach(s.Scene.Front, s.FrontItem, new SelectionProvider(null!));
            s.Scene.Attach(s.Scene.Child, childItem);
            s.Select(back: true, front: false);

            s.Scene.Down(35, 35);

            Assert.IsTrue(child.Value);
            Assert.IsTrue(s.Front.Value, "Front is pressed too, as an item of the outer provider.");
            Assert.IsFalse(s.Back.Value);
        }

        [TestMethod]
        public void WithoutProvider_AClickTogglesTheItemAlone()
        {
            using var s = new Selection(attach: false);
            s.Scene.Attach(s.Scene.Back, s.BackItem);

            s.Click(15, 15);
            Assert.IsTrue(s.Back.Value);

            s.Click(15, 15);
            Assert.IsFalse(s.Back.Value);
        }

        [TestMethod]
        public void DisabledProvider_TurnsSelectionOffInItsScope()
        {
            using var s = new Selection();
            s.Provider.SetEnabled(false);
            s.Select(back: false, front: true);

            s.Click(15, 15);
            s.Click(90, 10);

            Assert.IsFalse(s.Back.Value);
            Assert.IsTrue(s.Front.Value);
        }
    }
}
