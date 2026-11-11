using Stride.Core.Mathematics;
using VL.Fu.Behaviors;
using VL.Fu.Core.Interaction;
using VL.Lib.IO.Notifications;

namespace VL.Fu.Tests.Behaviors
{
    /// <summary>
    /// <see cref="Pinchable"/>: a two-touch pinch, and an ancestor's pinch taking the pointer from a child's drag.
    /// </summary>
    [TestClass]
    public sealed class PinchableTests
    {
        private sealed class Outputs : IDisposable
        {
            public Recorder<float> Scale { get; } = new(1f);
            public Recorder<bool> IsPinching { get; } = new(false);

            public Pinchable Connect(Pinchable pinchable)
            {
                pinchable.SetScale(Scale.Channel);
                pinchable.SetIsPinching(IsPinching.Channel);
                return pinchable;
            }

            public void Dispose()
            {
                Scale.Dispose();
                IsPinching.Dispose();
            }
        }

        [TestMethod]
        public void Pinch_StartsPastTheThreshold_AndEndsWhenATouchLifts()
        {
            using var scene = new BehaviorScene();
            using var outputs = new Outputs();
            scene.Attach(scene.Front, outputs.Connect(new Pinchable(null!)));

            scene.TouchDown(7, 40, 40);
            scene.TouchDown(8, 60, 60);
            scene.TouchMove(8, 66, 66);

            CollectionAssert.AreEqual(new[] { true }, outputs.IsPinching.Values);
            Assert.IsTrue(outputs.Scale.Value > 1f);

            scene.TouchUp(8, 66, 66);

            CollectionAssert.AreEqual(new[] { true, false }, outputs.IsPinching.Values);
            Assert.IsNull(scene.Claims.ClaimOf(7));
            Assert.IsNull(scene.Claims.ClaimOf(8));
        }

        [TestMethod]
        public void AncestorPinch_TakesTheTouchFromAChildDrag()
        {
            using var scene = new BehaviorScene();
            using var outputs = new Outputs();
            using var offset = new Recorder<Vector2>(Vector2.Zero);
            using var isDragging = new Recorder<bool>(false);
            using var onDragEnd = new Recorder<System.Reactive.Unit>(System.Reactive.Unit.Default);
            var draggable = new Draggable(null!);
            draggable.SetOffset(offset.Channel);
            draggable.SetIsDragging(isDragging.Channel);
            draggable.SetOnDragEnd(onDragEnd.Channel);
            scene.Attach(scene.Child, draggable);
            scene.Attach(scene.Front, outputs.Connect(new Pinchable(null!)));

            scene.TouchDown(7, 35, 35);
            scene.TouchMove(7, 45, 35);
            CollectionAssert.AreEqual(new[] { true }, isDragging.Values);

            scene.TouchDown(8, 60, 60);
            scene.TouchMove(8, 66, 66);

            CollectionAssert.AreEqual(new[] { true }, outputs.IsPinching.Values);
            CollectionAssert.AreEqual(new[] { true, false }, isDragging.Values);
            Assert.AreEqual(Vector2.Zero, offset.Value);
            Assert.AreEqual(0, onDragEnd.Values.Count);
        }

        [TestMethod]
        public void ClaimedUp_NobodyProcessed_StillResetsAPendingPinch()
        {
            using var scene = new BehaviorScene();
            using var outputs = new Outputs();
            var claimer = new Probe((self, notification) =>
            {
                if (notification is TouchNotification { Kind: TouchNotificationKind.TouchDown, Id: 7 })
                    notification.RequestPointerCapture(self, GestureKind.Drag);
                return false;
            });
            scene.Attach(scene.Child, claimer);
            scene.Attach(scene.Front, outputs.Connect(new Pinchable(null!)));

            scene.TouchDown(7, 35, 35);
            scene.TouchUp(7, 35, 35);

            scene.TouchDown(8, 40, 40);
            scene.TouchDown(9, 60, 60);
            scene.TouchMove(9, 66, 66);

            Assert.IsNotNull(scene.Claims.ClaimOf(9));
            CollectionAssert.AreEqual(new[] { true }, outputs.IsPinching.Values);
        }
    }
}
