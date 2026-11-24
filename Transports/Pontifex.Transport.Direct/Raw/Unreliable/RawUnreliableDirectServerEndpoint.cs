using System;
using System.Threading;
using Actuarius.Memory;
using Pontifex.Raw.Unreliable;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Unreliable.Direct
{
    /// <summary>
    /// Unreliable server session endpoint carried by an in-process <see cref="Channel"/>.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Ownership</b>: the accepting transport owns the channel registry. The endpoint
    /// holds the channel that delivered its trigger and sends replies through it; it does not dispose or
    /// unregister the channel, so a later message from the same source can recreate a route on the same
    /// channel.</description></item>
    /// <item><description><b>Commit decoupling</b>: the endpoint owns a bounded commit queue so that the
    /// peer's callbacks never begin before <c>Send</c> returns, even though in-process delivery is
    /// synchronous.</description></item>
    /// <item><description><b>Terminal signal</b>: a disposed channel stops the endpoint. The endpoint's own
    /// stop removes its route through the transport's terminal
    /// subscription.</description></item>
    /// </list>
    /// </remarks>
    internal sealed class RawUnreliableDirectServerEndpoint : RawUnreliableServerEndpoint
    {
        private readonly Channel _channel;
        private readonly Action _onChannelDisposed;
        private readonly SerializedCallbackQueue<CommitItem> _sendQueue;
        private readonly int _queueCapacity;

        private int _outstandingCommitMessages;

        /// <summary>
        /// Gets or sets a test hook invoked after a successful commit-queue post and before the send action
        /// returns.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Internal test seam only.</description></item>
        /// </list>
        /// </remarks>
        internal Action? AfterPostForTests { get; set; }

        /// <summary>
        /// Gets the number of accepted messages whose commit has not completed.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Diagnostic and test seam only. The value is the endpoint's
        /// internal admission counter, not the shared transport budget.</description></item>
        /// </list>
        /// </remarks>
        internal int OutstandingCommitMessagesForTests => Volatile.Read(ref _outstandingCommitMessages);

        internal RawUnreliableDirectServerEndpoint(
            string transportName,
            IEndPoint remoteEndPoint,
            Channel channel,
            IRawUnreliableHandler handler,
            int messageMaxByteSize,
            long bufferCapacityBytes,
            int queueCapacity,
            long inboxCapacityBytes,
            UnionDataList trigger,
            SendBudget sendBudget,
            IMemoryRental memory,
            ILogger log)
            : base(transportName, remoteEndPoint, handler, messageMaxByteSize, bufferCapacityBytes,
                queueCapacity, inboxCapacityBytes, trigger, sendBudget, memory, log)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _onChannelDisposed = OnChannelDisposed;
            _queueCapacity = queueCapacity;

            _sendQueue = new SerializedCallbackQueue<CommitItem>(
                queueCapacity,
                $"unreliable-direct-server-{transportName}",
                CommitSend,
                item =>
                {
                    item.Message.Release();
                    item.Token.ReleaseOnce();
                });
            _sendQueue.ExceptionHandler += ex => Log.wtf(ex);

            Stopped += OnTerminalStopped;
            _channel.Disposed += _onChannelDisposed;
        }

        /// <inheritdoc/>
        protected override void BeginStart()
        {
            if (_channel.IsDisposed)
            {
                // The peer disposed the channel after this endpoint was created but before it could
                // start, so the subscription above never received the notification. Stop first: the
                // one-shot stop invalidates the endpoint, releases the trigger with the inbox, and
                // removes the route through the transport's terminal subscription.
                ((IRawUnreliableEndpoint)this).Stop(new UnknownRemoteIntention(TransportName));
            }

            base.BeginStart();
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
        /// token-taking overload. The override releases the buffer as a guard.</description></item>
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
            if (_channel.IsDisposed || !_sendQueue.Post(item))
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
        /// <param name="bufferToSend">The buffer to commit. Ownership transfers to this method on every
        /// path.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Runs on the endpoint's single commit worker, never
        /// concurrently with another commit.</description></item>
        /// </list>
        /// </remarks>
        private void CommitSend(CommitItem item)
        {
            UnionDataList bufferToSend = item.Message;
            Channel channel = _channel;
            if (channel.IsDisposed)
            {
                bufferToSend.Release();
                item.Token.ReleaseOnce();
                return;
            }

            Conformance.BeforeSendCommitGate.Hit();
            WaitForSendReturn();
            try
            {
                // The channel releases the buffer on its disposed path only. A delivery system can throw
                // before taking ownership (a disposed Netem system does), so the endpoint still owns the
                // buffer here and releases it exactly once.
                channel.SendToClient(bufferToSend);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                bufferToSend.Release();
            }
            finally
            {
                Conformance.AfterSendCommitGate.Hit();
                item.Token.ReleaseOnce();
            }
        }

        private void OnChannelDisposed()
        {
            ((IRawUnreliableEndpoint)this).Stop(new UnknownRemoteIntention(TransportName));
        }

        private void OnTerminalStopped(StopReason reason)
        {
            _sendQueue.Dispose();
            _channel.Disposed -= _onChannelDisposed;
        }

        public override string ToString()
        {
            try
            {
                return $"direct-server-endpoint[{RemoteEndPoint}]";
            }
            catch (Exception)
            {
                return "direct-server-endpoint[unknown]";
            }
        }

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
