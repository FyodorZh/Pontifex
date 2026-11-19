using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Raw.Reliable.Logger;
using Scriba;

namespace Pontifex.Raw.Reliable.NoAck.Logger
{
    /// <summary>
    /// Logging wrapper for a reliable no-acknowledgement raw server transport.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Every transport call and every handler callback is logged to the wrapped
    /// transport's <see cref="ITransport.Log"/>.</item>
    /// <item>Each admitted connection receives its own
    /// <see cref="RawReliableServerHandlerLogger"/>, because a no-acknowledgement factory is
    /// invoked once per connection.</item>
    /// <item>The wrapper forwards the factory result unchanged: a null handler still rejects the
    /// connection, and an exception is logged and rethrown so the transport keeps its own
    /// rejection semantics.</item>
    /// </list>
    /// </remarks>
    public sealed class RawReliableNoAckServerLogger :
        IRawReliableNoAckServer,
        IRawReliableNoAckServerHandlerFactory<IRawReliableNoAckServerHandler>
    {
        private readonly IRawReliableNoAckServer _core;

        private IRawReliableNoAckServerHandlerFactory<IRawReliableNoAckServerHandler>? _userFactory;

        /// <summary>
        /// Gets the transport type of the wrapped transport.
        /// </summary>
        public TransportType Type => TransportType.RawReliableNoAck;

        /// <summary>
        /// Initializes a new logging wrapper around the given server transport.
        /// </summary>
        /// <param name="core">The transport that performs the actual work.</param>
        public RawReliableNoAckServerLogger(IRawReliableNoAckServer core)
        {
            _core = core;
        }

        string ITransport.Name => _core.Name;

        bool ITransport.IsValid => _core.IsValid;

        bool ITransport.IsStarted => _core.IsStarted;

        bool ITransport.Start(Action<StopReason> onStopped)
        {
            _core.Log.i("Start()");
            return _core.Start(reason =>
            {
                _core.Log.i("OnStopped(" + reason + ")");
                onStopped(reason);
            });
        }

        bool ITransport.Stop(StopReason? reason)
        {
            _core.Log.i("Stop(" + reason + ")");
            return _core.Stop(reason);
        }

        ILogger ITransport.Log => _core.Log;

        IMemoryRental ITransport.Memory => _core.Memory;

        /// <summary>
        /// Appends the transport controls of the wrapped transport to the destination list.
        /// </summary>
        /// <param name="dst">The destination list.</param>
        /// <param name="predicate">Optional filter applied to each control.</param>
        public void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
        {
            _core.GetControls(dst, predicate);
        }

        bool IRawReliableNoAckServer.Init(
            IRawReliableNoAckServerHandlerFactory<IRawReliableNoAckServerHandler> factory)
        {
            _core.Log.i("Init()");
            _userFactory = factory;
            return _core.Init(this);
        }

        int IRawTransport.MessageMaxByteSize => _core.MessageMaxByteSize;

        /// <summary>
        /// Logs the factory call and forwards it to the user factory, wrapping the created
        /// session handler so that all of its callbacks are logged.
        /// </summary>
        /// <param name="remote">The remote endpoint of the connecting client.</param>
        /// <returns>Null when the user factory rejects the connection; otherwise a logged
        /// wrapper over the created session handler.</returns>
        public IRawReliableNoAckServerHandler? TryCreate(IEndPoint remote)
        {
            if (_userFactory == null)
            {
                _core.Log.e("UserFactory is null");
                return null;
            }

            _core.Log.i("UserFactory.TryCreate(" + remote + ")");
            IRawReliableNoAckServerHandler? handler;
            try
            {
                handler = _userFactory.TryCreate(remote);
            }
            catch (Exception ex)
            {
                _core.Log.e("UserFactory.TryCreate failed: " + ex);
                throw;
            }

            if (handler == null)
            {
                return null;
            }

            _core.Log.i("UserFactory returned the session handler");
            return new RawReliableServerHandlerLogger(_core.Log, handler);
        }
    }
}
