using SkiaSharp;
using Stride.Core.Mathematics;
using VL.Fu.Core.Interaction;
using VL.Lib.IO;

namespace VL.Fu.Tests.Core.Interaction.Recognizers
{
    /// <summary>
    /// A node to address samples to. Recognizers only compare it by reference.
    /// </summary>
    internal sealed class TestNode : Interactive<TestNode>
    {
        public TestNode()
            : base(null!) { }
    }

    /// <summary>
    /// Builds the samples one pointer produces on one node, in window pixels.
    /// <see cref="WindowToParent"/> and <see cref="Scaling"/> go into every sample built after they are set,
    /// so a test can move the node's parent space or the DIP scaling between samples.
    /// </summary>
    internal sealed class TestPointer(IInteractive node, int id = PointerIds.Mouse)
    {
        public IInteractive Node { get; set; } = node;

        public int Id { get; } = id;

        public SKMatrix? WindowToParent { get; set; } = SKMatrix.Identity;

        public float Scaling { get; set; } = 1f;

        public PointerSample Down(float x, float y, MouseButtons button = MouseButtons.Left) =>
            Sample(PointerSampleKind.Down, x, y, button);

        public PointerSample Move(float x, float y) => Sample(PointerSampleKind.Move, x, y, MouseButtons.None);

        public PointerSample Up(float x, float y, MouseButtons button = MouseButtons.Left) =>
            Sample(PointerSampleKind.Up, x, y, button);

        public PointerSample Wheel(float x, float y, float notches) =>
            Sample(PointerSampleKind.Wheel, x, y, MouseButtons.None) with { WheelDelta = new Vector2(0f, notches) };

        private PointerSample Sample(PointerSampleKind kind, float x, float y, MouseButtons button) =>
            new()
            {
                Kind = kind,
                PointerId = Id,
                Button = button,
                Modifiers = Keys.None,
                PositionInWindow = new Vector2(x, y),
                Node = Node,
                WindowToNode = WindowToParent,
                WindowToParent = WindowToParent,
                Scaling = Scaling,
                TimeMs = 0,
            };
    }
}
