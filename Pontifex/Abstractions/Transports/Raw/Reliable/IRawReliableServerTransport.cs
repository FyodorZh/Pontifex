namespace Pontifex.Raw.Reliable
{
    /// <summary>
    /// Marks a reliable raw server transport whose sessions are described by a server handler.
    /// </summary>
    /// <typeparam name="THandler">The session handler type accepted by the transport.</typeparam>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: The type parameter carries no members of its own; it lets a
    /// concrete server expose the handler contract it expects without a proxy or an adapter.</description></item>
    /// </list>
    /// </remarks>
    public interface IRawReliableServerTransport<THandler> : IRawReliableTransport
        where THandler : class, IRawReliableServerHandler { }
}
