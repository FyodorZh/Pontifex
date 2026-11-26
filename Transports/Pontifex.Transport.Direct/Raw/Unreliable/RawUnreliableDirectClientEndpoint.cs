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
        private IDeliverySystem _clientDeliverySystem;
        private IDeliverySystem _serverDeliverySystem;
        private readonly RawUnreliableDirectClientTransport.DeliveryDirection _clientDirection;
        private readonly RawUnreliableDirectClientTransport.DeliveryDirection _serverDirection;
        private readonly Action _onChannelDisposed;
        private readonly SerializedCallbackQueue<CommitItem> _sendQueue;
        private readonly int _queueCapacity;

        private Channel? _channel;
        private bool _clientSystemInstalled;
        private bool _serverSystemInstalled;
        private int _outstandingCommitMessages;

        /// <summary>
        /// Gets or sets a test hook invoked after a successful commit-queue post and before the send action
        /// returns.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Internal test seam only. It lets a test throw after the post
        /// and prove that the reservation is still released exactly once by the commit path.</description></item>
        /// </list>
        /// </remarks>
        internal Action? AfterPostForTests { get; set; }

        /// <summary>
        /// Gets the number of accepted messages whose commit has not completed.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Diagnostic and test seam only. The value is the internal
        /// admission counter, not the shared transport budget.</description></item>
        /// </list>
        /// </remarks>
        internal int OutstandingCommitMessagesForTests => Volatile.Read(ref _outstandingCommitMessages);

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

        /// <summary>
        /// Invokes the commit path directly for carrier tests.
        /// </summary>
        /// <param name="bufferToSend">The buffer to commit. Ownership transfers to this method.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Internal test seam only. It runs the same commit logic as the
        /// queue worker so a test can arrange a disposed channel deterministically.</description></item>
        /// </list>
        /// </remarks>
        internal void CommitForTests(UnionDataList bufferToSend)
        {
            CommitSend(new CommitItem(bufferToSend, CreateSendSlotReservation(() => { })));
        }

        /// <summary>
        /// Replaces the channel reference for carrier tests.
        /// </summary>
        /// <param name="channel">The channel to expose to the commit path, or <see langword="null"/>.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Internal test seam only. It does not subscribe to the channel,
        /// so a test can observe the discarded-message path without triggering the endpoint's terminal
        /// stop.</description></item>
        /// </list>
        /// </remarks>
        internal void SetChannelForTests(Channel? channel)
        {
            _channel = channel;
        }

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
            RawUnreliableDirectClientTransport.DeliveryDirection clientDirection,
            RawUnreliableDirectClientTransport.DeliveryDirection serverDirection,
            bool unboundedCommitQueue,
            IMemoryRental memory,
            ILogger log)
            : base(transportName, serverEndPoint, handler, messageMaxByteSize, bufferCapacityBytes,
                queueCapacity, inboxCapacityBytes, memory, log)
        {
            if (queueCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(queueCapacity));
            }

            _serverEndPoint = serverEndPoint ?? throw new ArgumentNullException(nameof(serverEndPoint));
            _clientEndPoint = clientEndPoint ?? throw new ArgumentNullException(nameof(clientEndPoint));
            _clientDeliverySystem = clientDeliverySystem ??
                throw new ArgumentNullException(nameof(clientDeliverySystem));
            _serverDeliverySystem = serverDeliverySystem ??
                throw new ArgumentNullException(nameof(serverDeliverySystem));
            _clientDirection = clientDirection ?? throw new ArgumentNullException(nameof(clientDirection));
            _serverDirection = serverDirection ?? throw new ArgumentNullException(nameof(serverDirection));
            _onChannelDisposed = OnChannelDisposed;
            _queueCapacity = queueCapacity;

            if (unboundedCommitQueue)
            {
                _sendQueue = new SerializedCallbackQueue<CommitItem>(
                    $"unreliable-direct-client-unbounded-{transportName}",
                    CommitSend,
                    DisposeItem);
            }
            else
            {
                _sendQueue = new SerializedCallbackQueue<CommitItem>(
                    queueCapacity,
                    $"unreliable-direct-client-{transportName}",
                    CommitSend,
                    DisposeItem);
            }

            _sendQueue.ExceptionHandler += ex => Log.wtf(ex);

            // Releases the channel and the commit queue after the inbox release installed by the base
            // constructor.
            Stopped += OnTerminalStopped;
        }

        /// <inheritdoc/>
        protected override SendSlotReservation? TryReserveSendSlot()
        {
            while (true)
            {
                int current = Volatile.Read(ref _outstandingCommitMessages);
                if (current >= _queueCapacity)
                {
                    return null;
                }

                if (Interlocked.CompareExchange(ref _outstandingCommitMessages, current + 1, current) == current)
                {
                    return CreateSendSlotReservation(ReleaseCommitSlot);
                }
            }
        }

        private void ReleaseCommitSlot()
        {
            Interlocked.Decrement(ref _outstandingCommitMessages);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Not used by this endpoint: admission always runs through the
        /// token-taking overload. The override releases the buffer as a guard so a future caller cannot leak
        /// it.</description></item>
        /// </list>
        /// </remarks>
        protected override void DoSendRaw(UnionDataList bufferToSend)
        {
            bufferToSend.Release();
        }

        /// <inheritdoc/>
        protected override void DoSendRaw(UnionDataList bufferToSend, SendSlotReservation reservation)
        {
            var item = new CommitItem(bufferToSend, reservation);
            Channel? channel = _channel;
            if (channel == null)
            {
                channel = Connect();
                if (channel == null)
                {
                    bufferToSend.Release();
                    reservation.ReleaseOnce();
                    return;
                }
            }

            if (channel.IsDisposed || !_sendQueue.Post(item))
            {
                bufferToSend.Release();
                reservation.ReleaseOnce();
                return;
            }

            AfterPostForTests?.Invoke();
        }

        /// <summary>
        /// Commits one queued buffer to the in-process channel.
        /// </summary>
        /// <param name="item">The queued message and its reservation. Ownership transfers to this method on
        /// every path.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Runs on the endpoint's single commit worker, never
        /// concurrently with another commit.</description></item>
        /// <item><description><b>Other notes</b>: The channel releases the buffer on every path. A disposed
        /// channel drops the buffer; the endpoint is stopped by the channel's own terminal notification. The
        /// delivery is deferred until the sender has returned from <c>Send</c>.</description></item>
        /// </list>
        /// </remarks>
        private void CommitSend(CommitItem item)
        {
            UnionDataList bufferToSend = item.Message;
            Channel? channel = _channel;
            if (channel == null || channel.IsDisposed)
            {
                bufferToSend.Release();
                item.Token.ReleaseOnce();
                return;
            }

            Conformance.BeforeSendCommitGate.Hit();
            WaitForSendReturn();
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
                item.Token.ReleaseOnce();
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
            Channel? channel = null;
            bool clientInstalled = false;
            bool serverInstalled = false;

            try
            {
                channel = ChannelManager.Instance.Connect(RawUnreliableDirectInfo.TransportName, _serverEndPoint, _clientEndPoint);
                if (channel == null)
                {
                    return null;
                }

                channel.ClientHandler = OnChannelMessage;

                channel.SetClientDeliverySystem(_clientDeliverySystem);
                clientInstalled = true;
                _clientSystemInstalled = true;

                channel.SetServerDeliverySystem(_serverDeliverySystem);
                serverInstalled = true;
                _serverSystemInstalled = true;

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
            catch (Exception ex)
            {
                // Manager and install failures are transient carrier failures: the endpoint stays alive and
                // the next send retries with fresh systems. No application callback runs here.
                Log.wtf(ex);

                if (channel != null)
                {
                    channel.ClientHandler = null;

                    // Remove the failed channel from the server registry and dispose it. The registry, not
                    // the endpoint, owns a channel created by the manager.
                    if (!ChannelManager.Instance.Disconnect(RawUnreliableDirectInfo.TransportName, _serverEndPoint, _clientEndPoint))
                    {
                        channel.Dispose();
                    }
                }

                if (!clientInstalled)
                {
                    ReleaseOnce(_clientDeliverySystem);
                }

                if (!serverInstalled && !ReferenceEquals(_serverDeliverySystem, _clientDeliverySystem))
                {
                    ReleaseOnce(_serverDeliverySystem);
                }

                _clientDirection.AskResetToPerfect();
                _serverDirection.AskResetToPerfect();
                _clientDeliverySystem = _clientDirection.Materialize();
                _serverDeliverySystem = _serverDirection.Materialize();
                _clientSystemInstalled = false;
                _serverSystemInstalled = false;
                return null;
            }
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

            if (!_clientSystemInstalled)
            {
                ReleaseOnce(_clientDeliverySystem);
            }

            if (!_serverSystemInstalled && !ReferenceEquals(_serverDeliverySystem, _clientDeliverySystem))
            {
                ReleaseOnce(_serverDeliverySystem);
            }
        }

        private static void ReleaseOnce(IDeliverySystem system)
        {
            try
            {
                DeliverySystemLifetime.Release(system);
            }
            catch (Exception)
            {
            }
        }

        private static void DisposeItem(CommitItem item)
        {
            item.Message.Release();
            item.Token.ReleaseOnce();
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
            if (!ChannelManager.Instance.Disconnect(RawUnreliableDirectInfo.TransportName, _serverEndPoint, _clientEndPoint))
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

        /// <summary>
        /// One queued commit message and its admission reservation.
        /// </summary>
        private sealed class CommitItem
        {
            public CommitItem(UnionDataList message, SendSlotReservation token)
            {
                Message = message;
                Token = token;
            }

            public UnionDataList Message { get; }

            public SendSlotReservation Token { get; }
        }
    }
}
