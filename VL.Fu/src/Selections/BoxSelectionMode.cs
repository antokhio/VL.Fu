using Stride.Core.Mathematics;
using VL.Core.Import;
using VL.Fu.Core;
using VL.Fu.Core.Selections;

namespace VL.Fu.Selections
{
    [ProcessNode(Name = "BoxSelection", FragmentSelection = FragmentSelection.Explicit)]
    public class BoxSelectionMode : ISelectionMode
    {
        private Vector2 _startPosition;
        public bool IsActive { get; private set; }
        public RectangleF SelectionRectangle { get; private set; }

        [Fragment]
        public BoxSelectionMode() { }

        public void Start(Vector2 startPosition, IReadOnlyList<FuKey> modifiers)
        {
            IsActive = true;
            _startPosition = startPosition;
            SelectionRectangle = new RectangleF(startPosition.X, startPosition.Y, 0, 0);
        }

        public void UpdateSelection(Vector2 currentPosition, IReadOnlyList<FuKey> modifiers)
        {
            if (!IsActive)
                return;

            var left = Math.Min(_startPosition.X, currentPosition.X);
            var top = Math.Min(_startPosition.Y, currentPosition.Y);
            var right = Math.Max(_startPosition.X, currentPosition.X);
            var bottom = Math.Max(_startPosition.Y, currentPosition.Y);

            SelectionRectangle = new RectangleF(left, top, right - left, bottom - top);
        }

        public void End()
        {
            IsActive = false;
        }

        public void Reset()
        {
            IsActive = false;
            SelectionRectangle = RectangleF.Empty;
        }

        [Fragment]
        public ISelectionMode Output => this;
    }
}
