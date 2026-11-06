using Fu.Core.Layout;
using VL.Lib.Collections;
using YogaSharp;

namespace Fu.Core.Tests.Layout
{
    /// <summary>
    /// The native config cache is process-wide and tests run in parallel,
    /// so every test uses its own <see cref="YogaConfig.PointScaleFactor"/> to stay independent.
    /// </summary>
    [TestClass]
    public sealed unsafe class YogaConfigTests
    {
        private sealed class Box : Layoutable<Box>
        {
            public Box(string name)
                : base(null!)
            {
                Name = name;
            }

            public string Name { get; }

            public YGNode* Native => Handle;

            public float NativeScale => Native->GetConfig()->GetPointScaleFactor();

            public override string ToString() => Name;
        }

        private static Spread<Box> S(params Box[] boxes) => Spread.Create(boxes);

        [TestMethod]
        public void NewNode_UsesDefaultConfig()
        {
            using Box a = new("a"), b = new("b");

            Assert.IsNull(a.Config);
            Assert.AreSame(YogaConfig.Default, a.EffectiveConfig);
            Assert.IsTrue(a.Native->GetConfig() == b.Native->GetConfig());
            Assert.AreEqual(0f, a.NativeScale);
            Assert.IsTrue(a.Native->GetConfig()->GetUseWebDefaults());
        }

        [TestMethod]
        public void SetConfig_AppliesToWholeSubtree()
        {
            using Box a = new("a"), b = new("b"), c = new("c");
            a.SetChildren(S(b));
            b.SetChildren(S(c));
            var config = new YogaConfig(PointScaleFactor: 1.25f, Errata: YGErrata.Classic);

            a.SetConfig(config);

            foreach (var node in new[] { a, b, c })
            {
                Assert.AreEqual(config, node.EffectiveConfig, node.Name);
                Assert.AreEqual(1.25f, node.NativeScale, node.Name);
                Assert.AreEqual(YGErrata.Classic, node.Native->GetConfig()->GetErrata(), node.Name);
            }
            Assert.IsNull(b.Config);
        }

        [TestMethod]
        public void OwnConfig_WinsOverInheritedOne()
        {
            using Box a = new("a"), b = new("b"), c = new("c");
            a.SetChildren(S(b));
            b.SetChildren(S(c));
            var own = new YogaConfig(PointScaleFactor: 2.25f);

            b.SetConfig(own);
            a.SetConfig(new YogaConfig(PointScaleFactor: 2.5f));

            Assert.AreEqual(2.5f, a.NativeScale);
            Assert.AreEqual(own, b.EffectiveConfig);
            Assert.AreEqual(own, c.EffectiveConfig);
            Assert.AreEqual(2.25f, c.NativeScale);
        }

        [TestMethod]
        public void SetConfig_Null_InheritsAgain()
        {
            using Box a = new("a"), b = new("b");
            a.SetChildren(S(b));
            var inherited = new YogaConfig(PointScaleFactor: 3.25f);
            a.SetConfig(inherited);
            b.SetConfig(new YogaConfig(PointScaleFactor: 3.5f));

            b.SetConfig(null);

            Assert.AreEqual(inherited, b.EffectiveConfig);
            Assert.AreEqual(3.25f, b.NativeScale);
        }

        [TestMethod]
        public void MovedNode_TakesConfigOfNewParent_AndDefaultWhenDetached()
        {
            using Box a = new("a"), b = new("b"), c = new("c");
            a.SetConfig(new YogaConfig(PointScaleFactor: 4.25f));
            b.SetConfig(new YogaConfig(PointScaleFactor: 4.5f));
            a.SetChildren(S(c));
            Assert.AreEqual(4.25f, c.NativeScale);

            c.SetParent(b);
            Assert.AreEqual(4.5f, c.NativeScale);

            c.SetParent(null);
            Assert.AreSame(YogaConfig.Default, c.EffectiveConfig);
            Assert.AreEqual(0f, c.NativeScale);
        }

        [TestMethod]
        public void EqualConfigs_ShareOneNativeConfig()
        {
            using Box a = new("a"), b = new("b");

            a.SetConfig(new YogaConfig(PointScaleFactor: 5.25f));
            b.SetConfig(new YogaConfig(PointScaleFactor: 5.25f));

            Assert.IsTrue(a.Native->GetConfig() == b.Native->GetConfig());
        }

        [TestMethod]
        public void NativeConfig_IsFreedWhenLastNodeLetsGo()
        {
            var config = new YogaConfig(PointScaleFactor: 6.25f);
            using Box a = new("a");
            var b = new Box("b");
            a.SetConfig(config);
            b.SetConfig(config);

            a.SetConfig(null);
            Assert.IsTrue(YogaConfigCache.IsAlive(config), "still used by b");

            b.Dispose();
            Assert.IsFalse(YogaConfigCache.IsAlive(config));
        }

        [TestMethod]
        public void AnimatedConfig_DoesNotKeepOldNativeConfigs()
        {
            using Box a = new("a");

            for (var i = 0; i < 10; i++)
                a.SetConfig(new YogaConfig(PointScaleFactor: 7f + i / 100f));

            for (var i = 0; i < 9; i++)
                Assert.IsFalse(YogaConfigCache.IsAlive(new YogaConfig(PointScaleFactor: 7f + i / 100f)), $"step {i}");
            Assert.AreEqual(7.09f, a.NativeScale);
        }

        [TestMethod]
        public void DisposedParent_LeavesChildrenOnDefaultConfig()
        {
            using Box b = new("b");
            var a = new Box("a");
            a.SetConfig(new YogaConfig(PointScaleFactor: 8.25f));
            a.SetChildren(S(b));

            a.Dispose();

            Assert.AreSame(YogaConfig.Default, b.EffectiveConfig);
            Assert.AreEqual(0f, b.NativeScale);
            Assert.IsTrue(YogaConfigCache.IsAlive(YogaConfig.Default));
        }
    }
}
