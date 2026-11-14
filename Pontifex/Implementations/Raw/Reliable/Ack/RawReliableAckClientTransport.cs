using System;
using Actuarius.Memory;
using Scriba;

namespace Pontifex.Raw.Reliable.Ack
{
    /// <summary>
    /// Base class for reliable acknowledgement client transports. Owns the carrier-independent connect
    /// lifecycle: handler validation, endpoint creation and attachment, start, and teardown.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: A concrete carrier supplies only an endpoint factory. The base
    /// starts that endpoint and stops it on teardown, preserving the endpoint's ownership of connectivity and
    /// the handler lifecycle.</description></item>
    /// </list>
    /// </remarks>
    public abstract class RawReliableAckClientTransport : RawReliableClientTransport<IRawReliableAckClientHandler>, IRawReliableAckClient
    {
        public override TransportType Type => TransportType.RawReliableAck;

        protected RawReliableAckClientTransport(string typeName, ILogger logger, IMemoryRental memory) 
            : base(typeName, logger, memory, new RawReliableConformanceControl())
        {
        }

        /// <summary>
        /// Creates the carrier-specific client endpoint for one connection attempt.
        /// </summary>
        /// <param name="handler">The initialized handler. Must be non-null.</param>
        /// <returns>A non-started endpoint that owns the connection.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Preconditions/Postconditions</b>: Called at most once per transport, from the
        /// start transition. The returned endpoint is attached and started by the base class; the
        /// implementation must not attach or start it.</description></item>
        /// <item><description><b>Other notes</b>: Throwing is allowed; the base logs the exception and fails
        /// the start.</description></item>
        /// </list>
        /// </remarks>
        protected abstract RawReliableAckClientEndpoint CreateEndpoint(IRawReliableAckClientHandler handler);

        protected sealed override bool BeginConnect()
        {
            var handler = Handler;
            if (handler == null)
            {
                Log.e("Transport handler is not set");
                return false;
            }

            try
            {
                var endpoint = CreateEndpoint(handler);
                AttachEndpoint(endpoint);

                if (!endpoint.Start())
                {
                    Log.e("Failed to start the client endpoint");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                return false;
            }
        }

        protected override void OnReadyToConnect()
        {
            // The endpoint connects asynchronously from BeginConnect; there is no additional ready step.
        }

        protected override void DestroyTransport(StopReason reason)
        {
            var endpoint = AttachedEndpoint;
            if (endpoint == null)
            {
                return;
            }

            try
            {
                endpoint.Stop(reason, false);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
            }
        }
    }
}
