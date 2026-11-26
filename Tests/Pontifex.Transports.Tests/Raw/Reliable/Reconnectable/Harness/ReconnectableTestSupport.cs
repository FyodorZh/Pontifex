using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Actuarius.Memory;
using Operarius;
using Pontifex.Raw.Reliable;
using Pontifex.StopReasons;
using Pontifex.Utils;

namespace Pontifex.Tests.Raw.Reliable.Reconnectable.Harness
{
    /// <summary>
    /// Hand-driven periodic driver for both the reconnectable client and its server session logics.
    /// </summary>
    internal sealed class ManualReconnectableDriver : ILogicDriver<IPeriodicLogicDriverCtl>
    {
        private sealed class Entry
        {
            public ILogic<IPeriodicLogicDriverCtl> Logic = null!;
            public Control Control = null!;
            public bool Running;
        }

        private readonly List<Entry> _entries = new List<Entry>();

        public int StartCalls { get; private set; }

        public int FinishCalls { get; private set; }

        public bool IsRunning => _entries.Any(e => e.Running);

        public int RunningCount => _entries.Count(e => e.Running);

        public event Action<ILogic<IPeriodicLogicDriverCtl>>? LogicStopped;

        public event Action<Exception>? ErrorStream
        {
            add { }
            remove { }
        }

        public LogicStartResult Start(ILogic<IPeriodicLogicDriverCtl> logic)
        {
            StartCalls++;
            var control = new Control(this);
            var entry = new Entry { Logic = logic, Control = control };
            _entries.Add(entry);

            if (!logic.LogicStarted(control))
            {
                _entries.Remove(entry);
                return LogicStartResult.FailedToStart;
            }

            entry.Running = true;
            return LogicStartResult.Success;
        }

        public void Tick()
        {
            foreach (var entry in _entries.ToArray())
            {
                if (entry.Running)
                {
                    ((IPeriodicLogic)entry.Logic).LogicTick(entry.Control);
                }
            }
        }

        public void Stop()
        {
            foreach (var entry in _entries.ToArray())
            {
                StopEntry(entry);
            }
        }

        public Task Finish()
        {
            FinishCalls++;
            Stop();
            return Task.CompletedTask;
        }

        public Task WaitForFinish()
        {
            return Task.CompletedTask;
        }

        private void StopEntry(Entry entry)
        {
            if (!entry.Running)
            {
                return;
            }

            entry.Running = false;
            entry.Logic.LogicStopped();
            LogicStopped?.Invoke(entry.Logic);
        }

        private sealed class Control : IPeriodicLogicDriverCtl
        {
            private readonly ManualReconnectableDriver _owner;

            public Control(ManualReconnectableDriver owner)
            {
                _owner = owner;
            }

            public DateTime CurrentTime { get; } = new DateTime(2000, 1, 1);

            public TimeSpan Period => TimeSpan.FromMilliseconds(20);

            public void Stop()
            {
                var entry = _owner._entries.FirstOrDefault(e => ReferenceEquals(e.Control, this));
                if (entry != null)
                {
                    _owner.StopEntry(entry);
                }
            }
        }
    }

    /// <summary>
    /// A minimal <see cref="IEndPoint"/> carrying only the identity the reconnectable proxies forward.
    /// </summary>
    internal sealed class FakeReconnectableEndPoint : IEndPoint
    {
        public FakeReconnectableEndPoint(string address)
        {
            Address = address;
        }

        public string Address { get; }

        public bool Equals(IEndPoint? other)
        {
            return other is FakeReconnectableEndPoint endpoint && endpoint.Address == Address;
        }

        public override bool Equals(object? obj)
        {
            return obj is FakeReconnectableEndPoint endpoint && endpoint.Address == Address;
        }

        public override int GetHashCode()
        {
            return Address.GetHashCode();
        }

        public override string ToString()
        {
            return Address;
        }
    }

    /// <summary>
    /// Records the observable lifecycle and payloads of an application client handler.
    /// </summary>
    internal sealed class RecordingReconnectableClientHandler : IRawReliableClientHandler
    {
        public IRawReliableEndpoint? Endpoint { get; private set; }

        public int ConnectedCount { get; private set; }

