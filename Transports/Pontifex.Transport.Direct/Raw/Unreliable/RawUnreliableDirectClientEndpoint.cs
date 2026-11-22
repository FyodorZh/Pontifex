using System;
using System.Threading;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.Raw.Unreliable;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Pontifex.VirtualDelivery;
using Scriba;

namespace Pontifex.Raw.Unreliable.Direct
{
    /// <summary>
    /// Unreliable client endpoint carried by an in-process <see cref="Channel"/>.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Ownership</b>: the endpoint owns its channel. The channel is acquired lazily on
    /// the first send because the direct carrier has no explicit connect step; a missing server drops the
    /// message and a later send retries the acquisition.</description></item>
    /// <item><description><b>Commit decoupling</b>: in-process delivery runs the peer's callbacks on the
    /// sending thread. The endpoint therefore owns a bounded commit queue: <see cref="DoSendRaw"/> posts the
    /// buffer and a dedicated worker performs the channel commit. This keeps the contract rule that a callback
    /// for an accepted message never begins before <c>Send</c> returns, and keeps the conformance gates off the
    /// caller's thread.</description></item>
    /// <item><description><b>Terminal signal</b>: the channel's <see cref="Channel.Disposed"/> notification is
    /// the only disconnection signal. In-process delivery has no end-of-stream, so either peer disposing the
    /// channel stops this endpoint; stopping the endpoint stops the owning client
    /// transport.</description></item>
    /// </list>
    /// </remarks>
    internal sealed class RawUnreliableDirectClientEndpoint : RawUnreliableEndpoint
    {
        private readonly IEndPoint _serverEndPoint;
        private readonly IEndPoint _clientEndPoint;
        private readonly IDeliverySystem _clientDeliverySystem;
        private readonly IDeliverySystem _serverDeliverySystem;
        private readonly Action _onChannelDisposed;
        private readonly SerializedCallbackQueue<UnionDataList> _sendQueue;

        private Channel? _channel;
        private bool _deliverySystemsInstalled;

        /// <summary>
        /// Gets the channel acquired by this endpoint, for carrier-specific tests.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Diagnostic and test seam only; the channel is present only
        /// after the first send acquired it and before teardown.</description></item>
        /// </list>
        /// </remarks>
        internal Channel? ChannelForTests => _channel;

        internal RawUnreliableDirectClientEndpoint(
            string transportName,
            IEndPoint serverEndPoint,
            IEndPoint clientEndPoint,
            IRawUnreliableHandler handler,
            int messageMaxByteSize,
            long bufferCapacityBytes,
            int queueCapacity,
            long inboxCapacityBytes,
            IDeliverySystem clientDeliverySystem,
            IDeliverySystem serverDeliverySystem,
            IMemoryRental memory,
            ILogger log)
            : base(transportName, serverEndPoint, handler, messageMaxByteSize, bufferCapacityBytes,
                queueCapacity, inboxCapacityBytes, memory, log)
        {
            _serverEndPoint = serverEndPoint ?? throw new ArgumentNullException(nameof(serverEndPoint));
            _clientEndPoint = clientEndPoint ?? throw new ArgumentNullException(nameof(clientEndPoint));
            _clientDeliverySystem = clientDeliverySystem ??
                throw new ArgumentNullException(nameof(clientDeliverySystem));
            _serverDeliverySystem = serverDeliverySystem ??
                throw new ArgumentNullException(nameof(serverDeliverySystem));
            _onChannelDisposed = OnChannelDisposed;

            _sendQueue = new SerializedCallbackQueue<UnionDataList>(
                queueCapacity,
                $"unreliable-direct-client-{transportName}",
                CommitSend,
                message => message.Release());
            _sendQueue.ExceptionHandler += ex => Log.wtf(ex);

            // Releases the channel and the commit queue after the inbox release installed by the base
            // constructor.
            Stopped += OnTerminalStopped;
        }

        /// <inheritdoc/>
        protected override void DoSendRaw(UnionDataList bufferToSend)
        {
            Channel? channel = _channel;
            if (channel == null)
            {
                channel = Connect();
                if (channel == null)
                {
                    bufferToSend.Release();
                    return;
                }
            }

            if (channel.IsDisposed || !_sendQueue.Post(bufferToSend))
            {
                bufferToSend.Release();
            }
        }

