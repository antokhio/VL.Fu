using System.Reactive;
using VL.Fu.Behaviors;
using VL.Lib.IO;

namespace VL.Fu.Tests.Behaviors
{
    /// <summary>
    /// <see cref="Clickable"/>: clicks, the pressed state, and what happens to both when another gesture takes the pointer (O2).
    /// </summary>
    [TestClass]
    public sealed class ClickableTests
    {
        private sealed class Outputs : IDisposable
        {
            public Recorder<Unit> OnClick { get; } = new(Unit.Default);
            public Recorder<bool> IsPressed { get; } = new(false);

            public Clickable Connect(Clickable clickable)
            {
                clickable.SetOnClick(OnClick.Channel);
                clickable.SetIsPressed(IsPressed.Channel);
                return clickable;
            }

            public void Dispose()
            {
                OnClick.Dispose();
                IsPressed.Dispose();
            }
        }

        [TestMethod]
        public void Click_PressesAtOnce_AndFiresOnceOnTheProcessedUp()
        {
            using var scene = new BehaviorScene();
            using var outputs = new Outputs();
            scene.Attach(scene.Child, outputs.Connect(new Clickable(null!)));

            scene.Down(35, 35);
            CollectionAssert.AreEqual(new[] { true }, outputs.IsPressed.Values);
            Assert.AreEqual(0, outputs.OnClick.Values.Count);

            Assert.IsTrue(scene.Up(36, 36));
            CollectionAssert.AreEqual(new[] { true, false }, outputs.IsPressed.Values);
            Assert.AreEqual(1, outputs.OnClick.Values.Count);
        }

        [TestMethod]
        public void Slip_KeepsThePressUntilTheUp_AndDoesNotClick()
        {
            using var scene = new BehaviorScene();
            using var outputs = new Outputs();
            scene.Attach(scene.Child, outputs.Connect(new Clickable(null!)));

            scene.Down(35, 35);
            scene.Move(55, 35);
            CollectionAssert.AreEqual(new[] { true }, outputs.IsPressed.Values);

            Assert.IsFalse(scene.Up(35, 35));
            CollectionAssert.AreEqual(new[] { true, false }, outputs.IsPressed.Values);
            Assert.AreEqual(0, outputs.OnClick.Values.Count);
        }

        [TestMethod]
        public void OtherButton_DoesNotPress()
        {
            using var scene = new BehaviorScene();
            using var outputs = new Outputs();
            scene.Attach(scene.Child, outputs.Connect(new Clickable(null!)));

            scene.Down(35, 35, MouseButtons.Right);
            scene.Up(35, 35, MouseButtons.Right);

            Assert.AreEqual(0, outputs.IsPressed.Values.Count);
            Assert.AreEqual(0, outputs.OnClick.Values.Count);
        }

        [TestMethod]
        public void Disabling_DuringAPress_ReleasesIt_AndTheUpDoesNotClick()
        {
            using var scene = new BehaviorScene();
            using var outputs = new Outputs();
            var clickable = outputs.Connect(new Clickable(null!));
            scene.Attach(scene.Child, clickable);
            scene.Down(35, 35);

            clickable.SetEnabled(false);
            clickable.SetEnabled(true);
            scene.Up(35, 35);

            CollectionAssert.AreEqual(new[] { true, false }, outputs.IsPressed.Values);
            Assert.AreEqual(0, outputs.OnClick.Values.Count);
        }

        [TestMethod]
        public void InvalidThreshold_IsIgnored()
        {
            var clickable = new Clickable(null!);

            clickable.SetThreshold(float.NaN);
            clickable.SetThreshold(-1f);

            Assert.AreEqual(6f, clickable.Threshold);
        }

        [TestMethod]
        public void NestedClickables_OnlyTheInnerOneClicks_AndTheOuterOneIsReleased()
        {
            using var scene = new BehaviorScene();
            using var inner = new Outputs();
            using var outer = new Outputs();
            scene.Attach(scene.Child, inner.Connect(new Clickable(null!)));
            scene.Attach(scene.Front, outer.Connect(new Clickable(null!)));

            scene.Down(35, 35);
            scene.Up(35, 35);

            Assert.AreEqual(1, inner.OnClick.Values.Count);
            Assert.AreEqual(0, outer.OnClick.Values.Count);
            CollectionAssert.AreEqual(new[] { true, false }, outer.IsPressed.Values);
        }

        [TestMethod]
        public void DragOfTheSameNode_SuppressesTheClick_ButKeepsThePressUntilTheUp()
        {
            using var scene = new BehaviorScene();
            using var outputs = new Outputs();
            var clickable = outputs.Connect(new Clickable(null!));
            clickable.SetThreshold(50f);
            scene.Attach(scene.Child, clickable, new Draggable(null!));

            scene.Down(35, 35);
            scene.Move(45, 35);
            Assert.IsNotNull(scene.Claims.ClaimOf(VL.Fu.Core.Interaction.PointerIds.Mouse));
            CollectionAssert.AreEqual(new[] { true }, outputs.IsPressed.Values);

            scene.Up(35, 35);
            CollectionAssert.AreEqual(new[] { true, false }, outputs.IsPressed.Values);
            Assert.AreEqual(0, outputs.OnClick.Values.Count);
        }

        [TestMethod]
        public void DragOfAnAncestor_ReleasesThePressAtOnce()
        {
            using var scene = new BehaviorScene();
            using var outputs = new Outputs();
            var clickable = outputs.Connect(new Clickable(null!));
            clickable.SetThreshold(50f);
            scene.Attach(scene.Child, clickable);
            scene.Attach(scene.Front, new Draggable(null!));

            scene.Down(35, 35);
            scene.Move(45, 35);
            CollectionAssert.AreEqual(new[] { true, false }, outputs.IsPressed.Values);

            scene.Up(35, 35);
            CollectionAssert.AreEqual(new[] { true, false }, outputs.IsPressed.Values);
            Assert.AreEqual(0, outputs.OnClick.Values.Count);
        }
    }
}
