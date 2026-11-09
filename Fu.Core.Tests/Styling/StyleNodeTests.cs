using Fu.Core.Rendering;
using Fu.Core.Styles;
using Fu.Core.Styling;
using VL.Fu.Styles;
using VL.Lib.Collections;
using YogaSharp;

namespace Fu.Core.Tests.Styling
{
    [TestClass]
    public sealed unsafe class StyleNodeTests
    {
        private sealed class Node : Renderable
        {
            public Node()
                : base(null!) { }

            public YGNode* Native => Handle;
        }

        private static Spread<Style> S(params Style?[] items) => Spread.Create(items!);

        private static SetWidthNode Width(float width, params Style?[] inputs)
        {
            var node = new SetWidthNode(null!);
            node.SetInput(S(inputs));
            node.SetWidth(width);
            return node;
        }

        [TestMethod]
        public void Output_FromConstruction_IsBuilt()
        {
            Assert.IsNull(new SetWidthNode(null!).Output);
            Assert.IsNull(new SetWidthRatioNode(null!).Output);
            Assert.IsNull(new SetHeightNode(null!).Output);
            Assert.IsNull(new SetHeightRatioNode(null!).Output);
            Assert.IsNull(new SetSizeNode(null!).Output);
            Assert.IsNull(new SetSizeRatioNode(null!).Output);
            Assert.IsNull(new SetGapNode(null!).Output);
            Assert.IsNull(new SetTransformNode(null!).Output);
            Assert.IsNull(new CombineNode(null!).Output);
            Assert.AreEqual(new WidthStyle(S(), YGValue.Auto), new SetWidthAutoNode(null!).Output);
            Assert.AreEqual(new HeightStyle(S(), YGValue.Auto), new SetHeightAutoNode(null!).Output);
            Assert.AreEqual(new SizeStyle(S(), YGValue.Auto, YGValue.Auto), new SetSizeAutoNode(null!).Output);
        }

        [TestMethod]
        public void Output_Unchanged_IsSameReference()
        {
            var width = Width(10);
            var output = width.Output;

            width.SetWidth(10);
            width.SetInput(S());

            Assert.AreSame(output, width.Output);
        }

        [TestMethod]
        public void SetInput_NewSpreadWithSameItems_KeepsOutput()
        {
            var ten = Width(10);
            var five = Width(5, ten.Output);
            var output = five.Output;

            five.SetInput(S(ten.Output));

            Assert.AreSame(output, five.Output);
        }

        [TestMethod]
        public void SetInput_ChangedItem_RebuildsOutput()
        {
            var ten = Width(10);
            var five = Width(5, ten.Output);
            var output = five.Output;

            ten.SetWidth(11);
            five.SetInput(S(ten.Output));

            Assert.AreNotSame(output, five.Output);
            Assert.AreEqual(new WidthStyle(S(new WidthStyle(S(), 11)), 5), five.Output);
        }

        [TestMethod]
        public void SetInput_Null_IsEmpty()
        {
            var width = Width(10);

            width.SetInput(null!);

            Assert.AreEqual(new WidthStyle(S(), 10), width.Output);
        }

        [TestMethod]
        public void Combine_UnconnectedPins_AreSkipped()
        {
            var ten = Width(10).Output;
            var combine = new CombineNode(null!);

            combine.SetInput(S(null, ten, null));

            Assert.AreSame(ten, combine.Output);
        }

        [TestMethod]
        public void Combine_SeveralInputs_KeepsOrder()
        {
            var ten = Width(10).Output;
            var five = Width(5).Output;
            var combine = new CombineNode(null!);

            combine.SetInput(S(ten, five));

            CollectionAssert.AreEqual(new[] { ten, five }, combine.Output!.Inputs.ToArray());
        }

        [TestMethod]
        public void SetWidth_EmptyValue_OutputsInputs()
        {
            var ten = Width(10);
            var five = Width(5, ten.Output);

            five.SetWidth(default);

            Assert.AreSame(ten.Output, five.Output);
        }

        [TestMethod]
        public void SetWidth_NaN_IsIgnored()
        {
            var width = Width(float.NaN);

            Assert.IsNull(width.Output);
        }

        [TestMethod]
        public void SetWidth_EmptyValue_NodeFallsBackToEarlierSetter()
        {
            using var node = new Node();
            var ten = Width(10);
            var five = Width(5, ten.Output);
            node.SetStyle(five.Output);
            Assert.AreEqual(YGValue.Point(5), node.Native->GetWidth());

            five.SetWidth(default);
            node.SetStyle(five.Output);

            Assert.AreEqual(YGValue.Point(10), node.Native->GetWidth());
        }

        [TestMethod]
        public void SetWidthRatio_AfterSetWidth_Wins()
        {
            using var node = new Node();
            var ratio = new SetWidthRatioNode(null!);
            ratio.SetInput(S(Width(10).Output));
            ratio.SetWidthRatio(0.5f);

            node.SetStyle(ratio.Output);

            Assert.AreEqual(YGValue.Percent(50), node.Native->GetWidth());
        }

        [TestMethod]
        public void SetWidthRatio_EmptyValue_PassesInputsThrough()
        {
            var ten = Width(10).Output;
            var ratio = new SetWidthRatioNode(null!);
            ratio.SetInput(S(ten));
            ratio.SetWidthRatio(0.5f);

            ratio.SetWidthRatio(default);

            Assert.AreSame(ten, ratio.Output);
        }

