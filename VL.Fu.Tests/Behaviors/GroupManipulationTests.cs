using Stride.Core.Mathematics;
using VL.Core;
using VL.Fu.Behaviors;
using VL.Fu.Core.Interaction;
using VL.Lib.IO;
using VL.Skia;
using YogaSharp;

namespace VL.Fu.Tests.Behaviors
{
    /// <summary>
    /// Moving and transforming the selection of a <see cref="SelectionProvider"/> together: who drives, who takes part,
    /// what each member writes in its own parent space, and how the group is cancelled.
    /// </summary>
    /// <remarks>
    /// The provider is on Root, Back (10–50) and Front (30–70) are its items, each with a <see cref="Movable"/> by default.
    /// Their positions start at their layout positions, (10, 10) and (30, 30). Child is an item too when asked for, inside Front.
    /// (15, 15) presses Back, (65, 65) presses Front, (60, 20) is background inside the bounds of Back and Front together,
    /// and (90, 90) is background outside of them.
    /// </remarks>
    [TestClass]
    public sealed unsafe class GroupManipulationTests
    {
        private sealed class Group : IDisposable
        {
            public Group(IBehavior? back = null, IBehavior? front = null, IBehavior? child = null, bool withChild = false)
            {
                Provider.SetSelectionBounds(Bounds.Channel);
                Provider.SetMarqueeRect(Marquee.Channel);
                BackItem.SetIsSelected(BackSelected.Channel);
                FrontItem.SetIsSelected(FrontSelected.Channel);
                ChildItem.SetIsSelected(ChildSelected.Channel);
                BackMove.SetPosition(BackPosition.Channel);
                FrontMove.SetPosition(FrontPosition.Channel);
                ChildMove.SetPosition(ChildPosition.Channel);

                Scene.Attach(Scene.Root, Provider);
                Scene.Attach(Scene.Back, BackItem, back ?? BackMove);
                Scene.Attach(Scene.Front, FrontItem, front ?? FrontMove);
                if (withChild || child is not null)
                    Scene.Attach(Scene.Child, ChildItem, child ?? ChildMove);
            }

            public BehaviorScene Scene { get; } = new();
            public SelectionProvider Provider { get; } = new(null!);
            public Selectable BackItem { get; } = new(null!);
            public Selectable FrontItem { get; } = new(null!);
            public Selectable ChildItem { get; } = new(null!);
            public Movable BackMove { get; } = new(null!);
            public Movable FrontMove { get; } = new(null!);
            public Movable ChildMove { get; } = new(null!);
            public Recorder<bool> BackSelected { get; } = new(false);
            public Recorder<bool> FrontSelected { get; } = new(false);
            public Recorder<bool> ChildSelected { get; } = new(false);
            public Recorder<Vector2> BackPosition { get; } = new(new Vector2(10, 10));
            public Recorder<Vector2> FrontPosition { get; } = new(new Vector2(30, 30));
            public Recorder<Vector2> ChildPosition { get; } = new(Vector2.Zero);
            public Recorder<Optional<RectangleF>> Bounds { get; } = new(default);
            public Recorder<Optional<RectangleF>> Marquee { get; } = new(default);

            public void Select(bool back, bool front, bool child = false)
            {
                BackSelected.Channel.Value = back;
                FrontSelected.Channel.Value = front;
                ChildSelected.Channel.Value = child;
            }

            public void Dispose()
            {
                Scene.Dispose();
                foreach (var recorder in new IDisposable[]
                {
                    BackSelected, FrontSelected, ChildSelected, BackPosition, FrontPosition, ChildPosition, Bounds, Marquee,
                })
                    recorder.Dispose();
            }
        }

        [TestMethod]
        public void DraggingASelectedItem_MovesEverySelectedItem()
        {
            using var g = new Group();
            g.Select(back: true, front: true);

            g.Scene.Down(15, 15);
            g.Scene.Move(25, 20);
            Assert.AreEqual(new Vector2(20, 15), g.BackPosition.Value);
            Assert.AreEqual(new Vector2(40, 35), g.FrontPosition.Value);

            g.Scene.Move(35, 25);
            Assert.IsTrue(g.Scene.Up(35, 25));

            Assert.AreEqual(new Vector2(30, 20), g.BackPosition.Value);
            Assert.AreEqual(new Vector2(50, 40), g.FrontPosition.Value);
            Assert.IsTrue(g.BackSelected.Value);
            Assert.IsTrue(g.FrontSelected.Value);
            Assert.IsNull(g.Scene.Claims.ClaimOf(PointerIds.Mouse));
        }

