using System;
using System.Threading;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable
{
    /// <inheritdoc cref="IRawReliableServerEndpointImp"/>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Handler callbacks</b>: a connected session delivers <c>OnConnected</c>, then one
    /// <c>OnDisconnected</c>, then one <c>OnStopped</c>. A session that never reaches <c>OnConnected</c>
    /// delivers exactly one <c>OnStopped</c> and no <c>OnDisconnected</c>. The logical connection is owned by
    /// the transport handshake; this endpoint reports it to the session handler.</description></item>
    /// <item><description><b>Other notes</b>: The concrete transport must override BeginDisconnect, call the
    /// base implementation, and then call FinishDisconnect to complete teardown. The agreed message maximum is
    /// sealed at construction because the accept handshake already negotiated it.</description></item>
    /// </list>
    /// </remarks>
    public abstract class RawReliableHandshakeServerEndpoint : RawReliableServerEndpoint, IRawReliableServerEndpointImp
    {
        private UnionDataList? _pendingHandshakeResponse;

        /// <summary>
        /// Initializes a new reliable server endpoint that performs the transport handshake, with the given
        /// transport name, logging, and memory rental, and the idle-session timeout.
        /// </summary>
        /// <param name="transportName">The name of the transport that owns this endpoint.</param>
        /// <param name="remoteEndPoint">The remote endpoint address, or <see langword="null"/> when it is
        /// unknown.</param>
        /// <param name="handler">The session handler that receives the connection lifecycle notifications and
        /// inbound messages.</param>
        /// <param name="messageMaxByteSize">The negotiated inclusive maximum message size, in bytes, admitted by
        /// this endpoint; must be at least 1.</param>
        /// <param name="bufferCapacityBytes">The byte budget for messages accepted by this endpoint but not
        /// yet transmitted; must be at least 1.</param>
        /// <param name="memory">The memory rental system used to rent buffers.</param>
        /// <param name="log">The logging system used by this endpoint.</param>
        /// <param name="disconnectTimeout">The maximum time without an inbound message before the endpoint is
        /// stopped as timed out; must be positive.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The handler is retained, not disposed, by the endpoint. The
        /// endpoint starts in the constructed stage. The negotiated message maximum is marked as sealed
        /// immediately, because the base constructor sets it before the start stage begins.</description></item>
        /// </list>
        /// </remarks>
        protected RawReliableHandshakeServerEndpoint(string transportName, IEndPoint remoteEndPoint,
            IRawReliableServerHandler handler, int messageMaxByteSize, long bufferCapacityBytes,
            IMemoryRental memory, ILogger log, TimeSpan disconnectTimeout)
            : base(transportName, remoteEndPoint, handler, messageMaxByteSize, bufferCapacityBytes, memory, log, disconnectTimeout)
        {
            MarkMessageMaxByteSizeSet();
            Stopped += OnEndpointStopped;
        }

        /// <summary>
        /// Stores the already-built handshake response that is sent once the low-level connection is ready.
        /// </summary>
        /// <param name="response">The handshake response without its frame-type byte. Ownership transfers to the
        /// endpoint.</param>
        protected void SetPendingHandshakeResponse(UnionDataList response)
        {
            if (response == null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            if (Interlocked.CompareExchange(ref _pendingHandshakeResponse, response, null) != null)
            {
                throw new InvalidOperationException("The pending handshake response is already set.");
            }
        }

        /// <summary>
        /// Releases the buffered handshake response when it has not been sent yet.
        /// </summary>
        protected void ReleasePendingHandshakeResponse()
        {
            var response = Interlocked.Exchange(ref _pendingHandshakeResponse, null);
            response?.Release();
        }

        /// <summary>
        /// Validates the framed handshake response before it is scheduled for transmission.
        /// </summary>
        /// <param name="framedResponse">The response including its frame type.</param>
        /// <returns><see langword="true"/> when the response fits the carrier's handshake limit; otherwise
        /// <see langword="false"/>.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The default returns <see langword="true"/>. A carrier that
        /// enforces a handshake size limit overrides it and compares the framed size, so the response is checked
        /// after its type byte is added.</description></item>
        /// </list>
        /// </remarks>
        protected virtual bool ValidateHandshakeResponseFrame(UnionDataList framedResponse)
        {
            return true;
        }

        /// <summary>
        /// Sends the pending handshake response, admits the session, and then starts the inherited idle-session
        /// watchdog.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Called on the serial action queue.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: A missing response or a response that cannot
        /// be admitted for outbound delivery stops the endpoint without notifying the handler of a connection.
        /// A handler exception during the connection notification is logged and isolated, and the watchdog is
        /// still started.</description></item>
        /// </list>
        /// </remarks>
        protected override void OnConnected()
        {
            var response = Interlocked.Exchange(ref _pendingHandshakeResponse, null);
            if (response == null)
            {
                Stop(new TextFail(TransportName, "No pending transport handshake response"), false);
                return;
            }

            bool sent;
            using (var disposer = response.AsDisposable())
            {
                try
                {
                    response.PutFirst((byte)MessageType.HandshakeResponseMessage);
                    if (!ValidateHandshakeResponseFrame(response))
                    {
                        Stop(new TextFail(TransportName,
                            "Handshake response exceeds HandshakeMaxByteSize"), false);
                        return;
                    }

                    sent = ScheduleToSend(response.Acquire()) == SendResult.Ok;
                }
                catch (Exception ex)
                {
                    Log.wtf(ex);
                    sent = false;
                }
            }

            if (!sent)
            {
                Stop(new TextFail(TransportName, "Failed to send the transport handshake response"), false);
                return;
            }

            if (!NotifyConnected())
            {
                return;
            }

            base.OnConnected();
        }

        /// <summary>
        /// Handles a fatal endpoint failure by releasing the pending response and notifying the handler of the
        /// logical disconnection when the endpoint was connected, then invoking the base implementation.
        /// </summary>
        protected override void FailHard(StopReason reason)
        {
            ReleasePendingHandshakeResponse();
            NotifyDisconnectedCore(reason);

            base.FailHard(reason);
        }

        /// <summary>
        /// Releases the pending response and notifies the handler of the logical disconnection when the
        /// endpoint was connected.
        /// </summary>
        protected override void BeginDisconnect(StopReason reason, bool tryBeGentle)
        {
            ReleasePendingHandshakeResponse();
            NotifyDisconnectedCore(reason);
        }

        private void OnEndpointStopped(StopReason reason)
        {
            ReleasePendingHandshakeResponse();
        }
    }
}
