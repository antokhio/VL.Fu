using VL.Fu.Behaviors;

namespace VL.Fu.Tests.Behaviors
{
    /// <summary>
    /// <see cref="Hoverable"/> follows the pointer's hover path into its channel.
    /// </summary>
    [TestClass]
    public sealed class HoverableTests
    {
        [TestMethod]
        public void EnterAndLeave_WriteTheHoverState()
        {
            using var scene = new BehaviorScene();
            using var isHovered = new Recorder<bool>(false);
            var hoverable = new Hoverable(null!);
            hoverable.SetIsHovered(isHovered.Channel);
            scene.Attach(scene.Child, hoverable);

            scene.Move(35, 35);
            scene.Move(36, 36);
            scene.Move(15, 15);

            CollectionAssert.AreEqual(new[] { true, false }, isHovered.Values);
        }

        [TestMethod]
        public void Disabling_WritesFalse_AndEnablingWritesWhetherThePointerIsOverTheNodeNow()
        {
            using var scene = new BehaviorScene();
            using var isHovered = new Recorder<bool>(false);
            var hoverable = new Hoverable(null!);
            hoverable.SetIsHovered(isHovered.Channel);
            scene.Attach(scene.Child, hoverable);
            scene.Move(35, 35);

            hoverable.SetEnabled(false);
            scene.Move(15, 15);
            scene.Move(35, 35);
            hoverable.SetEnabled(true);

            CollectionAssert.AreEqual(new[] { true, false, true }, isHovered.Values);
        }

        [TestMethod]
        public void SwappingTheChannel_TellsTheOldOneThePointerLeft_AndTheNewOneWhereItIs()
        {
            using var scene = new BehaviorScene();
            using var first = new Recorder<bool>(false);
            using var second = new Recorder<bool>(false);
            var hoverable = new Hoverable(null!);
            hoverable.SetIsHovered(first.Channel);
            scene.Attach(scene.Child, hoverable);
            scene.Move(35, 35);

            hoverable.SetIsHovered(second.Channel);

            CollectionAssert.AreEqual(new[] { true, false }, first.Values);
            CollectionAssert.AreEqual(new[] { true }, second.Values);
        }
    }
}