        [TestMethod]
        public void DraggingAnUnselectedItem_SelectsIt_AndMovesItAlone()
        {
            using var g = new Group();
            g.Select(back: false, front: true);

            g.Scene.Down(15, 15);
            g.Scene.Move(25, 15);
            g.Scene.Up(25, 15);

            Assert.AreEqual(new Vector2(20, 10), g.BackPosition.Value);
            Assert.AreEqual(0, g.FrontPosition.Values.Count);
            Assert.IsTrue(g.BackSelected.Value);
            Assert.IsFalse(g.FrontSelected.Value);
        }

        [TestMethod]
        public void ShiftDraggingAnUnselectedItem_MovesTheExtendedGroup()
        {
            using var g = new Group();
            g.Select(back: false, front: true);

            g.Scene.Down(15, 15, MouseButtons.Left, Keys.Shift);
            g.Scene.Move(25, 15, Keys.Shift);
            g.Scene.Up(25, 15, MouseButtons.Left, Keys.Shift);

            Assert.AreEqual(new Vector2(20, 10), g.BackPosition.Value);
            Assert.AreEqual(new Vector2(40, 30), g.FrontPosition.Value);
        }

        [TestMethod]
        public void TheMembersAreFixedAtTheStart()
        {
            using var g = new Group();
            g.Select(back: true, front: false);
            g.Scene.Down(15, 15);
            g.Scene.Move(25, 15);

            g.Select(back: false, front: true);
            g.Scene.Move(35, 15);
            g.Scene.Up(35, 15);

            // Back was deselected but keeps moving until the end; Front was selected too late.
            Assert.AreEqual(new Vector2(30, 10), g.BackPosition.Value);
            Assert.AreEqual(0, g.FrontPosition.Values.Count);
        }

        [TestMethod]
        public void Escape_RestoresEveryMember()
        {
            using var g = new Group();
            g.Select(back: true, front: true);
            g.Scene.Down(15, 15);
            g.Scene.Move(25, 15);

            g.Scene.Escape();

            Assert.AreEqual(new Vector2(10, 10), g.BackPosition.Value);
            Assert.AreEqual(new Vector2(30, 30), g.FrontPosition.Value);
            g.Scene.Move(35, 15);
            Assert.AreEqual(new Vector2(10, 10), g.BackPosition.Value);
        }

        [TestMethod]
        public void AMemberHiddenDuringTheDrag_IsStillRestored()
        {
            using var g = new Group();
            g.Select(back: true, front: true);
            g.Scene.Down(15, 15);
            g.Scene.Move(25, 15);

            g.Scene.Front.Native->SetDisplay(YGDisplay.None);
            g.Scene.Root.CalculateLayout();
            g.Scene.Escape();

            Assert.AreEqual(new Vector2(30, 30), g.FrontPosition.Value);
        }

        [TestMethod]
        public void EachMember_GetsTheDeltaInItsOwnParentSpace()
        {
            using var g = new Group(withChild: true);
            g.Select(back: true, front: false, child: true);

            // Front is scaled by half about its center, so its space has twice the units of the window.
            g.Scene.Front.SetTransformation(Matrix.Scaling(0.5f, 0.5f, 1));

            g.Scene.Down(15, 15);
            g.Scene.Move(35, 15);

            Assert.AreEqual(new Vector2(30, 10), g.BackPosition.Value);
            Assert.AreEqual(new Vector2(40, 0), g.ChildPosition.Value);
            Assert.AreEqual(0, g.FrontPosition.Values.Count);
        }

        [TestMethod]
        public void DraggingInsideTheBounds_MovesTheGroup_AndOutsideDrawsAMarquee()
        {
            using var g = new Group();
            g.Select(back: true, front: true);

            g.Scene.Down(60, 20);
            g.Scene.Move(70, 30);
            g.Scene.Up(70, 30);

            Assert.AreEqual(new Vector2(20, 20), g.BackPosition.Value);
            Assert.AreEqual(new Vector2(40, 40), g.FrontPosition.Value);
            Assert.IsFalse(g.Marquee.Values.Any(m => m.HasValue));

            g.Scene.Down(90, 90);
            g.Scene.Move(80, 80);
            Assert.IsTrue(g.Marquee.Value.HasValue);
            Assert.AreEqual(new Vector2(20, 20), g.BackPosition.Value);
        }

