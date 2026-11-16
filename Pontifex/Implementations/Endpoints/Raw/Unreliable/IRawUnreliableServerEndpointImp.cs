namespace Pontifex.Raw.Unreliable
{
    /// <summary>
    /// Implementation marker for an unreliable server endpoint: an unreliable endpoint that belongs to one
    /// accepted server session and shares its transport's outbound budget.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: Internal on purpose. There is no public server marker interface
    /// in the unreliable contract; the transport implementations use this marker to distinguish the server
    /// branch from the client branch. It adds no member to
    /// <see cref="IRawUnreliableEndpointImp"/>.</description></item>
    /// </list>
    /// </remarks>
    internal interface IRawUnreliableServerEndpointImp : IRawUnreliableEndpointImp
    {
    }
}
