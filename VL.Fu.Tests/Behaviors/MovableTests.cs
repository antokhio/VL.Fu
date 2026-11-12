using Stride.Core.Mathematics;
using VL.Fu.Behaviors;
using VL.Fu.Core.Interaction;

namespace VL.Fu.Tests.Behaviors
{
    /// <summary>
    /// <see cref="Movable"/> on its own: the position follows the pointer from the press, in the parent's units,
    /// and returns to the press when the drag is cancelled.
    /// </summary>
    [TestClass]
    public sealed class MovableTests
    {
        [TestMethod]
        public void Drag_WritesThePositionAtThePressPlusTheOffset()
        {
            using var scene = new BehaviorScene();
            using var position = new Recorder<Vector2>(new Vector2(5, 5));
            var movable = new Movable(null!);
            movable.SetPosition(position.Channel);
            scene.Attach(scene.Child, movable);

            scene.Down(35, 35);
            scene.Move(45, 35);
            Assert.AreEqual(new Vector2(15, 5), position.Value);

            scene.Move(50, 40);
            Assert.IsTrue(scene.Up(50, 40));

            Assert.AreEqual(new Vector2(20, 10), position.Value);
            Assert.IsNull(scene.Claims.ClaimOf(PointerIds.Mouse));
        }

        [TestMethod]
        public void Move_WithinTheThreshold_WritesNothing()
        {
            using var scene = new BehaviorScene();
            using var position = new Recorder<Vector2>(Vector2.Zero);
            var movable = new Movable(null!);
            movable.SetPosition(position.Channel);
            scene.Attach(scene.Child, movable);

            scene.Down(35, 35);
            scene.Move(37, 35);
            scene.Up(37, 35);

            Assert.AreEqual(0, position.Values.Count);
        }

        [TestMethod]
        public void Escape_ReturnsToThePositionAtThePress()
        {
            using var scene = new BehaviorScene();
            using var position = new Recorder<Vector2>(new Vector2(5, 5));
            var movable = new Movable(null!);
            movable.SetPosition(position.Channel);
            scene.Attach(scene.Child, movable);
            scene.Down(35, 35);
            scene.Move(45, 35);

            scene.Escape();

            Assert.AreEqual(new Vector2(5, 5), position.Value);
            scene.Move(55, 35);
            Assert.AreEqual(new Vector2(5, 5), position.Value);
        }

        [TestMethod]
        public void SwappingTheChannel_DuringADrag_RestoresTheOldOne_AndLeavesTheNewOneUntouched()
        {
            using var scene = new BehaviorScene();
            using var position = new Recorder<Vector2>(new Vector2(5, 5));
            using var next = new Recorder<Vector2>(new Vector2(-1, -1));
            var movable = new Movable(null!);
            movable.SetPosition(position.Channel);
            scene.Attach(scene.Child, movable);
            scene.Down(35, 35);
            scene.Move(45, 35);

            movable.SetPosition(next.Channel);

            Assert.AreEqual(new Vector2(5, 5), position.Value);
            Assert.AreEqual(0, next.Values.Count);
            scene.Move(55, 35);
            Assert.IsNull(scene.Claims.ClaimOf(PointerIds.Mouse));
            Assert.AreEqual(0, next.Values.Count);
        }

        [TestMethod]
        public void UnderAScaledParent_TheOffsetIsInTheParentsUnits()
        {
            using var scene = new BehaviorScene();
            using var position = new Recorder<Vector2>(Vector2.Zero);
            var movable = new Movable(null!);
            movable.SetPosition(position.Channel);
            scene.Attach(scene.Child, movable);

            // Front is scaled by half about its center (50, 50), so Child covers (40, 40) to (45, 45) on screen.
            scene.Front.SetTransformation(Matrix.Scaling(0.5f, 0.5f, 1));

            scene.Down(42, 42);
            scene.Move(52, 42);

            Assert.AreEqual(new Vector2(20, 0), position.Value);
        }
    }
}
