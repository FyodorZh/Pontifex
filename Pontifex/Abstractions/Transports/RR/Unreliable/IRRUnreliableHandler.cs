namespace Pontifex.RR.Unreliable
{
    /// <summary>
    /// Common callbacks of an RR unreliable transport handler.
    /// </summary>
    public interface IRRUnreliableHandler : IHandler
    {
        /// <summary>
        /// Called once when the transport and its carrier are ready.
        /// </summary>
        void Started();

        /// <summary>
        /// Called once when the transport stops.
        /// </summary>
        void Stopped();
    }
}
