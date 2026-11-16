using System.Net;
using Pontifex.NetSockets;
using Pontifex.Utils;

namespace Pontifex.Raw.Unreliable.Udp
{
    /// <summary>
    /// One accepted UDP session's outbound path: the transport's shared asynchronous sender plus the
    /// session's remote address.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Thread safety</b>: <see cref="UdpAsyncSender.Send"/> is safe to call concurrently
    /// from any number of endpoint serial queues.</description></item>
    /// <item><description><b>Delivery semantics</b>: best effort. A queue overflow or a stopped transport
    /// releases the message and drops it silently; dropped packets are never logged. Exception logging stays in
    /// the sender.</description></item>
    /// </list>
    /// </remarks>
    internal sealed class UdpSessionChannel
    {
        private readonly UdpAsyncSender _sender;
        private readonly EndPoint _remoteEndPoint;

        public UdpSessionChannel(UdpAsyncSender sender, EndPoint remoteEndPoint)
        {
            _sender = sender;
            _remoteEndPoint = remoteEndPoint;
        }

        /// <summary>
        /// Submits one message to the shared sender.
        /// </summary>
        /// <param name="message">The message to send. Ownership transfers to the sender on every
        /// path.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The send result is deliberately ignored. Unreliable mode has
        /// no post-admission reporting; overflow and a stopped sender both release the message and drop
        /// it.</description></item>
        /// </list>
        /// </remarks>
        public void Send(UnionDataList message)
        {
            if (message == null!)
            {
                return;
            }

            // The sender consumes and releases the message on every result, including overflow and stop.
            _sender.Send(_remoteEndPoint, message);
        }
    }
}
