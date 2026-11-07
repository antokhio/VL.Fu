namespace Fu.Core.Styling
{
    public interface IStyle
    {
        void Apply();
        void Remove();
    }

    public abstract class Style : IStyle
    {
        public void Apply()
        {
            throw new NotImplementedException();
        }

        public void Remove()
        {
            throw new NotImplementedException();
        }
    }
}
