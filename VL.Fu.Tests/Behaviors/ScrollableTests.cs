using System.Reactive;
using Stride.Core.Mathematics;
using VL.Fu.Behaviors;
using VL.Fu.Core.Interaction;
using VL.Lib.IO;

namespace VL.Fu.Tests.Behaviors
{
    /// <summary>
    /// <see cref="Scrollable"/> on Front (at (30, 30), 40 × 40) with Child as the content, made 40 × 200 to scroll vertically
    /// or 200 × 40 to scroll horizontally, so the range is 160 along that axis and 0 along the other.
    /// </summary>
    /// <remarks>
    /// The offset isn't applied to Child, so hits don't change during a test.
    /// </remarks>
    [TestClass]
    public sealed class ScrollableTests
    {
        [TestMethod]
        public void VerticalWheel_ScrollsByTheStep_DownAndUp()
        {
            using var offset = new Recorder<Vector2>(Vector2.Zero);
            using var scene = Tall(offset, out _);

            Assert.IsTrue(scene.Wheel(50, 50, -1));
            Assert.AreEqual(new Vector2(0, 48), offset.Value);

            Assert.IsTrue(scene.Wheel(50, 50, 0.5f));
            Assert.AreEqual(new Vector2(0, 24), offset.Value);
        }

        [TestMethod]
        public void HorizontalWheel_AndShiftWheel_ScrollHorizontally()
        {
            using var offset = new Recorder<Vector2>(Vector2.Zero);
            using var scene = Wide(offset, out _);

            Assert.IsTrue(scene.HorizontalWheel(50, 50, 1));
            Assert.AreEqual(new Vector2(48, 0), offset.Value);

            Assert.IsTrue(scene.Wheel(50, 50, -1, Keys.Shift));
            Assert.AreEqual(new Vector2(96, 0), offset.Value);
        }

        [TestMethod]
        public void Wheel_AtTheLimit_IsNotProcessed_AndTheOuterViewTakesIt()
        {
            using var scene = new BehaviorScene();
            using var outer = new Recorder<Vector2>(Vector2.Zero);
            using var inner = new Recorder<Vector2>(Vector2.Zero);
            scene.Attach(scene.Root, Connect(new Scrollable(null!), outer));
            scene.Resize(scene.Back, 40, 300);
            scene.Resize(scene.Child, 40, 200);
            scene.Attach(scene.Front, Connect(new Scrollable(null!), inner));

            scene.Wheel(50, 50, -10);
            Assert.AreEqual(new Vector2(0, 160), inner.Value);
            Assert.AreEqual(0, outer.Values.Count);

            Assert.IsTrue(scene.Wheel(50, 50, -1));
            Assert.AreEqual(new Vector2(0, 160), inner.Value);
            Assert.AreEqual(new Vector2(0, 48), outer.Value);
        }

        [TestMethod]
        public void Wheel_AlongAnAxisThatFits_IsNotProcessed()
        {
            using var offset = new Recorder<Vector2>(Vector2.Zero);
            using var scene = Wide(offset, out _);

            Assert.IsFalse(scene.Wheel(50, 50, -1));

            Assert.AreEqual(0, offset.Values.Count);
        }

        [TestMethod]
        public void Wheel_WithAStepOfZero_IsNotProcessed()
        {
            using var offset = new Recorder<Vector2>(Vector2.Zero);
            using var scene = Tall(offset, out var scrollable);
            scrollable.SetWheelStep(0);

            Assert.IsFalse(scene.Wheel(50, 50, -1));

            Assert.AreEqual(0, offset.Values.Count);
        }

        [TestMethod]
        [DataRow(-1f)]
        [DataRow(float.NaN)]
        [DataRow(float.PositiveInfinity)]
        public void InvalidWheelStep_IsIgnored(float step)
        {
            var scrollable = new Scrollable(null!);
            scrollable.SetWheelStep(10);

            scrollable.SetWheelStep(step);

            Assert.AreEqual(10f, scrollable.WheelStep);
        }

        [TestMethod]
        public void Wheel_OutsideTheRange_DoesntJump_AndMovesBackTowardsIt()
        {
            using var scene = new BehaviorScene();
            using var offset = new Recorder<Vector2>(new Vector2(0, 300));
            scene.Resize(scene.Child, 40, 200);
            scene.Attach(scene.Front, Connect(new Scrollable(null!), offset));

            Assert.IsFalse(scene.Wheel(50, 50, -1));
            Assert.AreEqual(new Vector2(0, 300), offset.Value);

            Assert.IsTrue(scene.Wheel(50, 50, 1));
            Assert.AreEqual(new Vector2(0, 252), offset.Value);
        }

