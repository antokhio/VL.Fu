using VL.Core.Import;
using VL.Fu.Core;
using YogaSharp;

namespace VL.Fu.Styles
{
    // ---------------------------------------------------------
    // LAYOUT & ALIGNMENT
    // ---------------------------------------------------------

    [ProcessNode(Name = "SetDirection")]
    public class StyleSetDirection : StyleNode
    {
        private YGDirection _direction = YGDirection.Inherit;

        protected override unsafe void Apply(IStylable node) =>
            node.Handle->SetDirection(_direction);

        public void SetDirection(YGDirection direction = YGDirection.Inherit) =>
            SetValue(ref _direction, direction);
    }

    [ProcessNode(Name = "SetFlexDirection")]
    public class StyleSetFlexDirection : StyleNode
    {
        private YGFlexDirection _flexDirection = YGFlexDirection.Column;

        protected override unsafe void Apply(IStylable node) =>
            node.Handle->SetFlexDirection(_flexDirection);

        public void SetFlexDirection(YGFlexDirection flexDirection = YGFlexDirection.Column) =>
            SetValue(ref _flexDirection, flexDirection);
    }

    [ProcessNode(Name = "SetJustifyContent")]
    public class StyleSetJustifyContent : StyleNode
    {
        private YGJustify _justify = YGJustify.FlexStart;

        protected override unsafe void Apply(IStylable node) =>
            node.Handle->SetJustifyContent(_justify);

        public void SetJustifyContent(YGJustify justify = YGJustify.FlexStart) =>
            SetValue(ref _justify, justify);
    }

    [ProcessNode(Name = "SetAlignContent")]
    public class StyleSetAlignContent : StyleNode
    {
        private YGAlign _alignContent = YGAlign.FlexStart;

        protected override unsafe void Apply(IStylable node) =>
            node.Handle->SetAlignContent(_alignContent);

        public void SetAlignContent(YGAlign alignContent = YGAlign.FlexStart) =>
            SetValue(ref _alignContent, alignContent);
    }

    [ProcessNode(Name = "SetAlignItems")]
    public class StyleSetAlignItems : StyleNode
    {
        private YGAlign _alignItems = YGAlign.Stretch;

        protected override unsafe void Apply(IStylable node) =>
            node.Handle->SetAlignItems(_alignItems);

        public void SetAlignItems(YGAlign alignItems = YGAlign.Stretch) =>
            SetValue(ref _alignItems, alignItems);
    }

    [ProcessNode(Name = "SetAlignSelf")]
    public class StyleSetAlignSelf : StyleNode
    {
        private YGAlign _alignSelf = YGAlign.Auto;

        protected override unsafe void Apply(IStylable node) =>
            node.Handle->SetAlignSelf(_alignSelf);

        public void SetAlignSelf(YGAlign alignSelf = YGAlign.Auto) =>
            SetValue(ref _alignSelf, alignSelf);
    }

    [ProcessNode(Name = "SetPositionType")]
    public class StyleSetPositionType : StyleNode
    {
        private YGPositionType _positionType = YGPositionType.Relative;

        protected override unsafe void Apply(IStylable node) =>
            node.Handle->SetPositionType(_positionType);

        public void SetPositionType(YGPositionType positionType = YGPositionType.Relative) =>
            SetValue(ref _positionType, positionType);
    }

    [ProcessNode(Name = "SetFlexWrap")]
    public class StyleSetFlexWrap : StyleNode
    {
        private YGWrap _flexWrap = YGWrap.NoWrap;

        protected override unsafe void Apply(IStylable node) => node.Handle->SetFlexWrap(_flexWrap);

        public void SetFlexWrap(YGWrap flexWrap = YGWrap.NoWrap) =>
            SetValue(ref _flexWrap, flexWrap);
    }

    [ProcessNode(Name = "SetOverflow")]
    public class StyleSetOverflow : StyleNode
    {
        private YGOverflow _overflow = YGOverflow.Visible;

        protected override unsafe void Apply(IStylable node) => node.Handle->SetOverflow(_overflow);

        public void SetOverflow(YGOverflow overflow = YGOverflow.Visible) =>
            SetValue(ref _overflow, overflow);
    }

    [ProcessNode(Name = "SetDisplay")]
    public class StyleSetDisplay : StyleNode
    {
        private YGDisplay _display = YGDisplay.Flex;

        protected override unsafe void Apply(IStylable node) => node.Handle->SetDisplay(_display);

        public void SetDisplay(YGDisplay display = YGDisplay.Flex) =>
            SetValue(ref _display, display);
    }

