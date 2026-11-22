namespace Pontifex.Raw.Unreliable
{
    /// <summary>
    /// Carrier-implemented debug switch that enables the transport-wide reliable mode used by the
    /// conformance suite of the unreliable transports.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Thread safety</b>: Implementations must be safe to call before the transport
    /// starts and must not throw.</description></item>
    /// <item><description><b>Preconditions/Postconditions</b>: The mode must be requested before the
    /// transport starts; an implementation reports <see langword="false"/> when it cannot provide the
    /// mode at the time of the call.</description></item>
    /// <item><description><b>Other notes</b>: The conformance control of an unreliable transport resolves
    /// this interface on the transport instance and forwards <c>TryMakeReliable</c> to it. The interface
    /// lets every unreliable carrier transport keep one implementation.</description></item>
    /// </list>
    /// </remarks>
    public interface IRawUnreliableDebugReliability
    {
        /// <summary>
        /// Enables transport-wide reliable debug mode for every current and future route of the
        /// transport.
        /// </summary>
        /// <returns><see langword="true"/> when the mode was enabled; otherwise
        /// <see langword="false"/>.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call from any thread.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: Must be called before the transport
        /// starts. The setting is transport-wide and applies to routes created later.</description></item>
        /// </list>
        /// </remarks>
        bool TryMakeReliableForDebug();
    }
}
