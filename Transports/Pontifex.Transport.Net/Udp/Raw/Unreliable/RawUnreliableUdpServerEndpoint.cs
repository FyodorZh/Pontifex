using System;
using Actuarius.Memory;
using Pontifex.Raw.Unreliable;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Unreliable.Udp
{
    /// <summary>
    /// Unreliable UDP server session endpoint. Sends replies through the accepting transport's shared
    /// asynchronous sender and the session's remote address.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Ownership</b>: the transport owns the socket, the receiver, and the shared sender.
    /// The endpoint owns one <see cref="UdpSessionChannel"/> for the accepted source route; stopping the
    /// transport stops the sender and then the endpoints.</description></item>
    /// <item><description><b>Delivery semantics</b>: best effort. A queue overflow or a stopped transport drops
    /// the packet silently; dropped packets are never logged.</description></item>
    /// </list>
    /// </remarks>
    internal sealed class RawUnreliableUdpServerEndpoint : RawUnreliableServerEndpoint
    {
        private readonly UdpSessionChannel _channel;

        internal RawUnreliableUdpServerEndpoint(
            string transportName,
            IEndPoint remoteEndPoint,
            IRawUnreliableHandler handler,
            int messageMaxByteSize,
            long bufferCapacityBytes,
            int inboxCapacityMessages,
            long inboxCapacityBytes,
            UnionDataList trigger,
            SendBudget sendBudget,
            UdpSessionChannel channel,
            IMemoryRental memory,
            ILogger log)
            : base(transportName, remoteEndPoint, handler, messageMaxByteSize, bufferCapacityBytes,
                inboxCapacityMessages, inboxCapacityBytes, trigger, sendBudget, memory, log)
        {
            _channel = channel ?? throw new System.ArgumentNullException(nameof(channel));
        }

        /// <inheritdoc/>
        protected override void DoSendRaw(UnionDataList bufferToSend)
        {
            Conformance.BeforeSendCommitGate.Hit();
            try
            {
                _channel.Send(bufferToSend);
            }
            catch (Exception ex)
            {
                // Best-effort drop: an unexpected sender failure must not fail the endpoint hard. The
                // sender owns and releases the buffer on every path it completes, so this catch must not
                // release it again.
                Log.wtf(ex);
            }
            finally
            {
                Conformance.AfterSendCommitGate.Hit();
            }
        }

        public override string ToString()
        {
            try
            {
                return $"udp-server-endpoint[{RemoteEndPoint}]";
            }
            catch (System.Exception)
            {
                return "udp-server-endpoint[unknown]";
            }
        }
    }
}