    // ---------------------------------------------------------
    // FLEX FACTORS
    // ---------------------------------------------------------

    [ProcessNode(Name = "SetFlex")]
    public class StyleSetFlex : StyleNode
    {
        private float _flex = float.NaN;

        protected override unsafe void Apply(IStylable node) => node.Handle->SetFlex(_flex);

        public void SetFlex(float flex = float.NaN) => SetValue(ref _flex, flex);
    }

    [ProcessNode(Name = "SetFlexGrow")]
    public class StyleSetFlexGrow : StyleNode
    {
        private float _flexGrow = 0f;

        protected override unsafe void Apply(IStylable node) => node.Handle->SetFlexGrow(_flexGrow);

        public void SetFlexGrow(float flexGrow = 0f) => SetValue(ref _flexGrow, flexGrow);
    }

    [ProcessNode(Name = "SetFlexShrink")]
    public class StyleSetFlexShrink : StyleNode
    {
        private float _flexShrink = 1f; // Yoga default is usually 1

        protected override unsafe void Apply(IStylable node) =>
            node.Handle->SetFlexShrink(_flexShrink);

        public void SetFlexShrink(float flexShrink = 1f) => SetValue(ref _flexShrink, flexShrink);
    }

    [ProcessNode(Name = "SetFlexBasis")]
    public class StyleSetFlexBasis : StyleNode
    {
        private float _flexBasis = float.NaN;

        protected override unsafe void Apply(IStylable node) =>
            node.Handle->SetFlexBasis(_flexBasis);

        public void SetFlexBasis(float flexBasis = float.NaN) =>
            SetValue(ref _flexBasis, flexBasis);
    }

    [ProcessNode(Name = "SetFlexBasisPercent")]
    public class StyleSetFlexBasisPercent : StyleNode
    {
        private float _flexBasisPercent = float.NaN;

        protected override unsafe void Apply(IStylable node) =>
            node.Handle->SetFlexBasisPercent(_flexBasisPercent);

        public void SetFlexBasisPercent(float flexBasisPercent = float.NaN) =>
            SetValue(ref _flexBasisPercent, flexBasisPercent);
    }

    // Parameterless flex basis variants
    [ProcessNode(Name = "SetFlexBasisAuto")]
    public class StyleSetFlexBasisAuto : StyleNode
    {
        protected override unsafe void Apply(IStylable node) => node.Handle->SetFlexBasisAuto();
    }

    [ProcessNode(Name = "SetFlexBasisMaxContent")]
    public class StyleSetFlexBasisMaxContent : StyleNode
    {
        protected override unsafe void Apply(IStylable node) =>
            node.Handle->SetFlexBasisMaxContent();
    }

    [ProcessNode(Name = "SetFlexBasisFitContent")]
    public class StyleSetFlexBasisFitContent : StyleNode
    {
        protected override unsafe void Apply(IStylable node) =>
            node.Handle->SetFlexBasisFitContent();
    }

    [ProcessNode(Name = "SetFlexBasisStretch")]
    public class StyleSetFlexBasisStretch : StyleNode
    {
        protected override unsafe void Apply(IStylable node) => node.Handle->SetFlexBasisStretch();
    }

    // ---------------------------------------------------------
    // POSITION, MARGIN, PADDING, BORDER, GAP
    // ---------------------------------------------------------

    [ProcessNode(Name = "SetPosition")]
    public class StyleSetPosition : StyleNode
    {
        private YGEdge _edge = YGEdge.All;
        private float _value = float.NaN;

        protected override unsafe void Apply(IStylable node) =>
            node.Handle->SetPosition(_edge, _value);

        public void SetEdge(YGEdge edge = YGEdge.All) => SetValue(ref _edge, edge);

        public void SetValue(float value = float.NaN) => SetValue(ref _value, value);
    }

    [ProcessNode(Name = "SetPositionPercent")]
    public class StyleSetPositionPercent : StyleNode
    {
        private YGEdge _edge = YGEdge.All;
        private float _percent = float.NaN;

        protected override unsafe void Apply(IStylable node) =>
            node.Handle->SetPositionPercent(_edge, _percent);

        public void SetEdge(YGEdge edge = YGEdge.All) => SetValue(ref _edge, edge);

        public void SetPercent(float percent = float.NaN) => SetValue(ref _percent, percent);
    }

    [ProcessNode(Name = "SetMargin")]
    public class StyleSetMargin : StyleNode
    {
        private YGEdge _edge = YGEdge.All;
        private float _value = float.NaN;

