using VL.Fu.Core.Region;
using VL.Fu.Core.Styles;
using VL.Fu.Core.Styling;
using VL.Fu.Styles;
using VL.Lib.Collections;
using YogaSharp;

namespace VL.Fu.Tests.Styles
{
    [TestClass]
    public sealed unsafe class StyleNodeTests
    {
        private sealed class Node : RegionProvider
        {
            public Node()
                : base(null!) { }

            public YGNode* Native => Handle;
        }

        private static Spread<IStyle> S(params IStyle?[] items) => Spread.Create(items!);

        private static SetWidthNode Width(float width, params IStyle?[] inputs)
        {
            var node = new SetWidthNode(null!);
            node.SetInput(S(inputs));
            node.SetWidth(width);
            return node;
        }

        private static IStyle? Height(float height)
        {
            var node = new SetHeightNode(null!);
            node.SetHeight(height);
            return node.Output;
        }

        private static IStyle? Size(float width, float height)
        {
            var node = new SetSizeNode(null!);
            node.SetSize(new Stride.Core.Mathematics.Vector2(width, height));
            return node.Output;
        }

        private static IStyle? Combine(params IStyle?[] styles)
        {
            var node = new CombineNode(null!);
            node.SetInput(S(styles));
            return node.Output;
        }

        /// <summary>
        /// Lays out a root styled with <paramref name="rootStyle"/> and one child per entry of <paramref name="childStyles"/>,
        /// and returns the children's layouts.
        /// </summary>
        private static Stride.Core.Mathematics.RectangleF[] LayOut(IStyle? rootStyle, params IStyle?[] childStyles)
        {
            using var root = new Node();
            var children = childStyles.Select(_ => new Node()).ToArray();
            try
            {
                root.SetChildren(Spread.Create(children));
                root.SetStyle(rootStyle);
                for (var i = 0; i < children.Length; i++)
                    children[i].SetStyle(childStyles[i]);

                root.CalculateLayout();
                return children.Select(c => c.Layout).ToArray();
            }
            finally
            {
                foreach (var child in children)
                    child.Dispose();
            }
        }

        private enum EdgeKind
        {
            Padding,
            Margin,
            Border,
        }

        /// <summary>
        /// Lays out a lone node styled with <paramref name="style"/> left to right and returns its resolved
        /// left, right, top and bottom padding, margin or border, after Yoga picked the winning slot per edge.
        /// </summary>
        private static float[] Resolved(IStyle? style, EdgeKind kind)
        {
            using var node = new Node();
            node.SetStyle(style);
            node.CalculateLayout();

            var edges = new[] { YGEdge.Left, YGEdge.Right, YGEdge.Top, YGEdge.Bottom };
            var result = new float[edges.Length];
            for (var i = 0; i < edges.Length; i++)
            {
                result[i] = kind switch
                {
                    EdgeKind.Padding => node.Native->GetComputedPadding(edges[i]),
                    EdgeKind.Margin => node.Native->GetComputedMargin(edges[i]),
                    _ => node.Native->GetComputedBorder(edges[i]),
                };
            }

            return result;
        }