        [TestMethod]
        public void PinchingASelectedTransformable_TransformsTheGroupAboutTheTouches()
        {
            using var back = new Recorder<Matrix>(Matrix.Identity);
            using var front = new Recorder<Matrix>(Matrix.Identity);
            var backTransformable = new Transformable(null!);
            var frontTransformable = new Transformable(null!);
            backTransformable.SetTransformation(back.Channel);
            frontTransformable.SetTransformation(front.Channel);
            using var g = new Group(backTransformable, frontTransformable, withChild: true);
            g.Select(back: true, front: true, child: true);

            g.Scene.TouchDown(7, 45, 45);
            g.Scene.TouchDown(8, 65, 65);
            g.Scene.TouchMove(8, 75, 75);

            // Scaled by 1.5 about (55, 55), which moved to (60, 60): a window point q goes to (60, 60) + 1.5 (q − (55, 55)).
            static Vector2 Expected(Vector2 q) => new Vector2(60, 60) + 1.5f * (q - new Vector2(55, 55));

            TransformableTests.AssertClose(
                new Vector2(75, 75),
                TransformableTests.Shown(front.Value, Vector2.Zero, new Vector2(35, 35))
            );
            TransformableTests.AssertClose(
                Expected(new Vector2(10, 10)),
                TransformableTests.Shown(new Vector2(10, 10), 40, back.Value, Vector2.Zero, Vector2.Zero)
            );

            // Child's position is in Front's space, which is the window moved by (30, 30).
            TransformableTests.AssertClose(Expected(new Vector2(30, 30)) - new Vector2(30, 30), g.ChildPosition.Value);
        }

        [TestMethod]
        public void DisablingTheProvider_CancelsTheGroupWithTheNextMove()
        {
            using var g = new Group();
            g.Select(back: true, front: true);
            g.Scene.Down(15, 15);
            g.Scene.Move(25, 15);

            g.Provider.SetEnabled(false);
            g.Scene.Move(35, 15);

            Assert.AreEqual(new Vector2(10, 10), g.BackPosition.Value);
            Assert.AreEqual(new Vector2(30, 30), g.FrontPosition.Value);
            Assert.IsNull(g.Scene.Claims.ClaimOf(PointerIds.Mouse));

            g.Scene.Move(45, 15);
            Assert.AreEqual(new Vector2(10, 10), g.BackPosition.Value);
        }

        [TestMethod]
        public void ASecondDrag_DuringAGroupDrag_MovesNothing()
        {
            using var g = new Group();
            g.Select(back: true, front: true);
            g.Scene.TouchDown(7, 15, 15);
            g.Scene.TouchMove(7, 25, 15);
            Assert.AreEqual(new Vector2(40, 30), g.FrontPosition.Value);

            g.Scene.TouchDown(8, 65, 65);
            g.Scene.TouchMove(8, 65, 75);
            g.Scene.TouchMove(8, 65, 85);

            Assert.AreEqual(new Vector2(20, 10), g.BackPosition.Value);
            Assert.AreEqual(new Vector2(40, 30), g.FrontPosition.Value);

            g.Scene.TouchMove(7, 35, 15);
            Assert.AreEqual(new Vector2(30, 10), g.BackPosition.Value);
            Assert.AreEqual(new Vector2(50, 30), g.FrontPosition.Value);
        }

        [TestMethod]
        public void ClickingASelectedItem_WithoutADrag_StillCollapsesTheSelection()
        {
            using var g = new Group();
            g.Select(back: true, front: true);

            g.Scene.Down(15, 15);
            g.Scene.Up(15, 15);

            Assert.IsTrue(g.BackSelected.Value);
            Assert.IsFalse(g.FrontSelected.Value);
            Assert.AreEqual(0, g.BackPosition.Values.Count);
            Assert.AreEqual(0, g.FrontPosition.Values.Count);
        }
    }
}