        [TestMethod]
        public void Range_IsTheClipArea_InsideTheBorder()
        {
            using var scene = new BehaviorScene();
            using var offset = new Recorder<Vector2>(Vector2.Zero);
            scene.Resize(scene.Child, 40, 200);
            scene.Clip(scene.Front, 5);
            scene.Attach(scene.Front, Connect(new Scrollable(null!), offset));

            scene.Wheel(50, 50, -10);

            // Child starts at the padding box's corner (5, 5) and ends at 205; the clip ends at 35.
            Assert.AreEqual(new Vector2(0, 170), offset.Value);
        }

        [TestMethod]
        public void TouchDrag_Scrolls_ClampedAtBothEnds()
        {
            using var offset = new Recorder<Vector2>(Vector2.Zero);
            using var scene = Tall(offset, out _);

            scene.TouchDown(7, 50, 60);
            scene.TouchMove(7, 50, 40);
            Assert.AreEqual(new Vector2(0, 20), offset.Value);

            scene.TouchMove(7, 50, 80);
            Assert.AreEqual(Vector2.Zero, offset.Value);

            scene.TouchMove(7, 50, -200);
            Assert.AreEqual(new Vector2(0, 160), offset.Value);

            Assert.IsTrue(scene.TouchUp(7, 50, -200));
            Assert.IsNull(scene.Claims.ClaimOf(7));
        }

        [TestMethod]
        public void TouchDrag_WithinTheThreshold_WritesNothing()
        {
            using var offset = new Recorder<Vector2>(Vector2.Zero);
            using var scene = Tall(offset, out _);

            scene.TouchDown(7, 50, 60);
            scene.TouchMove(7, 50, 58);
            scene.TouchUp(7, 50, 58);

            Assert.AreEqual(0, offset.Values.Count);
        }

        [TestMethod]
        public void MouseDrag_DoesNothingByDefault_AndScrollsWhenItsButtonIsSet()
        {
            using var offset = new Recorder<Vector2>(Vector2.Zero);
            using var scene = Tall(offset, out var scrollable);

            scene.Down(50, 60);
            scene.Move(50, 40);
            scene.Up(50, 40);
            Assert.AreEqual(0, offset.Values.Count);
            Assert.IsNull(scene.Claims.ClaimOf(PointerIds.Mouse));

            scrollable.SetDrag(MouseButtons.Left);
            scene.Down(50, 60);
            scene.Move(50, 40);

            Assert.AreEqual(new Vector2(0, 20), offset.Value);
        }

        [TestMethod]
        public void TouchDrag_WithTouchesTurnedOff_DoesNothing()
        {
            using var offset = new Recorder<Vector2>(Vector2.Zero);
            using var scene = Tall(offset, out var scrollable);
            scrollable.SetDrag(MouseButtons.Left, touch: false);

            scene.TouchDown(7, 50, 60);
            scene.TouchMove(7, 50, 40);

            Assert.AreEqual(0, offset.Values.Count);
            Assert.IsNull(scene.Claims.ClaimOf(7));
        }

        [TestMethod]
        public void Drag_UnderAScaledViewport_IsInItsUnits()
        {
            using var offset = new Recorder<Vector2>(Vector2.Zero);
            using var scene = Tall(offset, out _);

            // Front is scaled by half about its center (50, 50), so 10 pixels are 20 of its units.
            scene.Front.SetTransformation(Matrix.Scaling(0.5f, 0.5f, 1));

            scene.TouchDown(7, 50, 55);
            scene.TouchMove(7, 50, 45);

            Assert.AreEqual(new Vector2(0, 20), offset.Value);
        }

        [TestMethod]
        public void Drag_AlongAnAxisThatFits_IsLeftToTheOuterView()
        {
            using var scene = new BehaviorScene();
            using var outer = new Recorder<Vector2>(Vector2.Zero);
            using var inner = new Recorder<Vector2>(Vector2.Zero);
            scene.Resize(scene.Child, 40, 200);
            var item = scene.AddChild(scene.Child, 0, 0, 200, 10);
            scene.Attach(scene.Front, Connect(new Scrollable(null!), outer));
            var innerScrollable = Connect(new Scrollable(null!), inner);
            scene.Attach(scene.Child, innerScrollable);

            // Vertically only the outer view scrolls.
            scene.TouchDown(7, 35, 35);
            scene.TouchMove(7, 35, 15);
            scene.TouchUp(7, 35, 15);
            Assert.AreEqual(new Vector2(0, 20), outer.Value);
            Assert.AreEqual(0, inner.Values.Count);

            // Horizontally only the inner one does.
            scene.TouchDown(8, 35, 35);
            scene.TouchMove(8, 15, 35);
            Assert.AreEqual(new Vector2(20, 0), inner.Value);
            Assert.IsTrue(scene.Claims.ClaimOf(8)!.HasMember(innerScrollable));
            Assert.AreEqual(new Vector2(0, 20), outer.Value);
        }