        public List<int> Received { get; } = new List<int>();

        public List<string> Events { get; } = new List<string>();

        public StopReason? StopReason { get; private set; }

        public void OnConnected(IRawReliableEndpoint endPoint)
        {
            Endpoint = endPoint;
            ConnectedCount++;
            Events.Add("connected");
        }

        public void OnReceived(UnionDataList receivedBuffer)
        {
            try
            {
                if (receivedBuffer.Elements.Count > 0)
                {
                    Received.Add(receivedBuffer.Elements[0].Alias.IntValue);
                }
            }
            finally
            {
                receivedBuffer.Release();
            }
        }

        public void OnDisconnected(StopReason reason)
        {
            Events.Add("disconnected");
        }

        public void OnStopped(StopReason reason)
        {
            StopReason = reason;
            Events.Add("stopped");
        }
    }

    /// <summary>
    /// Records the observable lifecycle and payloads of an application server session handler.
    /// </summary>
    internal sealed class RecordingReconnectableServerHandler : IRawReliableServerHandler
    {
        public IRawReliableEndpoint? Endpoint { get; private set; }

        public int ConnectedCount { get; private set; }

        public List<int> Received { get; } = new List<int>();

        public List<string> Events { get; } = new List<string>();

        public StopReason? StopReason { get; private set; }

        public void OnConnected(IRawReliableEndpoint endPoint)
        {
            Endpoint = endPoint;
            ConnectedCount++;
            Events.Add("connected");
        }

        public void OnReceived(UnionDataList receivedBuffer)
        {
            try
            {
                if (receivedBuffer.Elements.Count > 0)
                {
                    Received.Add(receivedBuffer.Elements[0].Alias.IntValue);
                }
            }
            finally
            {
                receivedBuffer.Release();
            }
        }

        public void OnDisconnected(StopReason reason)
        {
            Events.Add("disconnected");
        }

        public void OnStopped(StopReason reason)
        {
            StopReason = reason;
            Events.Add("stopped");
        }
    }

    /// <summary>
    /// A recording server handler factory. Each admitted session receives a fresh handler.
    /// </summary>
    internal sealed class ReconnectableServerFactory : IRawReliableServerHandlerFactory<IRawReliableServerHandler>
    {
        private readonly Func<IEndPoint, IRawReliableServerHandler?> _create;

        public ReconnectableServerFactory(Func<IEndPoint, IRawReliableServerHandler?> create)
        {
            _create = create;
        }

        public int Calls { get; private set; }

        public IRawReliableServerHandler? TryCreate(IEndPoint remote)
        {
            Calls++;
            return _create(remote);
        }
    }

    /// <summary>
    /// Helpers for building and describing flat payloads in tests.
    /// </summary>
    internal static class ReconnectablePayload
    {
        public static UnionDataList CreateInt(int value)
        {
            var list = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
            list.PutLast(value);
            return list;
        }

        public static UnionDataList CreateString(string value)
        {
            var list = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
            list.PutLast(new UnionData(new MultiRefByteArray(Encoding.UTF8.GetBytes(value))));
            return list;
        }

        public static string[] Describe(UnionDataList data)
        {
            var result = new string[data.Elements.Count];
            for (int i = 0; i < data.Elements.Count; i++)
            {
                var element = data.Elements[i];
                switch (element.Type)
                {
                    case UnionDataType.Array:
                        result[i] = "s:" + Decode(element);
                        break;
                    case UnionDataType.Int:
                        result[i] = "i:" + element.Alias.IntValue;
                        break;
                    case UnionDataType.Short:
                        result[i] = "h:" + element.Alias.ShortValue;
                        break;
                    case UnionDataType.Byte:
                        result[i] = "b:" + element.Alias.ByteValue;
                        break;
                    default:
                        result[i] = element.Type.ToString();
                        break;
                }
            }

            return result;
        }

        private static string Decode(UnionData element)
        {
            var bytes = element.Bytes;
            if (bytes == null || bytes.Count == 0)
            {
                return string.Empty;
            }

            return Encoding.UTF8.GetString(bytes.ReadOnlyArray, bytes.Offset, bytes.Count);
        }
    }
}