        protected override unsafe void Apply(IStylable node) =>
            node.Handle->SetMargin(_edge, _value);

        public void SetEdge(YGEdge edge = YGEdge.All) => SetValue(ref _edge, edge);

        public void SetValue(float value = float.NaN) => SetValue(ref _value, value);
    }

    [ProcessNode(Name = "SetMarginPercent")]
    public class StyleSetMarginPercent : StyleNode
    {
        private YGEdge _edge = YGEdge.All;
        private float _percent = float.NaN;

        protected override unsafe void Apply(IStylable node) =>
            node.Handle->SetMarginPercent(_edge, _percent);

        public void SetEdge(YGEdge edge = YGEdge.All) => SetValue(ref _edge, edge);

        public void SetPercent(float percent = float.NaN) => SetValue(ref _percent, percent);
    }

    [ProcessNode(Name = "SetMarginAuto")]
    public class StyleSetMarginAuto : StyleNode
    {
        private YGEdge _edge = YGEdge.All;

        protected override unsafe void Apply(IStylable node) => node.Handle->SetMarginAuto(_edge);

        public void SetEdge(YGEdge edge = YGEdge.All) => SetValue(ref _edge, edge);
    }

    [ProcessNode(Name = "SetPadding")]
    public class StyleSetPadding : StyleNode
    {
        private YGEdge _edge = YGEdge.All;
        private float _value = float.NaN;

        protected override unsafe void Apply(IStylable node) =>
            node.Handle->SetPadding(_edge, _value);

        public void SetEdge(YGEdge edge = YGEdge.All) => SetValue(ref _edge, edge);

        public void SetValue(float value = float.NaN) => SetValue(ref _value, value);
    }

    [ProcessNode(Name = "SetPaddingPercent")]
    public class StyleSetPaddingPercent : StyleNode
    {
        private YGEdge _edge = YGEdge.All;
        private float _percent = float.NaN;

        protected override unsafe void Apply(IStylable node) =>
            node.Handle->SetPaddingPercent(_edge, _percent);

        public void SetEdge(YGEdge edge = YGEdge.All) => SetValue(ref _edge, edge);

        public void SetPercent(float percent = float.NaN) => SetValue(ref _percent, percent);
    }

    [ProcessNode(Name = "SetBorder")]
    public class StyleSetBorder : StyleNode
    {
        private YGEdge _edge = YGEdge.All;
        private float _value = float.NaN;

        protected override unsafe void Apply(IStylable node) =>
            node.Handle->SetBorder(_edge, _value);

        public void SetEdge(YGEdge edge = YGEdge.All) => SetValue(ref _edge, edge);

        public void SetValue(float value = float.NaN) => SetValue(ref _value, value);
    }

    [ProcessNode(Name = "SetGap")]
    public class StyleSetGap : StyleNode
    {
        private YGGutter _gutter = YGGutter.All;
        private float _gapLength = float.NaN;

        protected override unsafe void Apply(IStylable node) =>
            node.Handle->SetGap(_gutter, _gapLength);

        public void SetGutter(YGGutter gutter = YGGutter.All) => SetValue(ref _gutter, gutter);

        public void SetGapLength(float gapLength = float.NaN) =>
            SetValue(ref _gapLength, gapLength);
    }

    [ProcessNode(Name = "SetAspectRatio")]
    public class StyleSetAspectRatio : StyleNode
    {
        private float _aspectRatio = float.NaN;

        protected override unsafe void Apply(IStylable node) =>
            node.Handle->SetAspectRatio(_aspectRatio);

        public void SetAspectRatio(float aspectRatio = float.NaN) =>
            SetValue(ref _aspectRatio, aspectRatio);
    }

    // ---------------------------------------------------------
    // DIMENSIONS (Width, Height)
    // ---------------------------------------------------------

    [ProcessNode(Name = "SetWidth")]
    public class StyleSetWidth : StyleNode
    {
        private float _width = float.NaN;

        protected override unsafe void Apply(IStylable node) => node.Handle->SetWidth(_width);

        public void SetWidth(float width = float.NaN) => SetValue(ref _width, width);
    }

    [ProcessNode(Name = "SetWidthPercent")]
    public class StyleSetWidthPercent : StyleNode
    {
        private float _widthPercent = float.NaN;

        protected override unsafe void Apply(IStylable node) =>
            node.Handle->SetWidthPercent(_widthPercent);

        public void SetWidthPercent(float widthPercent = float.NaN) =>
            SetValue(ref _widthPercent, widthPercent);
    }

    [ProcessNode(Name = "SetHeight")]
    public class StyleSetHeight : StyleNode
    {
        private float _height = float.NaN;

