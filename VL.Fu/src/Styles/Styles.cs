//using VL.Core.Import;
//using VL.Fu.Core;

//namespace VL.Fu.Styles
//{
//    [ProcessNode]
//    public abstract class StyleSetterBase
//    {
//        private IFuStyle? _style;

//        public virtual void SetStyle(IFuStyle style)
//        {
//            if (ReferenceEquals(_style, style))
//                return;

//            _style = style;

//            Output = new(
//                style,
//                node =>
//                {
//                    unsafe
//                    {
//                        node.Handle->SetStyle(style);
//                    }
//                }
//            );
//        }

//        public virtual IFuStyle Output { get; protected set; }
//    }

//    [ProcessNode]
//    public class SetDirection : StyleSetterBase
//    {
//        public SetDirection()
//        {
//            Output = new(
//                style,
//                node =>
//                {
//                    unsafe
//                    {
//                        node.Handle->SetDirection(direction);
//                    }
//                }
//            );
//        }
//    }
//}
