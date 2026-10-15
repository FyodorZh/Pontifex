using System;
using System.Threading.Tasks;
using Actuarius.Concurrent;
using Pontifex.StopReasons;
using Pontifex.Utils;

namespace Pontifex.Raw.Reliable
{
    /// <summary>
    /// Abstract base class for a reliable client endpoint. Extends <see cref="RawReliableEndpoint"/> with an
    /// optional keep-alive mechanism that sends ping frames and stops the endpoint when no pong frame is
    /// received within the configured timeout.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Thread safety</b>: All public and protected instance members are safe to call
    /// concurrently from any thread. Inbound handling accesses the pong timestamp through a thread-safe atomic
    /// box, and the keep-alive loop runs on a thread-pool thread.</description></item>
    /// <item><description><b>Other notes</b>: Derived classes implement the inherited abstract connect and
    /// disconnect hooks. Overrides of OnConnected and ProcessInboundMessage must call the base implementation
    /// to keep the keep-alive timestamp current.</description></item>
    /// </list>
    /// </remarks>
    public abstract class RawReliableClientEndpoint : RawReliableEndpoint
    {
        private readonly PingOptions? _pingOptions;
        
        private readonly AtomicBox<DateTime> _lastPongReceiveTime = new ();

        /// <summary>
        /// Initializes a new reliable client endpoint owned by the given transport, with an optional keep-alive
        /// configuration.
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
        /// <param name="pingOptions">The keep-alive configuration, or <see langword="null"/> to disable
        /// keep-alive.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Preconditions/Postconditions</b>: When <paramref name="pingOptions"/> is
        /// provided, its PingPeriod must be positive and its DisconnectTimeout must be at least twice its
        /// PingPeriod.</description></item>
        /// <item><description><b>Other notes</b>: The owning transport and the handler are retained, not
        /// disposed, by the endpoint.</description></item>
        /// </list>
        /// </remarks>
        protected RawReliableClientEndpoint(IRawReliableTransport owner, IEndPoint remoteEndPoint, IRawReliableClientHandler handler, 
            int messageMaxByteSize, long bufferCapacityBytes,
            PingOptions? pingOptions)
            : base(owner.Name, remoteEndPoint, handler, messageMaxByteSize, bufferCapacityBytes, owner.Memory, owner.Log)
        {
            if (pingOptions != null &&
                (pingOptions.Value.PingPeriod <= TimeSpan.Zero ||
                 pingOptions.Value.PingPeriod * 2 > pingOptions.Value.DisconnectTimeout))
            {
                throw new InvalidOperationException();
            }

            _pingOptions = pingOptions;
        }

        /// <summary>
        /// Records the initial pong receive time and, when keep-alive is configured, starts the background
        /// keep-alive loop.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Called on the serial action queue; the keep-alive loop
        /// runs on a thread-pool thread.</description></item>
        /// <item><description><b>Other notes</b>: The setup is deferred by one action; if the endpoint is no
        /// longer connected when that action runs, no keep-alive loop is started. Overrides must call the base
        /// implementation to keep keep-alive working.</description></item>
        /// </list>
        /// </remarks>
        protected override void OnConnected()
        {
            _actionQueue.Schedule(() =>
            {
                if (IsLowLevelConnected)
                {
                    _lastPongReceiveTime.Value = DateTime.Now;
                    if (_pingOptions != null)
                    {
                        Task.Run(async () =>
                        {
                            try
                            {
                                DateTime lastPingSentTime = new();
                                while (IsLowLevelConnected)
                                {
                                    DateTime now = DateTime.Now;
                                    if (now - _lastPongReceiveTime.Value > _pingOptions.Value.DisconnectTimeout)
                                    {
                                        Stop(new TimeOut(TransportName), false);
                                        break;
                                    }

                                    if (now - lastPingSentTime > _pingOptions.Value.PingPeriod)
                                    {
                                        var buffer = Memory.CollectablePool.Acquire<UnionDataList>();
                                        buffer.PutFirst((byte)MessageType.PingMessage);
                                        var res = ScheduleToSend(buffer);
                                        if (res != SendResult.Ok)
                                        {
                                            Stop(new TextFail(TransportName, $"Ping failed with {res}"), false);
                                            break;
                                        }

                                        lastPingSentTime = now;
                                    }

                                    await Task.Delay(100);
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.wtf(ex);
                                Stop(new ExceptionFail(TransportName, ex), false);
                            }
                        });
                    }
                }
            });
        }

        /// <summary>
        /// Refreshes the keep-alive timestamp for a pong frame and delegates all other frame types to the base
        /// implementation.
        /// </summary>
        /// <param name="messageType">The frame type popped from the message.</param>
        /// <param name="message">The frame body. A pong frame is released by this method.</param>
        /// <returns><see langword="true"/> when the frame was handled; otherwise
        /// <see langword="false"/>.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Invoked on the caller's thread; the pong timestamp is
        /// accessed through a thread-safe atomic box.</description></item>
        /// <item><description><b>Other notes</b>: Overrides must delegate pong frames to the base implementation
        /// to keep the keep-alive timestamp current.</description></item>
        /// </list>
        /// </remarks>
        protected override bool ProcessInboundMessage(MessageType messageType, UnionDataList message)
        {
            switch (messageType)
            {
                case MessageType.PongMessage:
                    _lastPongReceiveTime.Value = DateTime.Now;
                    message.Release();
                    return true;
                default:
                    return base.ProcessInboundMessage(messageType, message);
            }
        }

        /// <summary>
        /// Configures the keep-alive ping behavior of a reliable client endpoint.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The endpoint constructor validates the combination of these
        /// values when the options are supplied.</description></item>
        /// </list>
        /// </remarks>
        protected struct PingOptions
        {
            /// <summary>
            /// The maximum time without a pong reply before the endpoint is stopped as timed out.
            /// </summary>
            public readonly TimeSpan DisconnectTimeout;

            /// <summary>
            /// The nominal interval at which ping frames are sent.
            /// </summary>
            public readonly TimeSpan PingPeriod;

            /// <summary>
            /// Initializes a new keep-alive configuration.
            /// </summary>
            /// <param name="disconnectTimeout">The maximum time without a pong reply before the endpoint is
            /// stopped as timed out.</param>
            /// <param name="pingPeriod">The nominal interval at which ping frames are sent.</param>
            /// <remarks>
            /// <list type="bullet">
            /// <item><description><b>Other notes</b>: The values are not validated here; the endpoint
            /// constructor validates them.</description></item>
            /// </list>
            /// </remarks>
            public PingOptions(TimeSpan disconnectTimeout, TimeSpan pingPeriod)
            {
                DisconnectTimeout = disconnectTimeout;
                PingPeriod = pingPeriod;
            }
        }
    }
}