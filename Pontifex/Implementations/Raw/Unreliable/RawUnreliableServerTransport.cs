using System;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Unreliable
{
    /// <summary>
    /// Base class for all RawUnreliable server transports. The generic parameter
    /// is the variant handler-factory delegate type: Ack servers supply a
    /// <see cref="Func{T1, T2, TResult}"/> receiving the triggering message,
    /// NoAck servers a <see cref="Func{T, TResult}"/> receiving only the source
    /// route. The type-safe factory invocation is the only variant difference.
    /// Carries the RawUnreliable shared machinery on the server side: inbound
    /// routing, endpoint creation, and server teardown.
    /// </summary>
    public abstract class RawUnreliableServerTransport<TFactory> : RawServerTransport, IRawUnreliableTransportDebug
        where TFactory : Delegate
    {
        protected new RawUnreliableTransportConformanceControl Conformance => (RawUnreliableTransportConformanceControl)base.Conformance;

        protected RawUnreliableServerTransport(string typeName, ILogger logger, IMemoryRental memory, RawUnreliableTransportConformanceControl? conformanceControl = null)
            : base(typeName, logger, memory, conformanceControl ?? new RawUnreliableTransportConformanceControl())
        {
        }

        protected bool TryInitializeServer(TFactory factory)
        {
            return base.TryInitializeServer(factory);
        }

        /// <summary>
        /// A server transport has no configured remote destination.
        /// </summary>
        protected virtual IEndPoint? ClientRemoteEndPoint => null;

        /// <summary>
        /// Commits an accepted message to the carrier for the given endpoint.
        /// Ownership of the message transfers to the carrier; it must release it
        /// on any non-<see cref="SendResult.Ok"/> result.
        /// </summary>
        protected abstract SendResult SendToCarrier(RawUnreliableEndpoint endpoint, UnionDataList message);

        /// <summary>
        /// Enables transport-wide reliable debug mode before Start. Returns false
        /// when the implementation cannot provide the test mode.
        /// </summary>
        protected internal abstract bool TryMakeReliableForDebug();

        bool IRawUnreliableTransportDebug.TryMakeReliableForDebug() => TryMakeReliableForDebug();

        /// <summary>
        /// Creates the endpoint for a session route and wires its send and stop
        /// operations.
        /// </summary>
        protected RawUnreliableEndpoint CreateEndpoint(IRawUnreliableHandler handler, IEndPoint? remote)
        {
            var ep = new RawUnreliableEndpoint(this, handler, remote)
            {
                SendDelegate = SendToCarrier,
                StopDelegate = StopEndpoint
            };
            return ep;
        }

        /// <summary>
        /// Type-safe variant factory invocation. NoAck invokes
        /// <c>factory(source)</c>; Ack invokes <c>factory(source, message)</c>.
        /// </summary>
        protected abstract IRawUnreliableHandler? InvokeFactory(TFactory factory, IEndPoint source, UnionDataList triggeringMessage);

        protected override void ProcessServerInbound(IEndPoint source, UnionDataList message)
        {
            if (_stopping || !IsStarted)
            {
                message.Release();
                return;
            }

            if (_routes.TryGetValue(source, out var existing))
            {
                DeliverToEndpoint(existing, message);
                return;
            }

            Conformance.BeforeHandlerFactoryGate.Hit();

            IRawUnreliableHandler? handler;
            try { handler = InvokeFactory((TFactory)HandlerFactory!, source, message); }
            catch (Exception e) { Log.wtf(e); message.Release(); return; }

            if (handler == null)
            {
                message.Release();
                return;
            }

            var ep = CreateEndpoint(handler, source);
            _routes[source] = ep;

            Conformance.BeforeHandlerStartedGate.Hit();
            ep.MarkValid();
            try
            {
                ep.Handler.OnStarted(ep);
                ep.MarkOnStartedCompleted();
            }
            catch (Exception e)
            {
                Log.wtf(e);
                _routes.TryRemove(source, out _);
                ep.MarkInvalid();
                message.Release();
                return;
            }

            DeliverToEndpoint(ep, message);
        }

        /// <summary>
        /// Runs one server session's teardown: the handler's OnStopped fires
        /// when the endpoint was started, and the session route is removed.
        /// </summary>
        protected override void TeardownEndpoint(RawEndpoint endpoint, StopReason reason)
        {
            var ep = (RawUnreliableEndpoint)endpoint;
            if (ep.TeardownDone) return;
            ep.MarkTeardownDone();

            ep.HitBeforeHandlerStoppedGate();

            if (ep.OnStartedCompleted)
            {
                try { ep.Handler.OnStopped(reason); }
                catch (Exception e) { Log.wtf(e); }
            }

            if (ep.RemoteEndPoint != null &&
                _routes.TryGetValue(ep.RemoteEndPoint, out var current) &&
                ReferenceEquals(current, ep))
            {
                _routes.TryRemove(ep.RemoteEndPoint, out _);
            }
        }
    }
}
