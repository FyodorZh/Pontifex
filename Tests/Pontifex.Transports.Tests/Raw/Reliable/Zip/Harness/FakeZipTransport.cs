using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Raw.Reliable;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Raw.Reliable.Zip.Harness
{
    /// <summary>
    /// A synchronous in-memory reliable endpoint. <see cref="Deliver"/> receives ownership of an outbound
    /// payload; when it is not set, the payload is released.
    /// </summary>
    internal sealed class FakeZipEndpoint : IRawReliableEndpoint
    {
        private readonly Action<StopReason> _onDisconnectRequested;

        public FakeZipEndpoint(IEndPoint remote, int messageMaxByteSize,
            Action<StopReason> onDisconnectRequested)
        {
            RemoteEndPoint = remote;
            MessageMaxByteSize = messageMaxByteSize;
            _onDisconnectRequested = onDisconnectRequested;
        }

        public Action<UnionDataList>? Deliver { get; set; }

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

            var deliver = Deliver;
            if (deliver != null)
            {
                deliver(bufferToSend);
            }
            else
            {
                bufferToSend.Release();
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
    /// A scripted <see cref="IRawReliableClient"/> whose connection and inbound traffic are driven by the
    /// test.
    /// </summary>
    internal sealed class FakeZipClient : IRawReliableClient
    {
        private bool _started;
        private bool _terminal;
        private Action<StopReason>? _onStopped;

        public IRawReliableClientHandler? Handler { get; private set; }

        public FakeZipEndpoint? Endpoint { get; private set; }

        public Action<FakeZipEndpoint>? ConfigureEndpoint { get; set; }

        public TransportType Type => TransportType.RawReliable;

        public string Name => "fake-zip-client";

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

            var endpoint = new FakeZipEndpoint(new FakeZipEndPoint("client"), MessageMaxByteSize,
                OnDisconnectRequested);
            ConfigureEndpoint?.Invoke(endpoint);
            Endpoint = endpoint;
            Handler.OnConnected(endpoint);
        }

        public void DeliverToClient(UnionDataList data)
        {
            Handler!.OnReceived(data);
        }

        public bool Stop(StopReason? reason = null)
        {
            if (!_started && _terminal)
            {
                return true;
            }

            var stopReason = reason ?? new Unknown(Name);
            _started = false;
            _terminal = true;
            if (Endpoint != null)
            {
                Endpoint.Connected = false;
            }

            Handler?.OnStopped(stopReason);
            _onStopped?.Invoke(stopReason);
            return true;
        }

        public void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
        {
        }

        private void OnDisconnectRequested(StopReason reason)
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
    }

    /// <summary>
    /// A scripted <see cref="IRawReliableServer"/> whose sessions are created and driven by the test.
    /// </summary>
    internal sealed class FakeZipServer : IRawReliableServer
    {
        private bool _started;
        private bool _terminal;
        private Action<StopReason>? _onStopped;

        public IRawReliableServerHandlerFactory<IRawReliableServerHandler>? Factory { get; private set; }

        public List<FakeZipServerSession> Sessions { get; } = new List<FakeZipServerSession>();

        public TransportType Type => TransportType.RawReliable;

        public string Name => "fake-zip-server";

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

        public FakeZipServerSession Connect()
        {
            if (!_started || Factory == null)
            {
                throw new InvalidOperationException("The fake server is not started.");
            }

            var handler = Factory.TryCreate(new FakeZipEndPoint("remote"));
            if (handler == null)
            {
                throw new InvalidOperationException("The fake server factory returned null.");
            }

            var session = new FakeZipServerSession(handler, MessageMaxByteSize);
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
    /// One fake server session: a handler plus its endpoint.
    /// </summary>
    internal sealed class FakeZipServerSession
    {
        private bool _terminal;

        public FakeZipServerSession(IRawReliableServerHandler handler, int messageMaxByteSize)
        {
            Handler = handler;
            Endpoint = new FakeZipEndpoint(new FakeZipEndPoint("remote"), messageMaxByteSize, Stop);
        }

        public IRawReliableServerHandler Handler { get; }

        public FakeZipEndpoint Endpoint { get; }

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
            Handler.OnDisconnected(reason);
            Handler.OnStopped(reason);
        }

        public void Release()
        {
            Endpoint.ReleaseSent();
        }
    }
}