        protected override unsafe void Apply(IStylable node) => node.Handle->SetHeight(_height);

        public void SetHeight(float height = float.NaN) => SetValue(ref _height, height);
    }

    [ProcessNode(Name = "SetHeightPercent")]
    public class StyleSetHeightPercent : StyleNode
    {
        private float _heightPercent = float.NaN;

        protected override unsafe void Apply(IStylable node) =>
            node.Handle->SetHeightPercent(_heightPercent);

        public void SetHeightPercent(float heightPercent = float.NaN) =>
            SetValue(ref _heightPercent, heightPercent);
    }

    // Parameterless Dimension Variants
    [ProcessNode(Name = "SetWidthAuto")]
    public class StyleSetWidthAuto : StyleNode
    {
        protected override unsafe void Apply(IStylable node) => node.Handle->SetWidthAuto();
    }

    [ProcessNode(Name = "SetWidthMaxContent")]
    public class StyleSetWidthMaxContent : StyleNode
    {
        protected override unsafe void Apply(IStylable node) => node.Handle->SetWidthMaxContent();
    }

    [ProcessNode(Name = "SetWidthFitContent")]
    public class StyleSetWidthFitContent : StyleNode
    {
        protected override unsafe void Apply(IStylable node) => node.Handle->SetWidthFitContent();
    }

    [ProcessNode(Name = "SetWidthStretch")]
    public class StyleSetWidthStretch : StyleNode
    {
        protected override unsafe void Apply(IStylable node) => node.Handle->SetWidthStretch();
    }

    [ProcessNode(Name = "SetHeightAuto")]
    public class StyleSetHeightAuto : StyleNode
    {
        protected override unsafe void Apply(IStylable node) => node.Handle->SetHeightAuto();
    }

    [ProcessNode(Name = "SetHeightMaxContent")]
    public class StyleSetHeightMaxContent : StyleNode
    {
        protected override unsafe void Apply(IStylable node) => node.Handle->SetHeightMaxContent();
    }

    [ProcessNode(Name = "SetHeightFitContent")]
    public class StyleSetHeightFitContent : StyleNode
    {
        protected override unsafe void Apply(IStylable node) => node.Handle->SetHeightFitContent();
    }

    [ProcessNode(Name = "SetHeightStretch")]
    public class StyleSetHeightStretch : StyleNode
    {
        protected override unsafe void Apply(IStylable node) => node.Handle->SetHeightStretch();
    }

    // ---------------------------------------------------------
    // MIN/MAX DIMENSIONS
    // ---------------------------------------------------------

    [ProcessNode(Name = "SetMinWidth")]
    public class StyleSetMinWidth : StyleNode
    {
        private float _minWidth = float.NaN;

        protected override unsafe void Apply(IStylable node) => node.Handle->SetMinWidth(_minWidth);

        public void SetMinWidth(float minWidth = float.NaN) => SetValue(ref _minWidth, minWidth);
    }

    [ProcessNode(Name = "SetMinWidthPercent")]
    public class StyleSetMinWidthPercent : StyleNode
    {
        private float _minWidthPercent = float.NaN;

        protected override unsafe void Apply(IStylable node) =>
            node.Handle->SetMinWidthPercent(_minWidthPercent);

        public void SetMinWidthPercent(float minWidthPercent = float.NaN) =>
            SetValue(ref _minWidthPercent, minWidthPercent);
    }

    [ProcessNode(Name = "SetMinHeight")]
    public class StyleSetMinHeight : StyleNode
    {
        private float _minHeight = float.NaN;

        protected override unsafe void Apply(IStylable node) =>
            node.Handle->SetMinHeight(_minHeight);

        public void SetMinHeight(float minHeight = float.NaN) =>
            SetValue(ref _minHeight, minHeight);
    }

    [ProcessNode(Name = "SetMinHeightPercent")]
    public class StyleSetMinHeightPercent : StyleNode
    {
        private float _minHeightPercent = float.NaN;

        protected override unsafe void Apply(IStylable node) =>
            node.Handle->SetMinHeightPercent(_minHeightPercent);

        public void SetMinHeightPercent(float minHeightPercent = float.NaN) =>
            SetValue(ref _minHeightPercent, minHeightPercent);
    }

    [ProcessNode(Name = "SetMaxWidth")]
    public class StyleSetMaxWidth : StyleNode
    {
        private float _maxWidth = float.NaN;

        protected override unsafe void Apply(IStylable node) => node.Handle->SetMaxWidth(_maxWidth);

