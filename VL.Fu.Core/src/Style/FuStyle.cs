namespace VL.Fu.Core
{
    public interface IFuStyle
    {
        Action<IStylable> Apply { get; }
    }

    public record struct FuStyle : IFuStyle
    {
        public IFuStyle? Style { get; init; }
        public Action<IStylable> Apply { get; init; }

        public FuStyle(IFuStyle? style, Action<IStylable> currentAction)
        {
            Style = style;

            if (style != null)
            {
                Apply = node =>
                {
                    currentAction(node); // Apply the current style
                    style.Apply(node); // Then apply the next style in the chain
                };
            }
            else
            {
                Apply = currentAction;
            }
        }
    }
}
