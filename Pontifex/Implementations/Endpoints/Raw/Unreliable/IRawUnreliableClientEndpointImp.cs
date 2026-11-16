namespace Pontifex.Raw.Unreliable
{
    /// <summary>
    /// Implementation marker for an unreliable client endpoint: an unreliable endpoint that owns the client
    /// side of a carrier and is attached to exactly one client transport.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: Internal on purpose. There is no public client marker interface
    /// in the unreliable contract; the transport implementations use this marker to distinguish the client
    /// branch from the server branch. It adds no member to
    /// <see cref="IRawUnreliableEndpointImp"/>.</description></item>
    /// </list>
    /// </remarks>
    internal interface IRawUnreliableClientEndpointImp : IRawUnreliableEndpointImp
    {
    }
}
