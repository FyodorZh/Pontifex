using System;
using Actuarius.Memory;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Unreliable
{
    /// <inheritdoc cref="IRawUnreliableServerEndpointImp"/>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: A server endpoint is created for one accepted source route. The
    /// message that triggered the route is passed through the constructor and stored as the first inbox
    /// element, so it is delivered before any message the now-published route can receive.</description></item>
    /// <item><description><b>Other notes</b>: Outbound admission combines the inherited per-endpoint byte budget
    /// with a <see cref="SendBudget"/> shared by all sessions of the transport. The shared budget is checked
    /// before the inherited admission and released through <see cref="RawEndpoint.OnPendingSendChanged"/>, so
    /// <see cref="RawEndpoint.DoSendRaw"/> needs no extra accounting.</description></item>
    /// </list>
    /// </remarks>
    public abstract class RawUnreliableServerEndpoint : RawUnreliableEndpoint, IRawUnreliableServerEndpointImp
    {
        private readonly SendBudget _sendBudget;

        /// <summary>
        /// Initializes a new unreliable server endpoint for one accepted session.
        /// </summary>
        /// <param name="transportName">The name of the transport that owns this endpoint.</param>
        /// <param name="remoteEndPoint">The source route of the accepted session.</param>
        /// <param name="handler">The handler that receives the trigger, later inbound messages, and the
        /// unreliable lifecycle notifications.</param>
        /// <param name="messageMaxByteSize">The inclusive maximum message size, in bytes, admitted by this
        /// endpoint; must be at least 1.</param>
        /// <param name="bufferCapacityBytes">The local byte budget for messages accepted by this endpoint but not
        /// yet transmitted; must be at least 1.</param>
        /// <param name="inboxCapacityMessages">The maximum number of messages buffered before the start
        /// completes, excluding the trigger; must not be negative.</param>
        /// <param name="inboxCapacityBytes">The maximum total payload size, in bytes, buffered before the start
        /// completes; must not be negative.</param>
        /// <param name="trigger">The message that created the route. Ownership transfers to the endpoint and it
        /// is delivered first, before every other buffered message; must not be
        /// <see langword="null"/>.</param>
        /// <param name="sendBudget">The outbound budget shared by all sessions of the transport; must not be
        /// <see langword="null"/>.</param>
        /// <param name="memory">The memory rental system used to rent buffers.</param>
        /// <param name="log">The logging system used by this endpoint.</param>
        /// <exception cref="ArgumentNullException"><paramref name="trigger"/> or
        /// <paramref name="sendBudget"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Preconditions/Postconditions</b>: The trigger is admitted regardless of the
        /// inbox caps, but it occupies one inbox slot and its payload size counts toward the inbox byte usage.
        /// It must be delivered before any other buffered message, so it must be the first inbox
        /// element.</description></item>
        /// <item><description><b>Other notes</b>: Configure the inherited local byte budget with the same cap as
        /// the shared budget, or larger, so that the shared budget is the one that binds. Otherwise a local
        /// rejection would report <see cref="SendResult.BufferOverflow"/> without the shared counter ever
        /// seeing the reservation.</description></item>
        /// </list>
        /// </remarks>
        protected RawUnreliableServerEndpoint(
            string transportName,
            IEndPoint remoteEndPoint,
            IRawUnreliableHandler handler,
            int messageMaxByteSize,
            long bufferCapacityBytes,
            int inboxCapacityMessages,
            long inboxCapacityBytes,
            UnionDataList trigger,
            SendBudget sendBudget,
            IMemoryRental memory,
            ILogger log)
            : base(transportName, remoteEndPoint, handler, messageMaxByteSize, bufferCapacityBytes,
                inboxCapacityMessages, inboxCapacityBytes, memory, log)
        {
            if (trigger == null!)
            {
                throw new ArgumentNullException(nameof(trigger));
            }

            _sendBudget = sendBudget ?? throw new ArgumentNullException(nameof(sendBudget));
            PrependToInbox(trigger);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe for concurrent calls from any thread; the shared budget
        /// check is a best-effort snapshot and may overshoot a bounded amount under concurrent
        /// senders.</description></item>
        /// <item><description><b>Precedence</b>: endpoint state is checked first, so a stopped endpoint reports
        /// its mapped state result for a null, released, oversized, or budget-rejected buffer. A running
        /// endpoint then reports <see cref="SendResult.InvalidMessage"/> for a null buffer and delegates the
        /// released-buffer and oversized cases to the inherited implementation
        /// (<see cref="SendResult.InvalidMessage"/> and <see cref="SendResult.MessageTooBig"/>). A message
        /// rejected by the shared budget is released and reported as
        /// <see cref="SendResult.BufferOverflow"/>.</description></item>
        /// <item><description><b>Other notes</b>: The shared budget reserves nothing here; the matching deltas are
        /// emitted by the inherited admission through <see cref="RawEndpoint.OnPendingSendChanged"/>, including
        /// the rollback that follows a rejected local admission. A state rejection releases a non-null live
        /// buffer; a null or dead buffer stays with the caller, as for
        /// <see cref="SendResult.InvalidMessage"/>.</description></item>
        /// </list>
        /// </remarks>
        public override SendResult Send(UnionDataList bufferToSend)
        {
            if (!CanSendNow(out var stateResult))
            {
                if (bufferToSend != null! && bufferToSend.IsAlive)
                {
                    bufferToSend.Release();
                }
                return stateResult;
            }

            if (bufferToSend == null!)
            {
                return SendResult.InvalidMessage;
            }

            if (!bufferToSend.IsAlive)
            {
                return base.Send(bufferToSend);
            }

            int size = bufferToSend.GetDataSize();

            if (size > MessageMaxByteSize)
            {
                return base.Send(bufferToSend);
            }

            if (!_sendBudget.CanAccept(size))
            {
                bufferToSend.Release();
                return SendResult.BufferOverflow;
            }

            return base.Send(bufferToSend);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Invoked from any thread that reserves or releases a buffer;
        /// the shared budget aggregates the deltas atomically.</description></item>
        /// <item><description><b>Other notes</b>: The inherited hook fires for the reservation, every rollback,
        /// every drop, and every completed transmission, so the shared counters return to their prior value
        /// once the pending messages drain.</description></item>
        /// </list>
        /// </remarks>
        protected override void OnPendingSendChanged(int deltaBytes, int deltaMessages)
        {
            base.OnPendingSendChanged(deltaBytes, deltaMessages);
            _sendBudget.Apply(deltaBytes, deltaMessages);
        }
    }
}