        [TestMethod]
        public void Output_FromConstruction_IsBuilt()
        {
            Assert.IsNull(new SetWidthNode(null!).Output);
#pragma warning disable CS0618 // Obsolete, but still imported, so it must still build.
            Assert.IsNull(new SetWidthPercentNode(null!).Output);
#pragma warning restore CS0618
            Assert.IsNull(new SetWidthRatioNode(null!).Output);
            Assert.IsNull(new SetHeightNode(null!).Output);
            Assert.IsNull(new SetHeightRatioNode(null!).Output);
            Assert.IsNull(new SetSizeNode(null!).Output);
            Assert.IsNull(new SetSizeRatioNode(null!).Output);
            Assert.IsNull(new SetGapNode(null!).Output);
            Assert.IsNull(new SetRowGapNode(null!).Output);
            Assert.IsNull(new SetColumnGapNode(null!).Output);
            Assert.IsNull(new SetTransformNode(null!).Output);
            Assert.IsNull(new CombineNode(null!).Output);
            Assert.IsNull(new SetFlexDirectionNode(null!).Output);
            Assert.IsNull(new SetFlexWrapNode(null!).Output);
            Assert.IsNull(new SetFlexGrowNode(null!).Output);
            Assert.IsNull(new SetFlexShrinkNode(null!).Output);
            Assert.IsNull(new SetFlexBasisNode(null!).Output);
            Assert.IsNull(new SetFlexBasisRatioNode(null!).Output);
            Assert.IsNull(new SetJustifyContentNode(null!).Output);
            Assert.IsNull(new SetAlignItemsNode(null!).Output);
            Assert.IsNull(new SetAlignSelfNode(null!).Output);
            Assert.IsNull(new SetAlignContentNode(null!).Output);
            Assert.IsNull(new SetPaddingNode(null!).Output);
            Assert.IsNull(new SetPaddingUniformNode(null!).Output);
            Assert.IsNull(new SetMarginUniformNode(null!).Output);
            Assert.IsNull(new SetBorderUniformNode(null!).Output);
            Assert.IsNull(new SetPaddingHorizontalNode(null!).Output);
            Assert.IsNull(new SetPaddingVerticalNode(null!).Output);
            Assert.IsNull(new SetPaddingLRTBNode(null!).Output);
            Assert.IsNull(new SetMarginNode(null!).Output);
            Assert.IsNull(new SetMarginHorizontalNode(null!).Output);
            Assert.IsNull(new SetMarginVerticalNode(null!).Output);
            Assert.IsNull(new SetMarginLRTBNode(null!).Output);
            Assert.IsNull(new SetBorderNode(null!).Output);
            Assert.IsNull(new SetBorderHorizontalNode(null!).Output);
            Assert.IsNull(new SetBorderVerticalNode(null!).Output);
            Assert.IsNull(new SetBorderLRTBNode(null!).Output);
            Assert.IsNull(new SetMinWidthNode(null!).Output);
            Assert.IsNull(new SetMinWidthRatioNode(null!).Output);
            Assert.IsNull(new SetMinHeightNode(null!).Output);
            Assert.IsNull(new SetMinHeightRatioNode(null!).Output);
            Assert.IsNull(new SetMaxWidthNode(null!).Output);
            Assert.IsNull(new SetMaxWidthRatioNode(null!).Output);
            Assert.IsNull(new SetMaxHeightNode(null!).Output);
            Assert.IsNull(new SetMaxHeightRatioNode(null!).Output);
            Assert.IsNull(new SetAspectRatioNode(null!).Output);
            Assert.IsNull(new SetAspectRatioVector2Node(null!).Output);
            Assert.IsNull(new SetAspectRatioInt2Node(null!).Output);
            Assert.AreEqual(new WidthStyle(S(), YGValue.Auto), new SetWidthAutoNode(null!).Output);
            Assert.AreEqual(new HeightStyle(S(), YGValue.Auto), new SetHeightAutoNode(null!).Output);
            Assert.AreEqual(new SizeStyle(S(), YGValue.Auto, YGValue.Auto), new SetSizeAutoNode(null!).Output);
            Assert.AreEqual(new FlexBasisStyle(S(), YGValue.Auto), new SetFlexBasisAutoNode(null!).Output);
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

        [TestMethod]
        public void SetTransform_NonFiniteElement_IgnoresWholeValue()
        {
            var transform = new SetTransformNode(null!);

            transform.SetTransform(Stride.Core.Mathematics.Matrix.Translation(5, float.NaN, 0));

            Assert.IsNull(transform.Output);
        }

        [TestMethod]
        public void SetTransform_NonFiniteElement_EarlierSetterHolds()
        {
            using var node = new Node();
            var first = new SetTransformNode(null!);
            first.SetTransform(Stride.Core.Mathematics.Matrix.Translation(5, 0, 0));
            var second = new SetTransformNode(null!);
            second.SetInput(S(first.Output));
            second.SetTransform(Stride.Core.Mathematics.Matrix.Scaling(float.PositiveInfinity));

            node.SetStyle(second.Output);

            Assert.AreSame(first.Output, second.Output);
            Assert.AreEqual(Stride.Core.Mathematics.Matrix.Translation(5, 0, 0), node.Transformation);
        }

        [TestMethod]
        public void SetFlexDirection_WritesSlot()
        {
            using var node = new Node();
            var direction = new SetFlexDirectionNode(null!);
            direction.SetFlexDirection(YGFlexDirection.Column);

            node.SetStyle(direction.Output);

            Assert.AreEqual(YGFlexDirection.Column, node.Native->GetFlexDirection());
        }

        [TestMethod]
        public void SetFlexDirection_UnknownValue_IsIgnored()
        {
            var direction = new SetFlexDirectionNode(null!);

            direction.SetFlexDirection((YGFlexDirection)99);

            Assert.IsNull(direction.Output);
        }

        [TestMethod]
        public void SetFlexDirection_EmptyValue_PassesInputsThrough()
        {
            var ten = Width(10).Output;
            var direction = new SetFlexDirectionNode(null!);
            direction.SetInput(S(ten));
            direction.SetFlexDirection(YGFlexDirection.Column);

            direction.SetFlexDirection(default);

            Assert.AreSame(ten, direction.Output);
        }

        [TestMethod]
        public void SetFlexDirection_Column_StacksChildren()
        {
            var direction = new SetFlexDirectionNode(null!);
            direction.SetInput(S(Size(100, 100)));
            direction.SetFlexDirection(YGFlexDirection.Column);

            var layouts = LayOut(direction.Output, Height(10), Height(10));

            Assert.AreEqual(10f, layouts[1].Y);
            Assert.AreEqual(100f, layouts[1].Width);
        }

        [TestMethod]
        public void SetFlexWrap_WritesSlot()
        {
            using var node = new Node();
            var wrap = new SetFlexWrapNode(null!);
            wrap.SetFlexWrap(YGWrap.Wrap);

            node.SetStyle(wrap.Output);

            Assert.AreEqual(YGWrap.Wrap, node.Native->GetFlexWrap());
        }

        [TestMethod]
        public void SetJustifyContent_Center_CentersChild()
        {
            var justify = new SetJustifyContentNode(null!);
            justify.SetInput(S(Size(100, 100)));
            justify.SetJustifyContent(YGJustify.Center);

            var layouts = LayOut(justify.Output, Width(20).Output);

            Assert.AreEqual(40f, layouts[0].X);
        }

        [TestMethod]
        public void SetAlignSelf_BeatsParentAlignItems()
        {
            var alignItems = new SetAlignItemsNode(null!);
            alignItems.SetInput(S(Size(100, 100)));
            alignItems.SetAlignItems(YGAlign.Center);
            var alignSelf = new SetAlignSelfNode(null!);
            alignSelf.SetInput(S(Size(20, 20)));
            alignSelf.SetAlignSelf(YGAlign.FlexEnd);

            var layouts = LayOut(alignItems.Output, Size(20, 20), alignSelf.Output);

            Assert.AreEqual(40f, layouts[0].Y);
            Assert.AreEqual(80f, layouts[1].Y);
        }

        [TestMethod]
        public void SetAlignItems_Auto_IsIgnored()
        {
            var alignItems = new SetAlignItemsNode(null!);

            alignItems.SetAlignItems(YGAlign.Auto);

            Assert.IsNull(alignItems.Output);
        }

        [TestMethod]
        public void SetAlignSelf_Auto_IsKept()
        {
            var alignSelf = new SetAlignSelfNode(null!);

            alignSelf.SetAlignSelf(YGAlign.Auto);

            Assert.AreEqual(new AlignSelfStyle(S(), YGAlign.Auto), alignSelf.Output);
        }

        [TestMethod]
        public void SetAlignContent_Baseline_IsIgnored()
        {
            var alignContent = new SetAlignContentNode(null!);

            alignContent.SetAlignContent(YGAlign.Baseline);

            Assert.IsNull(alignContent.Output);
        }

        [TestMethod]
        public void SetAlignContent_SpaceBetween_WritesSlot()
        {
            using var node = new Node();
            var alignContent = new SetAlignContentNode(null!);
            alignContent.SetAlignContent(YGAlign.SpaceBetween);

            node.SetStyle(alignContent.Output);

            Assert.AreEqual(YGAlign.SpaceBetween, node.Native->GetAlignContent());
        }

        [TestMethod]
        public void AlignItemsStyle_UnsupportedValue_IsSkipped()
        {
            using var node = new Node();

            node.SetStyle(new AlignItemsStyle(S(), YGAlign.SpaceAround));

            Assert.AreEqual(YGAlign.Stretch, node.Native->GetAlignItems());
        }

        [TestMethod]
        public void SetFlexGrow_TakesFreeSpace()
        {
            var grow = new SetFlexGrowNode(null!);
            grow.SetFlexGrow(1);

            var layouts = LayOut(Size(100, 100), Width(20).Output, grow.Output);

            Assert.AreEqual(80f, layouts[1].Width);
        }

        [TestMethod]
        public void SetFlexGrow_Negative_IsIgnored()
        {
            var grow = new SetFlexGrowNode(null!);

            grow.SetFlexGrow(-1);

            Assert.IsNull(grow.Output);
        }

        [TestMethod]
        public void SetFlexShrink_Zero_WritesSlot()
        {
            using var node = new Node();
            var shrink = new SetFlexShrinkNode(null!);
            shrink.SetFlexShrink(0);

            node.SetStyle(shrink.Output);

            Assert.AreEqual(0f, node.Native->GetFlexShrink());
        }

        [TestMethod]
        public void SetFlexShrink_NaN_IsIgnored()
        {
            var shrink = new SetFlexShrinkNode(null!);

            shrink.SetFlexShrink(float.NaN);

            Assert.IsNull(shrink.Output);
        }

        [TestMethod]
        public void SetFlexBasisAuto_AfterSetFlexBasis_Wins()
        {
            using var node = new Node();
            var basis = new SetFlexBasisNode(null!);
            basis.SetFlexBasis(10);
            var auto = new SetFlexBasisAutoNode(null!);
            auto.SetInput(S(basis.Output));

            node.SetStyle(auto.Output);

            Assert.AreEqual(YGValue.Auto, node.Native->GetFlexBasis());
        }

        [TestMethod]
        public void SetFlexBasisRatio_IsShareOfParent()
        {
            var basis = new SetFlexBasisRatioNode(null!);
            basis.SetFlexBasisRatio(0.25f);

            var layouts = LayOut(Size(200, 100), basis.Output);

            Assert.AreEqual(50f, layouts[0].Width);
        }

        [TestMethod]
        public void SetPadding_All_ResolvesAllEdges()
        {
            var padding = new SetPaddingNode(null!);
            padding.SetPadding(10);

            CollectionAssert.AreEqual(new[] { 10f, 10f, 10f, 10f }, Resolved(padding.Output, EdgeKind.Padding));
        }

        [TestMethod]
        public void SetPadding_WritesNativeSlot()
        {
            using var node = new Node();
            var padding = new SetPaddingNode(null!);
            padding.SetPadding(10, YGEdge.Start);

            node.SetStyle(padding.Output);

            Assert.AreEqual(new PaddingStyle(S(), YGEdge.Start, 10), padding.Output);
            Assert.AreEqual(YGValue.Point(10), node.Native->GetPadding(YGEdge.Start));
        }

        [TestMethod]
        public void SetPadding_Start_FollowsDirection()
        {
            var padding = new SetPaddingNode(null!);
            padding.SetPadding(10, YGEdge.Start);

            CollectionAssert.AreEqual(new[] { 10f, 0f, 0f, 0f }, Resolved(padding.Output, EdgeKind.Padding));
        }

        [TestMethod]
        public void SetPadding_AllAfterStart_Wins()
        {
            var start = new SetPaddingNode(null!);
            start.SetPadding(5, YGEdge.Start);
            var all = new SetPaddingNode(null!);
            all.SetInput(S(start.Output));
            all.SetPadding(10);

            CollectionAssert.AreEqual(new[] { 10f, 10f, 10f, 10f }, Resolved(all.Output, EdgeKind.Padding));
        }

        [TestMethod]
        public void SetPadding_UnknownEdge_IsIgnored()
        {
            var padding = new SetPaddingNode(null!);

            padding.SetPadding(10, (YGEdge)99);

            Assert.IsNull(padding.Output);
        }

        [TestMethod]
        public void SetPaddingUniform_AfterSetPaddingLRTB_Wins()
        {
            var lrtb = new SetPaddingLRTBNode(null!);
            lrtb.SetPaddingLRTB(1, 2, 3, 4);
            var padding = new SetPaddingUniformNode(null!);
            padding.SetInput(S(lrtb.Output));
            padding.SetPaddingUniform(10);

            CollectionAssert.AreEqual(new[] { 10f, 10f, 10f, 10f }, Resolved(padding.Output, EdgeKind.Padding));
        }

        [TestMethod]
        public void SetPaddingHorizontal_AfterLeft_Wins()
        {
            var lrtb = new SetPaddingLRTBNode(null!);
            lrtb.SetPaddingLRTB(left: 1);
            var horizontal = new SetPaddingHorizontalNode(null!);
            horizontal.SetInput(S(lrtb.Output));
            horizontal.SetPaddingHorizontal(10);

            CollectionAssert.AreEqual(new[] { 10f, 10f, 0f, 0f }, Resolved(horizontal.Output, EdgeKind.Padding));
        }

        [TestMethod]
        public void SetPaddingLRTB_AfterStart_Wins()
        {
            var start = new SetPaddingNode(null!);
            start.SetPadding(10, YGEdge.Start);
            var lrtb = new SetPaddingLRTBNode(null!);
            lrtb.SetInput(S(start.Output));
            lrtb.SetPaddingLRTB(left: 5);

            CollectionAssert.AreEqual(new[] { 5f, 0f, 0f, 0f }, Resolved(lrtb.Output, EdgeKind.Padding));
        }

        [TestMethod]
        public void SetPaddingHorizontal_KeepsEarlierVerticalEdges()
        {
            var padding = new SetPaddingUniformNode(null!);
            padding.SetPaddingUniform(5);
            var horizontal = new SetPaddingHorizontalNode(null!);
            horizontal.SetInput(S(padding.Output));
            horizontal.SetPaddingHorizontal(10);

            CollectionAssert.AreEqual(new[] { 10f, 10f, 5f, 5f }, Resolved(horizontal.Output, EdgeKind.Padding));
        }

        [TestMethod]
        public void SetPaddingVertical_WritesOnlyTopAndBottom()
        {
            var vertical = new SetPaddingVerticalNode(null!);
            vertical.SetPaddingVertical(10);

            CollectionAssert.AreEqual(new[] { 0f, 0f, 10f, 10f }, Resolved(vertical.Output, EdgeKind.Padding));
        }

        [TestMethod]
        public void SetPaddingLRTB_EmptyEdge_KeepsEarlierSetter()
        {
            var uniform = new SetPaddingUniformNode(null!);
            uniform.SetPaddingUniform(10);
            var lrtb = new SetPaddingLRTBNode(null!);
            lrtb.SetInput(S(uniform.Output));
            lrtb.SetPaddingLRTB(left: 5);

            Assert.AreEqual(new PaddingLRTBStyle(S(uniform.Output), 5, float.NaN, float.NaN, float.NaN), lrtb.Output);
            CollectionAssert.AreEqual(new[] { 5f, 10f, 10f, 10f }, Resolved(lrtb.Output, EdgeKind.Padding));
        }

        [TestMethod]
        public void SetPadding_Negative_IsIgnored()
        {
            var padding = new SetPaddingNode(null!);

            padding.SetPadding(-1);

            Assert.IsNull(padding.Output);
        }

        [TestMethod]
        public void PaddingLRTBStyle_InfiniteEdge_IsSkipped()
        {
            CollectionAssert.AreEqual(
                new[] { 0f, 0f, 0f, 0f },
                Resolved(new PaddingLRTBStyle(S(), 1, float.PositiveInfinity, 1, 1), EdgeKind.Padding)
            );
        }

        [TestMethod]
        public void PaddingStyle_UnknownEdgeOrNaN_IsSkipped()
        {
            CollectionAssert.AreEqual(new[] { 0f, 0f, 0f, 0f }, Resolved(new PaddingStyle(S(), (YGEdge)99, 1), EdgeKind.Padding));
            CollectionAssert.AreEqual(
                new[] { 0f, 0f, 0f, 0f },
                Resolved(new PaddingStyle(S(), YGEdge.All, float.NaN), EdgeKind.Padding)
            );
        }

        [TestMethod]
        public void SetPadding_InsetsChildren()
        {
            var padding = new SetPaddingNode(null!);
            padding.SetInput(S(Size(100, 100)));
            padding.SetPadding(10);

            var layouts = LayOut(padding.Output, (IStyle?)null);

            Assert.AreEqual(10f, layouts[0].X);
            Assert.AreEqual(10f, layouts[0].Y);
            Assert.AreEqual(80f, layouts[0].Height);
        }

        [TestMethod]
        public void SetMargin_Negative_IsWritten()
        {
            var margin = new SetMarginNode(null!);
            margin.SetMargin(-5);

            CollectionAssert.AreEqual(new[] { -5f, -5f, -5f, -5f }, Resolved(margin.Output, EdgeKind.Margin));
        }

        [TestMethod]
        public void SetMargin_End_FollowsDirection()
        {
            var margin = new SetMarginNode(null!);
            margin.SetMargin(4, YGEdge.End);

            CollectionAssert.AreEqual(new[] { 0f, 4f, 0f, 0f }, Resolved(margin.Output, EdgeKind.Margin));
        }

        [TestMethod]
        public void SetMarginHorizontalAndVertical_WriteTheirEdges()
        {
            var horizontal = new SetMarginHorizontalNode(null!);
            horizontal.SetMarginHorizontal(3);
            var vertical = new SetMarginVerticalNode(null!);
            vertical.SetInput(S(horizontal.Output));
            vertical.SetMarginVertical(4);

            CollectionAssert.AreEqual(new[] { 3f, 3f, 4f, 4f }, Resolved(vertical.Output, EdgeKind.Margin));
        }

        [TestMethod]
        public void SetMarginLRTB_OffsetsNode()
        {
            var margin = new SetMarginLRTBNode(null!);
            margin.SetInput(S(Size(10, 10)));
            margin.SetMarginLRTB(5, 0, 7, 0);

            var layouts = LayOut(Size(100, 100), margin.Output);

            Assert.AreEqual(5f, layouts[0].X);
            Assert.AreEqual(7f, layouts[0].Y);
        }

        [TestMethod]
        public void SetBorder_All_ResolvesAllEdges()
        {
            var border = new SetBorderNode(null!);
            border.SetBorder(2);

            CollectionAssert.AreEqual(new[] { 2f, 2f, 2f, 2f }, Resolved(border.Output, EdgeKind.Border));
        }

        [TestMethod]
        public void SetBorder_Top_WritesOnlyTop()
        {
            var border = new SetBorderNode(null!);
            border.SetBorder(3, YGEdge.Top);

            CollectionAssert.AreEqual(new[] { 0f, 0f, 3f, 0f }, Resolved(border.Output, EdgeKind.Border));
        }

        [TestMethod]
        public void SetBorderUniform_AfterSetBorderTop_Wins()
        {
            var top = new SetBorderNode(null!);
            top.SetBorder(3, YGEdge.Top);
            var uniform = new SetBorderUniformNode(null!);
            uniform.SetInput(S(top.Output));
            uniform.SetBorderUniform(1);

            CollectionAssert.AreEqual(new[] { 1f, 1f, 1f, 1f }, Resolved(uniform.Output, EdgeKind.Border));
        }

        [TestMethod]
        public void SetBorderVertical_AfterSetBorder_WinsPerEdge()
        {
            var border = new SetBorderNode(null!);
            border.SetBorder(2);
            var vertical = new SetBorderVerticalNode(null!);
            vertical.SetInput(S(border.Output));
            vertical.SetBorderVertical(4);

            CollectionAssert.AreEqual(new[] { 2f, 2f, 4f, 4f }, Resolved(vertical.Output, EdgeKind.Border));
        }

        [TestMethod]
        public void SetBorderLRTB_NegativeEdge_IgnoresOnlyThatEdge()
        {
            var border = new SetBorderLRTBNode(null!);

            border.SetBorderLRTB(1, -1, 2, 3);

            Assert.AreEqual(new BorderLRTBStyle(S(), 1, float.NaN, 2, 3), border.Output);
        }

        [TestMethod]
        public void SetMinWidth_WritesOnlyItsSlot()
        {
            using var node = new Node();
            var minWidth = new SetMinWidthNode(null!);
            minWidth.SetMinWidth(10);

            node.SetStyle(minWidth.Output);

            Assert.AreEqual(YGValue.Point(10), node.Native->GetMinWidth());
            Assert.AreEqual(YGValue.Auto, node.Native->GetWidth());
            Assert.AreEqual(YGValue.Undefined, node.Native->GetMaxWidth());
        }

        [TestMethod]
        public void SetMinWidthRatio_AfterSetMinWidth_Wins()
        {
            using var node = new Node();
            var minWidth = new SetMinWidthNode(null!);
            minWidth.SetMinWidth(10);
            var ratio = new SetMinWidthRatioNode(null!);
            ratio.SetInput(S(minWidth.Output));
            ratio.SetMinWidthRatio(0.5f);

            node.SetStyle(ratio.Output);

            Assert.AreEqual(YGValue.Percent(50), node.Native->GetMinWidth());
        }

        [TestMethod]
        public void SetMaxWidth_WritesSlot()
        {
            using var node = new Node();
            var maxWidth = new SetMaxWidthNode(null!);
            maxWidth.SetMaxWidth(10);

            node.SetStyle(maxWidth.Output);

            Assert.AreEqual(YGValue.Point(10), node.Native->GetMaxWidth());
        }

        [TestMethod]
        public void SetMaxWidthRatio_CapsWidth()
        {
            var ratio = new SetMaxWidthRatioNode(null!);
            ratio.SetInput(S(Width(100).Output));
            ratio.SetMaxWidthRatio(0.5f);

            var layouts = LayOut(Size(100, 100), ratio.Output);

            Assert.AreEqual(50f, layouts[0].Width);
        }

        [TestMethod]
        public void SetMinAndMaxHeight_BoundHeight()
        {
            var alignItems = new SetAlignItemsNode(null!);
            alignItems.SetInput(S(Size(100, 100)));
            alignItems.SetAlignItems(YGAlign.FlexStart);
            var minHeight = new SetMinHeightNode(null!);
            minHeight.SetInput(S(Height(10)));
            minHeight.SetMinHeight(30);
            var maxHeight = new SetMaxHeightNode(null!);
            maxHeight.SetInput(S(Height(80)));
            maxHeight.SetMaxHeight(50);

            var layouts = LayOut(alignItems.Output, minHeight.Output, maxHeight.Output);

            Assert.AreEqual(30f, layouts[0].Height);
            Assert.AreEqual(50f, layouts[1].Height);
        }

        [TestMethod]
        public void MinWidthStyle_Auto_IsSkipped()
        {
            using var node = new Node();

            node.SetStyle(new MinWidthStyle(S(), YGValue.Auto));

            Assert.AreEqual(YGValue.Undefined, node.Native->GetMinWidth());
        }

        [TestMethod]
        public void SetAspectRatio_DerivesHeightFromWidth()
        {
            var alignItems = new SetAlignItemsNode(null!);
            alignItems.SetInput(S(Size(200, 200)));
            alignItems.SetAlignItems(YGAlign.FlexStart);
            var aspectRatio = new SetAspectRatioNode(null!);
            aspectRatio.SetInput(S(Width(100).Output));
            aspectRatio.SetAspectRatio(2);

            var layouts = LayOut(alignItems.Output, aspectRatio.Output);

            Assert.AreEqual(100f, layouts[0].Width);
            Assert.AreEqual(50f, layouts[0].Height);
        }

        [TestMethod]
        public void SetAspectRatio_ZeroOrNegative_IsIgnored()
        {
            var zero = new SetAspectRatioNode(null!);
            var negative = new SetAspectRatioNode(null!);

            zero.SetAspectRatio(0);
            negative.SetAspectRatio(-1);

            Assert.IsNull(zero.Output);
            Assert.IsNull(negative.Output);
        }

        [TestMethod]
        public void SetAspectRatioVector2_IsWidthOverHeight()
        {
            var aspectRatio = new SetAspectRatioVector2Node(null!);

            aspectRatio.SetAspectRatioVector2(new Stride.Core.Mathematics.Vector2(16, 9));

            Assert.AreEqual(new AspectRatioStyle(S(), 16f / 9f), aspectRatio.Output);
        }

        [TestMethod]
        public void SetAspectRatioVector2_ProportionalValue_KeepsOutput()
        {
            var aspectRatio = new SetAspectRatioVector2Node(null!);
            aspectRatio.SetAspectRatioVector2(new Stride.Core.Mathematics.Vector2(2, 1));
            var output = aspectRatio.Output;

            aspectRatio.SetAspectRatioVector2(new Stride.Core.Mathematics.Vector2(4, 2));

            Assert.AreSame(output, aspectRatio.Output);
        }

        [TestMethod]
        public void SetAspectRatioVector2_NonPositiveComponent_IsIgnored()
        {
            var zero = new SetAspectRatioVector2Node(null!);
            var negative = new SetAspectRatioVector2Node(null!);

            zero.SetAspectRatioVector2(new Stride.Core.Mathematics.Vector2(16, 0));
            negative.SetAspectRatioVector2(new Stride.Core.Mathematics.Vector2(-16, -9));

            Assert.IsNull(zero.Output);
            Assert.IsNull(negative.Output);
        }

        [TestMethod]
        public void SetAspectRatioVector2_Invalid_DropsEarlierValue()
        {
            var ten = Width(10).Output;
            var aspectRatio = new SetAspectRatioVector2Node(null!);
            aspectRatio.SetInput(S(ten));
            aspectRatio.SetAspectRatioVector2(new Stride.Core.Mathematics.Vector2(2, 1));

            aspectRatio.SetAspectRatioVector2(new Stride.Core.Mathematics.Vector2(float.NaN, 1));

            Assert.AreSame(ten, aspectRatio.Output);
        }

        [TestMethod]
        public void SetAspectRatioInt2_DerivesHeightFromWidth()
        {
            var alignItems = new SetAlignItemsNode(null!);
            alignItems.SetInput(S(Size(200, 200)));
            alignItems.SetAlignItems(YGAlign.FlexStart);
            var aspectRatio = new SetAspectRatioInt2Node(null!);
            aspectRatio.SetInput(S(Width(100).Output));
            aspectRatio.SetAspectRatioInt2(new Stride.Core.Mathematics.Int2(2, 1));

            var layouts = LayOut(alignItems.Output, aspectRatio.Output);

            Assert.AreEqual(50f, layouts[0].Height);
        }

        [TestMethod]
        public void SetAspectRatioInt2_NonPositiveComponent_IsIgnored()
        {
            var aspectRatio = new SetAspectRatioInt2Node(null!);

            aspectRatio.SetAspectRatioInt2(new Stride.Core.Mathematics.Int2(0, 9));

            Assert.IsNull(aspectRatio.Output);
        }

        [TestMethod]
        public void Combine_PaddingMarginBorder_AllApply()
        {
            var padding = new SetPaddingNode(null!);
            padding.SetPadding(1);
            var margin = new SetMarginNode(null!);
            margin.SetMargin(2);
            var border = new SetBorderNode(null!);
            border.SetBorder(3);

            var combined = Combine(padding.Output, margin.Output, border.Output);

            Assert.AreEqual(1f, Resolved(combined, EdgeKind.Padding)[2]);
            Assert.AreEqual(2f, Resolved(combined, EdgeKind.Margin)[2]);
            Assert.AreEqual(3f, Resolved(combined, EdgeKind.Border)[2]);
        }
    }
}
