using Microsoft.Extensions.Logging;
using VL.Fu.Core.Region;
using VL.Fu.Core.Styles;
using VL.Fu.Core.Styling;
using VL.Fu.Styles;
using VL.Lib.Collections;
using VL.Lib.IO.Notifications;
using VL.Skia;

namespace VL.Fu.Tests.Core.Interaction
{
    [TestClass]
    public sealed class InteractiveTests
    {
        private sealed class Node : RegionProvider
        {
            public Node()
                : base(null!) { }

            public List<string> Warnings { get; } = new();

            public override void Log(LogLevel level, string message, TimeSpan? lifetime = null)
            {
                if (level >= LogLevel.Warning)
                    Warnings.Add(message);
                base.Log(level, message, lifetime);
            }
        }

        private sealed class Behavior : IBehavior
        {
            public bool Notify(INotification notification, CallerInfo caller) => false;
        }

        private static IStyle? StyleOf(PointerEvents value)
        {
            var node = new SetPointerEventsNode(null!);
            node.SetPointerEvents(value);
            return node.Output;
        }

        [TestMethod]
        public void PointerEvents_DefaultsToAuto()
        {
            using var node = new Node();

            Assert.AreEqual(PointerEvents.Auto, node.PointerEvents);
        }

        [TestMethod]
        public void SetPointerEvents_StoresValue()
        {
            using var node = new Node();

            node.SetPointerEvents(PointerEvents.None);

            Assert.AreEqual(PointerEvents.None, node.PointerEvents);
            Assert.AreEqual(0, node.Warnings.Count);
        }

        [TestMethod]
        public void SetPointerEvents_UndefinedValue_IsIgnoredWithWarning()
        {
            using var node = new Node();
            node.SetPointerEvents(PointerEvents.None);

            node.SetPointerEvents((PointerEvents)42);

            Assert.AreEqual(PointerEvents.None, node.PointerEvents);
            Assert.AreEqual(1, node.Warnings.Count);
        }

        [TestMethod]
        public void Style_CommitsPointerEvents()
        {
            using var node = new Node();

            node.SetStyle(StyleOf(PointerEvents.None));

            Assert.AreEqual(PointerEvents.None, node.PointerEvents);
        }

        [TestMethod]
        public void Style_Removed_ReturnsToAuto()
        {
            using var node = new Node();
            node.SetStyle(StyleOf(PointerEvents.None));

            node.SetStyle(null);

            Assert.AreEqual(PointerEvents.Auto, node.PointerEvents);
        }

        [TestMethod]
        public void Style_Unchanged_KeepsDirectValue()
        {
            using var node = new Node();
            node.SetStyle(new PointerEventsStyle(Array.Empty<IStyle>(), PointerEvents.Auto));

            node.SetPointerEvents(PointerEvents.None);
            node.SetStyle(new PointerEventsStyle(Array.Empty<IStyle>(), PointerEvents.Auto));

            Assert.AreEqual(PointerEvents.None, node.PointerEvents);
        }

        [TestMethod]
        public void Behaviors_DefaultToEmpty()
        {
            using var node = new Node();

            Assert.AreEqual(0, node.Behaviors.Count);
        }

        [TestMethod]
        public void SetBehaviors_StoresInPinOrder()
        {
            using var node = new Node();
            var a = new Behavior();
            var b = new Behavior();

            node.SetBehaviors(Spread.Create<IBehavior>(a, b));

            Assert.AreEqual(2, node.Behaviors.Count);
            Assert.AreSame(a, node.Behaviors[0]);
            Assert.AreSame(b, node.Behaviors[1]);
        }

        [TestMethod]
        public void SetBehaviors_Null_ClearsBehaviors()
        {
            using var node = new Node();
            node.SetBehaviors(Spread.Create<IBehavior>(new Behavior()));

            node.SetBehaviors((IReadOnlyList<IBehavior>?)null);

            Assert.AreEqual(0, node.Behaviors.Count);
            Assert.AreEqual(0, node.Warnings.Count);
        }

        [TestMethod]
        public void SetBehaviors_EmptyEntriesAndRepeats_AreDroppedWithOneWarning()
        {
            using var node = new Node();
            var a = new Behavior();
            var b = new Behavior();

            node.SetBehaviors(Spread.Create<IBehavior>(a, null!, a, b, null!));

            Assert.AreEqual(2, node.Behaviors.Count);
            Assert.AreSame(a, node.Behaviors[0]);
            Assert.AreSame(b, node.Behaviors[1]);
            Assert.AreEqual(1, node.Warnings.Count);
        }

        [TestMethod]
        public void SetBehaviors_EqualSequence_KeepsList()
        {
            using var node = new Node();
            var a = new Behavior();
            node.SetBehaviors(Spread.Create<IBehavior>(a));
            var before = node.Behaviors;

            node.SetBehaviors(Spread.Create<IBehavior>(a));

            Assert.AreSame(before, node.Behaviors);
        }

        [TestMethod]
        public void SetBehaviors_FuNode_IsIgnoredWithWarning()
        {
            using var node = new Node();
            using var other = new Node();
            var a = new Behavior();

            node.SetBehaviors(Spread.Create<IBehavior>(other, a));

            Assert.AreEqual(1, node.Behaviors.Count);
            Assert.AreSame(a, node.Behaviors[0]);
            Assert.AreEqual(1, node.Warnings.Count);
        }
    }
}
