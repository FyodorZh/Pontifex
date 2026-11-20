using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Raw.Reliable;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Raw.Reliable.Reconnectable.Harness
{
    /// <summary>
    /// A synchronous in-memory reliable endpoint. <see cref="Deliver"/> receives ownership of an outbound
    /// payload; when it is not set, the payload is released.
    /// </summary>
    internal sealed class FakeReconnectableEndpoint : IRawReliableEndpoint
    {
        private readonly Action<StopReason> _onDisconnectRequested;

        public FakeReconnectableEndpoint(IEndPoint remote, int messageMaxByteSize,
            Action<StopReason> onDisconnectRequested)
        {
            RemoteEndPoint = remote;
            MessageMaxByteSize = messageMaxByteSize;
            _onDisconnectRequested = onDisconnectRequested;
        }

        public Action<UnionDataList>? Deliver { get; set; }

        /// <summary>
        /// Optional outbound rewrite. The transform takes ownership of the payload and returns the payload to
        /// deliver. Used to corrupt the reconnectable secret on the reattach request.
        /// </summary>
        public Func<UnionDataList, UnionDataList>? Transform { get; set; }

        public bool Connected { get; set; } = true;

        public List<UnionDataList> Sent { get; } = new List<UnionDataList>();

        public IEndPoint? RemoteEndPoint { get; }

        public int MessageMaxByteSize { get; }

        public bool IsConnected => Connected;

        public SendResult Send(UnionDataList bufferToSend)
        {
            if (bufferToSend == null)
            {
                return SendResult.InvalidMessage;
            }

            if (!Connected)
            {
                bufferToSend.Release();
                return SendResult.NotConnected;
            }

            Sent.Add(bufferToSend.Clone(MemoryRental.Shared.CollectablePool));

            var transform = Transform;
            UnionDataList outgoing = transform != null ? transform(bufferToSend) : bufferToSend;
            var deliver = Deliver;
            if (deliver != null)
            {
                deliver(outgoing);
            }
            else
            {
                outgoing.Release();
            }

            return SendResult.Ok;
        }

        public bool Disconnect(StopReason reason)
        {
            if (!Connected)
            {
                return false;
            }

            Connected = false;
            _onDisconnectRequested(reason);
            return true;
        }

        public void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
        {
        }

        public void ReleaseSent()
        {
            foreach (var payload in Sent)
            {
                payload.Release();
            }

            Sent.Clear();
        }
    }

    /// <summary>
    /// A scripted <see cref="IRawReliableClient"/> representing one physical connection attempt.
    /// </summary>
    internal sealed class FakeReconnectableClient : IRawReliableClient
    {
        private bool _started;
        private bool _terminal;
        private Action<StopReason>? _onStopped;

        public IRawReliableClientHandler? Handler { get; private set; }

        public FakeReconnectableEndpoint? Endpoint { get; private set; }

        public Action<FakeReconnectableEndpoint>? ConfigureEndpoint { get; set; }

        public TransportType Type => TransportType.RawReliable;

        public string Name => "fake-reconnectable-client";

        public bool IsValid { get; private set; } = true;

        public bool IsStarted => _started;

        public ILogger Log { get; } = new Logger([]);

        public IMemoryRental Memory => MemoryRental.Shared;

        public int MessageMaxByteSize => 4096;

        public bool Init(IRawReliableClientHandler handler)
        {
            if (Handler != null)
            {
                return false;
            }

            Handler = handler;
            return true;
        }

        public bool Start(Action<StopReason> onStopped)
        {
            if (!IsValid || _started)
            {
                return false;
            }

            _started = true;
            _onStopped = onStopped;
            return true;
        }

        public void Connect()
        {
            if (!_started || Handler == null)
            {
                throw new InvalidOperationException("The fake client is not started.");
            }

            var endpoint = new FakeReconnectableEndpoint(new FakeReconnectableEndPoint("client"),
                MessageMaxByteSize, OnDisconnectRequested);
            ConfigureEndpoint?.Invoke(endpoint);
            Endpoint = endpoint;
            Handler.OnConnected(endpoint);
        }

        public void DeliverToClient(UnionDataList data)
        {
            Handler!.OnReceived(data);
        }

        public void DropFromRemote(StopReason reason)
        {
            if (_terminal)
            {
                return;
            }

            _terminal = true;
            _started = false;
            if (Endpoint != null)
            {
                Endpoint.Connected = false;
            }

            Handler?.OnDisconnected(reason);
            Handler?.OnStopped(reason);
            _onStopped?.Invoke(reason);
        }

        public bool Stop(StopReason? reason = null)
        {
            DropFromRemote(reason ?? new Unknown(Name));
            return true;
        }

        public void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
        {
        }

        private void OnDisconnectRequested(StopReason reason)
        {
            DropFromRemote(reason);
        }
    }

    /// <summary>
    /// A scripted <see cref="IRawReliableServer"/> whose physical sessions are created and driven by the
    /// test.
    /// </summary>
    internal sealed class FakeReconnectableServer : IRawReliableServer
    {
        private bool _started;
        private bool _terminal;
        private Action<StopReason>? _onStopped;

        public IRawReliableServerHandlerFactory<IRawReliableServerHandler>? Factory { get; private set; }

        public List<FakeReconnectableServerSession> Sessions { get; } = new List<FakeReconnectableServerSession>();

        public TransportType Type => TransportType.RawReliable;

        public string Name => "fake-reconnectable-server";

        public bool IsValid { get; private set; } = true;

        public bool IsStarted => _started;

        public ILogger Log { get; } = new Logger([]);

        public IMemoryRental Memory => MemoryRental.Shared;

        public int MessageMaxByteSize => 4096;

        public bool Init(IRawReliableServerHandlerFactory<IRawReliableServerHandler> factory)
        {
            if (_started || Factory != null)
            {
                return false;
            }

            Factory = factory;
            return true;
        }

        public bool Start(Action<StopReason> onStopped)
        {
            if (!IsValid || _started)
            {
                return false;
            }

            _started = true;
            _onStopped = onStopped;
            return true;
        }

        public FakeReconnectableServerSession Connect()
        {
            if (!_started || Factory == null)
            {
                throw new InvalidOperationException("The fake server is not started.");
            }

            var handler = Factory.TryCreate(new FakeReconnectableEndPoint("remote"));
            if (handler == null)
            {
                throw new InvalidOperationException("The fake server factory returned null.");
            }

            var session = new FakeReconnectableServerSession(handler, MessageMaxByteSize);
            Sessions.Add(session);
            handler.OnConnected(session.Endpoint);
            return session;
        }

        public bool Stop(StopReason? reason = null)
        {
            var stopReason = reason ?? new Unknown(Name);
            if (_terminal)
            {
                return true;
            }

            _terminal = true;
            _started = false;
            foreach (var session in Sessions)
            {
                session.Stop(stopReason);
            }

            _onStopped?.Invoke(stopReason);
            return true;
        }

        public void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
        {
        }
    }

    /// <summary>
    /// One physical server session: the reconnectable inner handler plus its endpoint.
    /// </summary>
    internal sealed class FakeReconnectableServerSession
    {
        private bool _terminal;

        public FakeReconnectableServerSession(IRawReliableServerHandler handler, int messageMaxByteSize)
        {
            Handler = handler;
            Endpoint = new FakeReconnectableEndpoint(new FakeReconnectableEndPoint("remote"),
                messageMaxByteSize, Stop);
        }

        public IRawReliableServerHandler Handler { get; }

        public FakeReconnectableEndpoint Endpoint { get; }

        public int HandlerDisconnectedCount { get; private set; }

        public int HandlerStoppedCount { get; private set; }

        public void DeliverToServer(UnionDataList data)
        {
            Handler.OnReceived(data);
        }

        public void Stop(StopReason reason)
        {
            if (_terminal)
            {
                return;
            }

            _terminal = true;
            Endpoint.Connected = false;
            HandlerDisconnectedCount++;
            Handler.OnDisconnected(reason);
            HandlerStoppedCount++;
            Handler.OnStopped(reason);
        }

        public void Release()
        {
            Endpoint.ReleaseSent();
        }
    }
}
