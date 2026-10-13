using System;
using Actuarius.Concurrent;
using Pontifex.StopReasons;
using Pontifex.Utils;

namespace Pontifex.Raw.Reliable
{
    /// <summary>
    /// Abstract base class for a reliable server endpoint. Extends <see cref="RawReliableEndpoint"/> with an
    /// idle-session watchdog: inbound traffic refreshes a timestamp, and the driver-invoked
    /// <see cref="PeriodicTick"/> stops the endpoint when no inbound message arrives within the configured
    /// timeout.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Thread safety</b>: All public and protected instance members are safe to call
    /// concurrently from any thread. The watchdog timestamp is accessed through a thread-safe atomic box and
    /// the stop request is idempotent.</description></item>
    /// <item><description><b>Other notes</b>: Designed to work with a reliable client endpoint that sends
    /// ping frames, which this endpoint answers with pong frames; the server disconnect timeout is intended to
    /// equal the client disconnect timeout. Overrides of OnConnected and ProcessInboundMessage must call the
    /// base implementation to keep the watchdog working.</description></item>
    /// </list>
    /// </remarks>
    public abstract class RawReliableServerEndpoint : RawReliableEndpoint
    {
        private readonly TimeSpan _disconnectTimeout;
        private readonly AtomicBox<DateTime> _lastInboundMessage = new();

        private volatile bool _timeInitialized;

        /// <summary>
        /// Initializes a new reliable server endpoint owned by the given transport, with the idle-session
        /// timeout.
        /// </summary>
        /// <param name="owner">The owning transport that provides the transport name, logging, and memory
        /// rental.</param>
        /// <param name="remoteEndPoint">The remote endpoint address, or <see langword="null"/> when it is
        /// unknown.</param>
        /// <param name="handler">The handler that receives inbound messages and connection lifecycle
        /// notifications.</param>
        /// <param name="messageMaxByteSize">The inclusive maximum message size, in bytes, admitted by this
        /// endpoint; must be at least 1.</param>
        /// <param name="bufferCapacityBytes">The byte budget for messages accepted by this endpoint but not
        /// yet transmitted; must be at least 1.</param>
        /// <param name="disconnectTimeout">The maximum time without an inbound message before the endpoint is
        /// stopped as timed out; must be positive.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Preconditions/Postconditions</b>: <paramref name="disconnectTimeout"/> must
        /// be greater than zero.</description></item>
        /// <item><description><b>Other notes</b>: The owning transport and the handler are retained, not
        /// disposed, by the endpoint.</description></item>
        /// </list>
        /// </remarks>
        protected RawReliableServerEndpoint(IRawReliableTransport owner, IEndPoint remoteEndPoint, IRawReliableServerHandler handler,
            int messageMaxByteSize, long bufferCapacityBytes,
            TimeSpan disconnectTimeout)
            : base(owner, remoteEndPoint, handler, messageMaxByteSize, bufferCapacityBytes)
        {
            if (disconnectTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentException(nameof(disconnectTimeout));
            }
            _disconnectTimeout = disconnectTimeout;
        }

        /// <summary>
        /// Advances the idle-session watchdog, stopping the endpoint with a timeout when no inbound message has
        /// arrived within the configured disconnect timeout.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call concurrently from any
        /// thread.</description></item>
        /// <item><description><b>Other notes</b>: Intended to be called periodically by the transport driver.
        /// The call is a no-op unless the endpoint is connected and its watchdog timestamp has been
        /// initialized. The effective timeout is the configured timeout rounded up to the tick
        /// interval.</description></item>
        /// </list>
        /// </remarks>
        public void PeriodicTick()
        {
            if (IsLowLevelConnected && _timeInitialized)
            {
                if (DateTime.Now - _lastInboundMessage.Value > _disconnectTimeout)
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
                    _lastInboundMessage.Value = DateTime.Now;
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
            _lastInboundMessage.Value = DateTime.Now;
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