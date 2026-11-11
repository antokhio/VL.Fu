using System.Reactive;
using Stride.Core.Mathematics;
using VL.Fu.Behaviors;
using VL.Fu.Core.Interaction;

namespace VL.Fu.Tests.Behaviors
{
    /// <summary>
    /// <see cref="Draggable"/>: the drag's lifecycle, and how it ends when it is disabled, rewired, removed or taken over.
    /// </summary>
    [TestClass]
    public sealed class DraggableTests
    {
        private sealed class Outputs : IDisposable
        {
            public Recorder<Vector2> Offset { get; } = new(Vector2.Zero);
            public Recorder<bool> IsDragging { get; } = new(false);
            public Recorder<Unit> OnDragStart { get; } = new(Unit.Default);
            public Recorder<Unit> OnDragEnd { get; } = new(Unit.Default);

            public Draggable Connect(Draggable draggable)
            {
                draggable.SetOffset(Offset.Channel);
                draggable.SetIsDragging(IsDragging.Channel);
                draggable.SetOnDragStart(OnDragStart.Channel);
                draggable.SetOnDragEnd(OnDragEnd.Channel);
                return draggable;
            }

            public void Dispose()
            {
                Offset.Dispose();
                IsDragging.Dispose();
                OnDragStart.Dispose();
                OnDragEnd.Dispose();
            }
        }

        [TestMethod]
        public void Drag_StartsPastTheThreshold_FollowsThePointer_AndEndsOnTheUp()
        {
            using var scene = new BehaviorScene();
            using var outputs = new Outputs();
            scene.Attach(scene.Child, outputs.Connect(new Draggable(null!)));

            scene.Down(35, 35);
            scene.Move(37, 35);
            Assert.AreEqual(0, outputs.IsDragging.Values.Count);

            scene.Move(45, 35);
            Assert.AreEqual(new Vector2(10, 0), outputs.Offset.Value);
            CollectionAssert.AreEqual(new[] { true }, outputs.IsDragging.Values);
            Assert.AreEqual(1, outputs.OnDragStart.Values.Count);

            scene.Move(50, 40);
            Assert.AreEqual(new Vector2(15, 5), outputs.Offset.Value);

            Assert.IsTrue(scene.Up(50, 40));
            CollectionAssert.AreEqual(new[] { true, false }, outputs.IsDragging.Values);
            Assert.AreEqual(1, outputs.OnDragEnd.Values.Count);
            Assert.AreEqual(new Vector2(15, 5), outputs.Offset.Value);
            Assert.IsNull(scene.Claims.ClaimOf(PointerIds.Mouse));
        }

        [TestMethod]
        public void Disabling_DuringADrag_WritesTheIdleFacts_AndReleasesTheClaimOnTheNextNotification()
        {
            using var scene = new BehaviorScene();
            using var outputs = new Outputs();
            var draggable = outputs.Connect(new Draggable(null!));
            scene.Attach(scene.Child, draggable);
            scene.Down(35, 35);
            scene.Move(45, 35);

            draggable.SetEnabled(false);

            Assert.AreEqual(Vector2.Zero, outputs.Offset.Value);
            CollectionAssert.AreEqual(new[] { true, false }, outputs.IsDragging.Values);
            Assert.AreEqual(0, outputs.OnDragEnd.Values.Count);
            Assert.IsNotNull(scene.Claims.ClaimOf(PointerIds.Mouse));

            scene.Move(50, 35);
            Assert.IsNull(scene.Claims.ClaimOf(PointerIds.Mouse));
        }

        [TestMethod]
        public void SwappingAChannel_DuringADrag_EndsItOnTheOldChannels_AndLeavesTheNewOneUntouched()
        {
            using var scene = new BehaviorScene();
            using var outputs = new Outputs();
            using var next = new Recorder<Vector2>(new Vector2(-1, -1));
            var draggable = outputs.Connect(new Draggable(null!));
            scene.Attach(scene.Child, draggable);
            scene.Down(35, 35);
            scene.Move(45, 35);

            draggable.SetOffset(next.Channel);

            Assert.AreEqual(Vector2.Zero, outputs.Offset.Value);
            CollectionAssert.AreEqual(new[] { true, false }, outputs.IsDragging.Values);
            Assert.AreEqual(0, next.Values.Count);
        }

        [TestMethod]
        public void Removing_TheBehavior_DuringADrag_WritesTheIdleFacts()
        {
            using var scene = new BehaviorScene();
            using var outputs = new Outputs();
            scene.Attach(scene.Child, outputs.Connect(new Draggable(null!)));
            scene.Down(35, 35);
            scene.Move(45, 35);

            scene.Attach(scene.Child);
            scene.Root.Dispatcher.Sweep();

            Assert.AreEqual(Vector2.Zero, outputs.Offset.Value);
            CollectionAssert.AreEqual(new[] { true, false }, outputs.IsDragging.Values);
            Assert.AreEqual(0, outputs.OnDragEnd.Values.Count);
        }

        [TestMethod]
        public void InvalidThreshold_IsIgnored()
        {
            var draggable = new Draggable(null!);

            draggable.SetThreshold(float.PositiveInfinity);

            Assert.AreEqual(4f, draggable.Threshold);
        }
    }
}