        public void SetMaxWidth(float maxWidth = float.NaN) => SetValue(ref _maxWidth, maxWidth);
    }

    [ProcessNode(Name = "SetMaxWidthPercent")]
    public class StyleSetMaxWidthPercent : StyleNode
    {
        private float _maxWidthPercent = float.NaN;

        protected override unsafe void Apply(IStylable node) =>
            node.Handle->SetMaxWidthPercent(_maxWidthPercent);

        public void SetMaxWidthPercent(float maxWidthPercent = float.NaN) =>
            SetValue(ref _maxWidthPercent, maxWidthPercent);
    }

    [ProcessNode(Name = "SetMaxHeight")]
    public class StyleSetMaxHeight : StyleNode
    {
        private float _maxHeight = float.NaN;

        protected override unsafe void Apply(IStylable node) =>
            node.Handle->SetMaxHeight(_maxHeight);

        public void SetMaxHeight(float maxHeight = float.NaN) =>
            SetValue(ref _maxHeight, maxHeight);
    }

    [ProcessNode(Name = "SetMaxHeightPercent")]
    public class StyleSetMaxHeightPercent : StyleNode
    {
        private float _maxHeightPercent = float.NaN;

        protected override unsafe void Apply(IStylable node) =>
            node.Handle->SetMaxHeightPercent(_maxHeightPercent);

        public void SetMaxHeightPercent(float maxHeightPercent = float.NaN) =>
            SetValue(ref _maxHeightPercent, maxHeightPercent);
    }

    // Parameterless Min/Max Variants
    [ProcessNode(Name = "SetMinWidthMaxContent")]
    public class StyleSetMinWidthMaxContent : StyleNode
    {
        protected override unsafe void Apply(IStylable node) =>
            node.Handle->SetMinWidthMaxContent();
    }

    [ProcessNode(Name = "SetMinWidthFitContent")]
    public class StyleSetMinWidthFitContent : StyleNode
    {
        protected override unsafe void Apply(IStylable node) =>
            node.Handle->SetMinWidthFitContent();
    }

    [ProcessNode(Name = "SetMinWidthStretch")]
    public class StyleSetMinWidthStretch : StyleNode
    {
        protected override unsafe void Apply(IStylable node) => node.Handle->SetMinWidthStretch();
    }

    [ProcessNode(Name = "SetMinHeightMaxContent")]
    public class StyleSetMinHeightMaxContent : StyleNode
    {
        protected override unsafe void Apply(IStylable node) =>
            node.Handle->SetMinHeightMaxContent();
    }

    [ProcessNode(Name = "SetMinHeightFitContent")]
    public class StyleSetMinHeightFitContent : StyleNode
    {
        protected override unsafe void Apply(IStylable node) =>
            node.Handle->SetMinHeightFitContent();
    }

    [ProcessNode(Name = "SetMinHeightStretch")]
    public class StyleSetMinHeightStretch : StyleNode
    {
        protected override unsafe void Apply(IStylable node) => node.Handle->SetMinHeightStretch();
    }

    [ProcessNode(Name = "SetMaxWidthMaxContent")]
    public class StyleSetMaxWidthMaxContent : StyleNode
    {
        protected override unsafe void Apply(IStylable node) =>
            node.Handle->SetMaxWidthMaxContent();
    }

    [ProcessNode(Name = "SetMaxWidthFitContent")]
    public class StyleSetMaxWidthFitContent : StyleNode
    {
        protected override unsafe void Apply(IStylable node) =>
            node.Handle->SetMaxWidthFitContent();
    }

    [ProcessNode(Name = "SetMaxWidthStretch")]
    public class StyleSetMaxWidthStretch : StyleNode
    {
        protected override unsafe void Apply(IStylable node) => node.Handle->SetMaxWidthStretch();
    }

    [ProcessNode(Name = "SetMaxHeightMaxContent")]
    public class StyleSetMaxHeightMaxContent : StyleNode
    {
        protected override unsafe void Apply(IStylable node) =>
            node.Handle->SetMaxHeightMaxContent();
    }

    [ProcessNode(Name = "SetMaxHeightFitContent")]
    public class StyleSetMaxHeightFitContent : StyleNode
    {
        protected override unsafe void Apply(IStylable node) =>
            node.Handle->SetMaxHeightFitContent();
    }

    [ProcessNode(Name = "SetMaxHeightStretch")]
    public class StyleSetMaxHeightStretch : StyleNode
    {
        protected override unsafe void Apply(IStylable node) => node.Handle->SetMaxHeightStretch();
    }
}
