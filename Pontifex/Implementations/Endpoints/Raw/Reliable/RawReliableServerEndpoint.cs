using System;
using Actuarius.Concurrent;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable
{
    /// <inheritdoc cref="IRawReliableServerEndpointImp"/>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: Overrides of OnConnected and ProcessInboundMessage must call the
    /// base implementation to keep the watchdog working.</description></item>
    /// </list>
    /// </remarks>
    public abstract class RawReliableServerEndpoint : RawReliableEndpoint, IRawReliableServerEndpointImp
    {
        private readonly TimeSpan _disconnectTimeout;
        private readonly IRawReliableServerHandler _handler;
        private readonly AtomicBox<DateTime> _lastInboundMessage = new();

        private volatile bool _timeInitialized;

        /// <summary>
        /// Initializes a new reliable server endpoint with the given transport name, logging, and memory
        /// rental, and the idle-session timeout.
        /// </summary>
        /// <param name="transportName">The name of the transport that owns this endpoint.</param>
        /// <param name="remoteEndPoint">The remote endpoint address, or <see langword="null"/> when it is
        /// unknown.</param>
        /// <param name="handler">The handler that receives inbound messages and connection lifecycle
        /// notifications.</param>
        /// <param name="messageMaxByteSize">The inclusive maximum message size, in bytes, admitted by this
        /// endpoint; must be at least 1.</param>
        /// <param name="bufferCapacityBytes">The byte budget for messages accepted by this endpoint but not
        /// yet transmitted; must be at least 1.</param>
        /// <param name="memory">The memory rental system used to rent buffers.</param>
        /// <param name="log">The logging system used by this endpoint.</param>
        /// <param name="disconnectTimeout">The maximum time without an inbound message before the endpoint is
        /// stopped as timed out; must be positive.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Preconditions/Postconditions</b>: <paramref name="disconnectTimeout"/> must
        /// be greater than zero.</description></item>
        /// <item><description><b>Other notes</b>: The handler is retained, not disposed, by the
        /// endpoint.</description></item>
        /// </list>
        /// </remarks>
        protected RawReliableServerEndpoint(string transportName, IEndPoint remoteEndPoint, IRawReliableServerHandler handler,
            int messageMaxByteSize, long bufferCapacityBytes, IMemoryRental memory, ILogger log,
            TimeSpan disconnectTimeout)
            : base(transportName, remoteEndPoint, handler, messageMaxByteSize, bufferCapacityBytes, memory, log)
        {
            if (disconnectTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentException(nameof(disconnectTimeout));
            }
            _disconnectTimeout = disconnectTimeout;
            _handler = handler;
        }

        /// <summary>
        /// Gets the server handler that receives the connection lifecycle notifications and inbound messages
        /// for this session.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to read concurrently; the reference is immutable.</description></item>
        /// <item><description><b>Other notes</b>: The handler is retained, not disposed, by the endpoint.</description></item>
        /// </list>
        /// </remarks>
        protected IRawReliableServerHandler ServerHandler => _handler;

        /// <summary>
        /// Notifies the server handler of the logical connection and arms the shared connected latch.
        /// </summary>
        /// <returns>Always <see langword="true"/>; a throwing handler is logged and isolated.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call from any thread.</description></item>
        /// <item><description><b>Other notes</b>: The latch is armed before the handler callback, so a later
        /// <see cref="NotifyDisconnectedCore"/> call delivers the logical disconnect notification even when
        /// the connection callback threw. A handler exception never stops the endpoint.</description></item>
        /// </list>
        /// </remarks>
        protected bool NotifyConnected()
        {
            return NotifyConnectedCore(() => _handler.OnConnected(this));
        }

        /// <summary>
        /// Gets the current time used by the idle-session watchdog.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to read concurrently.</description></item>
        /// <item><description><b>Other notes</b>: The default implementation returns
        /// <see cref="DateTime.Now"/>. It is a testability seam; overrides should return a monotonically
        /// non-decreasing local time.</description></item>
        /// </list>
        /// </remarks>
        protected virtual DateTime Now => DateTime.Now;

        /// <inheritdoc/>
        public void PeriodicTick()
        {
            if (IsLowLevelConnected && _timeInitialized)
            {
                if (Now - _lastInboundMessage.Value > _disconnectTimeout)
                {
                    Stop(new TimeOut(TransportName), false);
                }
            }
        }

        /// <summary>
        /// Initializes the idle-session watchdog timestamp when the logical connection completes.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Called on the serial action queue.</description></item>
        /// <item><description><b>Other notes</b>: The initialization is deferred by one action; if the endpoint
        /// is no longer connected when that action runs, the watchdog remains inactive. Overrides must call the
        /// base implementation.</description></item>
        /// </list>
        /// </remarks>
        protected override void OnConnected()
        {
            _actionQueue.Schedule(() =>
            {
                if (IsLowLevelConnected)
                {
                    _lastInboundMessage.Value = Now;
                    _timeInitialized = true;
                }
            });
        }

        /// <summary>
        /// Refreshes the idle-session watchdog timestamp for an inbound frame. Derived classes that intercept
        /// inbound frames without delegating to the base implementation must call this method so that the
        /// watchdog does not expire while those frames are being processed.
        /// </summary>
        protected void RefreshLastInboundMessage()
        {
            _lastInboundMessage.Value = Now;
        }

        /// <summary>
        /// Refreshes the idle-session timestamp for every inbound frame and answers ping frames with pong
        /// frames; all other frame types are delegated to the base implementation.
        /// </summary>
        /// <param name="messageType">The frame type popped from the message.</param>
        /// <param name="message">The frame body. A ping frame is released by this method.</param>
        /// <returns><see langword="true"/> when the frame was handled; otherwise
        /// <see langword="false"/>.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Invoked on the caller's thread; the watchdog timestamp is
        /// accessed through a thread-safe atomic box.</description></item>
        /// <item><description><b>Side effects</b>: Refreshes the watchdog timestamp for every inbound frame,
        /// including frames that are later rejected.</description></item>
        /// <item><description><b>Other notes</b>: A ping whose pong cannot be admitted stops the endpoint; a
        /// ping frame is always reported as handled, so the caller does not release it again. Overrides must
        /// delegate to the base implementation for non-ping frames.</description></item>
        /// </list>
        /// </remarks>
        protected override bool ProcessInboundMessage(MessageType messageType, UnionDataList message)
        {
            RefreshLastInboundMessage();
            switch (messageType)
            {
                case MessageType.PingMessage:
                {
                    message.Release();
                    
                    var buffer = Memory.CollectablePool.Acquire<UnionDataList>();
                    buffer.PutFirst((byte)MessageType.PongMessage);
                    var res = ScheduleToSend(buffer);
                    if (res != SendResult.Ok)
                    {
                        Stop(new TextFail(TransportName, "Failed to send Pong message"), false);
                    }

                    return true;
                }
                default:
                    return base.ProcessInboundMessage(messageType, message);
            }
        }
    }
}