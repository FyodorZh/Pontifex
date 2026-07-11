namespace Pontifex.Factory
{
    public class VoidElement : BaseElement
    {
        public static readonly VoidElement Instance = new VoidElement();
        
        public override ElementType Type => ElementType.Void;

        public override string ToString() => "";
    }
}