        /// <summary>
        /// Commits one queued buffer to the in-process channel.
        /// </summary>
        /// <param name="bufferToSend">The buffer to commit. Ownership transfers to this method on every
        /// path.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Runs on the endpoint's single commit worker, never
        /// concurrently with another commit.</description></item>
        /// <item><description><b>Other notes</b>: The channel releases the buffer on every path. A disposed
        /// channel drops the buffer; the endpoint is stopped by the channel's own terminal
        /// notification.</description></item>
        /// </list>
        /// </remarks>
        private void CommitSend(UnionDataList bufferToSend)
        {
            Channel? channel = _channel;
            if (channel == null)
            {
                bufferToSend.Release();
                return;
            }

            Conformance.BeforeSendCommitGate.Hit();
            try
            {
                channel.SendToServer(bufferToSend);
            }
            catch (Exception ex)
            {
                // Channel.SendToServer releases only on its disposed path. A delivery system can throw
                // before taking ownership (a disposed Netem system does), so the endpoint still owns the
                // buffer here and releases it exactly once.
                Log.wtf(ex);
                bufferToSend.Release();
            }
            finally
            {
                Conformance.AfterSendCommitGate.Hit();
            }
        }

        /// <summary>
        /// Acquires the in-process channel for this endpoint, if a server is registered.
        /// </summary>
        /// <returns>The acquired channel, or <see langword="null"/> when the direct server is not
        /// available.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Called only from <see cref="DoSendRaw"/>, which runs on the
        /// endpoint's serial action queue.</description></item>
        /// <item><description><b>Other notes</b>: A channel that is already disposed when it is handed over
        /// stops this endpoint; the caller then drops the current message.</description></item>
        /// </list>
        /// </remarks>
        private Channel? Connect()
        {
            Channel? channel = ChannelManager.Instance.Connect(_serverEndPoint, _clientEndPoint);
            if (channel == null)
            {
                return null;
            }

            channel.ClientHandler = OnChannelMessage;
            channel.SetClientDeliverySystem(_clientDeliverySystem);
            channel.SetServerDeliverySystem(_serverDeliverySystem);
            _deliverySystemsInstalled = true;
            channel.Disposed += _onChannelDisposed;
            _channel = channel;

            // The server may have rejected the channel synchronously, before the subscription above.
            if (channel.IsDisposed)
            {
                ((IRawUnreliableEndpoint)this).Stop(new UnknownRemoteIntention(TransportName));
                return null;
            }

            return channel;
        }

        /// <summary>
        /// Checks one inbound frame against the message limit and hands it to the endpoint inbox.
        /// </summary>
        /// <param name="message">The received message. Ownership transfers to this method on every
        /// path.</param>
        private void OnChannelMessage(UnionDataList message)
        {
            if (message.GetDataSize() > MessageMaxByteSize)
            {
                message.Release();
                Log.e("Inbound message exceeds MessageMaxByteSize and was discarded");
                return;
            }

            Deliver(message);
        }

        /// <summary>
        /// Stops the endpoint when its channel is disposed by either peer.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The one-shot endpoint stop makes a repeated channel
        /// notification a no-op. Stopping the endpoint stops the owning client transport through the transport
        /// subscription.</description></item>
        /// </list>
        /// </remarks>
        private void OnChannelDisposed()
        {
            ((IRawUnreliableEndpoint)this).Stop(new UnknownRemoteIntention(TransportName));
        }

        /// <summary>
        /// Releases the commit queue and the channel on every terminal path.
        /// </summary>
        /// <param name="reason">The reason the endpoint stopped. It is not used by the release.</param>
        private void OnTerminalStopped(StopReason reason)
        {
            _sendQueue.Dispose();
            CleanupChannel();

            if (!_deliverySystemsInstalled)
            {
                DeliverySystemLifetime.Release(_clientDeliverySystem);
                if (!ReferenceEquals(_clientDeliverySystem, _serverDeliverySystem))
                {
                    DeliverySystemLifetime.Release(_serverDeliverySystem);
                }
            }
        }

        /// <summary>
        /// Unsubscribes from and releases the owned channel exactly once.
        /// </summary>
        private void CleanupChannel()
        {
            Channel? channel = Interlocked.Exchange(ref _channel, null);
            if (channel == null)
            {
                return;
            }

            channel.Disposed -= _onChannelDisposed;
            channel.ClientHandler = null;

            // The manager removes the channel from the server registry and disposes it. When the server is
            // already gone there is no entry left to remove, so the channel is disposed directly.
            if (!ChannelManager.Instance.Disconnect(_serverEndPoint, _clientEndPoint))
            {
                channel.Dispose();
            }
        }

        public override string ToString()
        {
            try
            {
                return $"direct-client-endpoint[{_serverEndPoint}]";
            }
            catch (Exception)
            {
                return "direct-client-endpoint[unknown]";
            }
        }
    }
}
