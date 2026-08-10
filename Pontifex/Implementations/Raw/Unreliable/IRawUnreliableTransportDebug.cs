namespace Pontifex.Raw.Unreliable
{
    /// <summary>
    /// Internal seam through which the shared <see cref="RawUnreliableTransportConformanceControl"/>
    /// reaches the transport's reliable-debug-mode capability without naming a
    /// concrete client or server transport type.
    /// </summary>
    internal interface IRawUnreliableTransportDebug
    {
        bool TryMakeReliableForDebug();
    }
}
