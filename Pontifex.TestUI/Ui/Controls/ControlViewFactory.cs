namespace Pontifex.Test
{
    public static class ControlViewFactory
    {
        public static ControlView Construct(IControl control)
        {
            return control switch
            {
                IRawReliableClientControl rawReliableClientControl => new RawReliableClientControlView(rawReliableClientControl),
                _ => new UnknownControlView(control)
            };
        }
    }
}