        [TestMethod]
        public void SetHeight_WritesHeightOnly()
        {
            using var node = new Node();
            var height = new SetHeightNode(null!);
            height.SetHeight(20);

            node.SetStyle(height.Output);

            Assert.AreEqual(YGValue.Point(20), node.Native->GetHeight());
            Assert.AreEqual(YGValue.Auto, node.Native->GetWidth());
        }

        [TestMethod]
        public void SetHeightAuto_AfterSetHeight_Wins()
        {
            using var node = new Node();
            var height = new SetHeightNode(null!);
            height.SetHeight(20);
            var auto = new SetHeightAutoNode(null!);
            auto.SetInput(S(height.Output));

            node.SetStyle(auto.Output);

            Assert.AreEqual(YGValue.Auto, node.Native->GetHeight());
        }

        [TestMethod]
        public void SetSize_WritesBothAxes()
        {
            using var node = new Node();
            var size = new SetSizeNode(null!);
            size.SetSize(new Stride.Core.Mathematics.Vector2(30, 40));

            node.SetStyle(size.Output);

            Assert.AreEqual(YGValue.Point(30), node.Native->GetWidth());
            Assert.AreEqual(YGValue.Point(40), node.Native->GetHeight());
        }

        [TestMethod]
        public void SetWidth_AfterSetSize_OverridesWidthOnly()
        {
            using var node = new Node();
            var size = new SetSizeNode(null!);
            size.SetSize(new Stride.Core.Mathematics.Vector2(30, 40));

            node.SetStyle(Width(10, size.Output).Output);

            Assert.AreEqual(YGValue.Point(10), node.Native->GetWidth());
            Assert.AreEqual(YGValue.Point(40), node.Native->GetHeight());
        }

        [TestMethod]
        public void SetSize_NonFiniteComponent_IgnoresWholeValue()
        {
            var size = new SetSizeNode(null!);

            size.SetSize(new Stride.Core.Mathematics.Vector2(30, float.NaN));

            Assert.IsNull(size.Output);
        }

        /// <summary>
        /// A 200 x 100 root with one child sized to half its width and a quarter of its height.
        /// </summary>
        [TestMethod]
        public void SetSizeRatio_LaysOutRelativeToParent()
        {
            using var root = new Node();
            using var child = new Node();
            root.SetChildren(Spread.Create(new[] { child }));
            var rootSize = new SetSizeNode(null!);
            rootSize.SetSize(new Stride.Core.Mathematics.Vector2(200, 100));
            root.SetStyle(rootSize.Output);
            var ratio = new SetSizeRatioNode(null!);
            ratio.SetSizeRatio(new Stride.Core.Mathematics.Vector2(0.5f, 0.25f));
            child.SetStyle(ratio.Output);

            root.CalculateLayout();

            Assert.AreEqual(100f, child.Layout.Width);
            Assert.AreEqual(25f, child.Layout.Height);
        }

        [TestMethod]
        public void SizeStyle_UnsupportedUnit_SkipsBothAxes()
        {
            using var node = new Node();

            node.SetStyle(new SizeStyle(S(), YGValue.Point(30), YGValue.Undefined));

            Assert.AreEqual(YGValue.Auto, node.Native->GetWidth());
            Assert.AreEqual(YGValue.Auto, node.Native->GetHeight());
        }

        [TestMethod]
        public void SetGap_UnknownGutter_IsCorrectedToAll()
        {
            var gap = new SetGapNode(null!);

            gap.SetGap(10, (YGGutter)42);

            Assert.AreEqual(new GapStyle(S(), YGGutter.All, 10), gap.Output);
        }

        [TestMethod]
        public void SetGap_RowAndColumnSetters_OverrideAll()
        {
            using var node = new Node();
            var all = new SetGapNode(null!);
            all.SetGap(10);
            var row = new SetRowGapNode(null!);
            row.SetInput(S(all.Output));
            row.SetRowGap(20);
            var column = new SetColumnGapNode(null!);
            column.SetInput(S(row.Output));
            column.SetColumnGap(30);

            node.SetStyle(column.Output);

            Assert.AreEqual(10f, node.Native->GetGap(YGGutter.All));
            Assert.AreEqual(20f, node.Native->GetGap(YGGutter.Row));
            Assert.AreEqual(30f, node.Native->GetGap(YGGutter.Column));
        }

        /// <summary>
        /// A row of two 10 wide children: the second one starts after the first one and the column gap.
        /// </summary>
        [TestMethod]
        public void SetColumnGap_Removed_LayoutFallsBackToGap()
        {
            using var root = new Node();
            using var a = new Node();
            using var b = new Node();
            root.SetChildren(Spread.Create(new[] { a, b }));
            var childWidth = Width(10).Output;
            a.SetStyle(childWidth);
            b.SetStyle(childWidth);

            var all = new SetGapNode(null!);
            all.SetGap(10);
            var column = new SetColumnGapNode(null!);
            column.SetInput(S(all.Output));
            column.SetColumnGap(30);

            root.SetStyle(column.Output);
            root.CalculateLayout();
            Assert.AreEqual(40f, b.Layout.X);

            root.SetStyle(all.Output);
            root.CalculateLayout();
            Assert.AreEqual(20f, b.Layout.X);
        }

        [TestMethod]
        public void SetTransform_EmptyValue_NodeReturnsToIdentity()
        {
            using var node = new Node();
            var transform = new SetTransformNode(null!);
            transform.SetTransform(Stride.Core.Mathematics.Matrix.Translation(5, 0, 0));
            node.SetStyle(transform.Output);

            transform.SetTransform(default);
            node.SetStyle(transform.Output);

            Assert.AreEqual(Stride.Core.Mathematics.Matrix.Identity, node.Transformation);
        }
    }
}
