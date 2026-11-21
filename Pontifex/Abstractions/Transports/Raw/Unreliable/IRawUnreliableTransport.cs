namespace Pontifex.Raw.Unreliable
{
    /// <summary>
    /// Transport-level contract of RawUnreliable: the carrier lifecycle owner and the factory and
    /// registry of the endpoints that own their own lifecycle.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Start</b>: <c>Start(onStopped)</c> succeeds once the carrier has started and, for a
    /// client, its endpoint has started. A client creates and starts its single endpoint during that start; a
    /// server becomes able to receive at its configured address. Neither implies peer reachability or an
    /// established session.</description></item>
    /// <item><description><b>Asynchronous start failures</b>: a carrier or endpoint-start failure that is only
    /// detected after <c>Start</c> returned true, for example a UDP client bind failure, is reported as an
    /// ordinary stop through <c>onStopped</c>, never as a failed <c>Start</c>. A failure detected while the
    /// carrier itself starts, as when a server binds its listening socket, fails <c>Start</c>
    /// instead.</description></item>
    /// <item><description><b>Teardown ordering</b>: transport teardown invalidates its endpoints and schedules
    /// the endpoint handlers' <c>OnStopped</c> for every handler whose <c>OnStarted</c> completed successfully
    /// before the transport's own <c>onStopped</c> callback begins.</description></item>
    /// <item><description><b>Dropped packets</b>: a packet dropped by admission or by a carrier queue is never
    /// logged; that silence is by design. Carrier exception logging is unaffected: a carrier may still log a
    /// transport error such as a short send.</description></item>
    /// </list>
    /// </remarks>
    public interface IRawUnreliableTransport : IRawTransport
    {
    }
}