        [TestMethod]
        public void PressedItem_DoesntClick_WhenTheScrollTakesThePointer()
        {
            using var offset = new Recorder<Vector2>(Vector2.Zero);
            using var scene = Tall(offset, out _);
            using var onClick = new Recorder<Unit>(Unit.Default);
            var clickable = new Clickable(null!);
            clickable.SetOnClick(onClick.Channel);
            scene.Attach(scene.Child, clickable);

            scene.TouchDown(7, 50, 60);
            scene.TouchMove(7, 50, 40);
            scene.TouchUp(7, 50, 40);

            Assert.AreEqual(new Vector2(0, 20), offset.Value);
            Assert.AreEqual(0, onClick.Values.Count);
        }

        [TestMethod]
        public void ItemDrag_WinsOverTheScroll()
        {
            using var offset = new Recorder<Vector2>(Vector2.Zero);
            using var scene = Tall(offset, out _);
            using var dragged = new Recorder<Vector2>(Vector2.Zero);
            var draggable = new Draggable(null!);
            draggable.SetOffset(dragged.Channel);
            scene.Attach(scene.Child, draggable);

            scene.TouchDown(7, 50, 60);
            scene.TouchMove(7, 50, 40);

            Assert.AreEqual(new Vector2(0, -20), dragged.Value);
            Assert.AreEqual(0, offset.Values.Count);
        }

        [TestMethod]
        public void Wheel_DuringADrag_IsProcessed_AndChangesNothing()
        {
            using var offset = new Recorder<Vector2>(Vector2.Zero);
            using var scene = Tall(offset, out var scrollable);
            // A touch would mute the mouse wheel in the dispatcher, so the drag is a mouse one.
            scrollable.SetDrag(MouseButtons.Left);
            scene.Down(50, 60);
            scene.Move(50, 40);

            Assert.IsTrue(scene.Wheel(50, 40, -1));

            Assert.AreEqual(new Vector2(0, 20), offset.Value);
        }

        [TestMethod]
        public void Escape_ReturnsToTheOffsetAtTheStart()
        {
            using var offset = new Recorder<Vector2>(new Vector2(0, 50));
            using var scene = Tall(offset, out _);
            scene.TouchDown(7, 50, 60);
            scene.TouchMove(7, 50, 40);
            Assert.AreEqual(new Vector2(0, 70), offset.Value);

            scene.Escape();

            Assert.AreEqual(new Vector2(0, 50), offset.Value);
            scene.TouchMove(7, 50, 20);
            Assert.AreEqual(new Vector2(0, 50), offset.Value);
        }

        [TestMethod]
        public void Disabling_DuringADrag_ReturnsToTheOffsetAtTheStart_AndLetsGoOfThePointer()
        {
            using var offset = new Recorder<Vector2>(Vector2.Zero);
            using var scene = Tall(offset, out var scrollable);
            scene.TouchDown(7, 50, 60);
            scene.TouchMove(7, 50, 40);

            scrollable.SetEnabled(false);

            Assert.AreEqual(Vector2.Zero, offset.Value);
            scene.TouchMove(7, 50, 20);
            Assert.IsNull(scene.Claims.ClaimOf(7));
            Assert.AreEqual(Vector2.Zero, offset.Value);
        }

        [TestMethod]
        public void SwappingTheChannel_DuringADrag_RestoresTheOldOne_AndLeavesTheNewOneUntouched()
        {
            using var offset = new Recorder<Vector2>(Vector2.Zero);
            using var scene = Tall(offset, out var scrollable);
            using var next = new Recorder<Vector2>(new Vector2(0, 100));
            scene.TouchDown(7, 50, 60);
            scene.TouchMove(7, 50, 40);

            scrollable.SetOffset(next.Channel);

            Assert.AreEqual(Vector2.Zero, offset.Value);
            scene.TouchMove(7, 50, 20);
            Assert.IsNull(scene.Claims.ClaimOf(7));
            Assert.AreEqual(0, next.Values.Count);
        }

        private static BehaviorScene Tall(Recorder<Vector2> offset, out Scrollable scrollable) => Scene(40, 200, offset, out scrollable);

        private static BehaviorScene Wide(Recorder<Vector2> offset, out Scrollable scrollable) => Scene(200, 40, offset, out scrollable);

        /// <summary>
        /// A scene whose Front scrolls Child, resized to <paramref name="width"/> × <paramref name="height"/>, into <paramref name="offset"/>.
        /// </summary>
        private static BehaviorScene Scene(float width, float height, Recorder<Vector2> offset, out Scrollable scrollable)
        {
            var scene = new BehaviorScene();
            scene.Resize(scene.Child, width, height);
            scrollable = Connect(new Scrollable(null!), offset);
            scene.Attach(scene.Front, scrollable);
            return scene;
        }
        private static Scrollable Connect(Scrollable scrollable, Recorder<Vector2> offset)
        {
            scrollable.SetOffset(offset.Channel);
            return scrollable;
        }
    }
}
