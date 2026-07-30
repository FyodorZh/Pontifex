// using System;
// using System.Collections.Generic;
// using System.Threading;
// using Actuarius.Memory;
// using Pontifex.NoAck.Raw;
// using Pontifex.Utils;
// using Scriba;
//
// namespace Pontifex.Converters
// {
//     public class NoAckRawUnreliableToNoAckRawReliableConverter : ITransportConverter
//     {
//         public TransportType From => TransportType.NoAckRawUnreliable;
//         public TransportType To => TransportType.NoAckRawReliable;
//
//         public Func<ITransport> Convert(Func<ITransport> innerTransportCtor, IMemoryRental? memoryOverride = null, ILogger? loggerOverride = null)
//         {
//             return () =>
//             {
//                 var transport = innerTransportCtor();
//                 if (transport is INoAckRawUnreliableClient client)
//                     return new ReliableClientWrapper(client, memoryOverride, loggerOverride);
//                 if (transport is INoAckRawUnreliableServer server)
//                     return new ReliableServerWrapper(server, memoryOverride, loggerOverride);
//
//                 throw new ArgumentException($"Transport must implement {nameof(INoAckRawUnreliableClient)} or {nameof(INoAckRawUnreliableServer)}", nameof(transport));
//             };
//         }
//
//         private static class MessageType
//         {
//             public const byte Data = 0;
//             public const byte Ack = 1;
//         }
//
//         private const int HeaderSize = 1 + 4;
//         private static readonly TimeSpan RetransmitInterval = TimeSpan.FromMilliseconds(200);
//         private static readonly TimeSpan RetransmitTimeout = TimeSpan.FromMilliseconds(500);
//         private const int MaxRecvWindow = 2048;
//
//         private sealed class ReliableClientWrapper : INoAckRawReliableClient
//         {
//             private readonly INoAckRawUnreliableClient _inner;
//             private readonly IMemoryRental _memory;
//             private readonly ILogger _log;
//
//             private int _nextSendSeq;
//             private int _expectedRecvSeq;
//
//             private readonly object _sendLock = new();
//             private readonly Dictionary<int, UnionDataList> _sendBuffer = new();
//             private readonly Dictionary<int, DateTime> _sendTimes = new();
//
//             private readonly object _recvLock = new();
//             private readonly SortedDictionary<int, UnionDataList> _recvBuffer = new();
//             private readonly List<UnionDataList> _deliveryCache = new();
//
//             private Thread? _retransmitThread;
//             private volatile bool _stopped;
//             private readonly AutoResetEvent _retransmitEvent = new AutoResetEvent(false);
//             
//             public TransportType Type => TransportType.NoAckRawReliable;
//
//             public ReliableClientWrapper(INoAckRawUnreliableClient inner, IMemoryRental? memoryOverride, ILogger? loggerOverride)
//             {
//                 _inner = inner;
//                 _log = loggerOverride ?? inner.Log;
//                 _memory = memoryOverride ?? inner.Memory;
//
//                 _inner.OnReceived += OnInnerReceived;
//             }
//
//             public event Action<UnionDataList>? OnReceived;
//
//             public int MessageMaxByteSize => _inner.MessageMaxByteSize - HeaderSize;
//
//             public string Name => _inner.Name;
//             public bool IsValid => _inner.IsValid;
//             public bool IsStarted => _inner.IsStarted;
//             public ILogger Log => _log;
//             public IMemoryRental Memory => _memory;
//             
//             public void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
//             {
//             }
//
//             public bool Start(Action<StopReason> onStopped)
//             {
//                 _stopped = false;
//
//                 if (!_inner.Start(onStopped))
//                 {
//                     _stopped = true;
//                     return false;
//                 }
//
//                 _retransmitThread = new Thread(RetransmitLoop)
//                 {
//                     IsBackground = true,
//                     Name = "ReliableClientWrapper.Retransmit"
//                 };
//                 _retransmitThread.Start();
//                 return true;
//             }
//
//             public bool Stop(StopReason? reason = null)
//             {
//                 _stopped = true;
//                 _retransmitEvent.Set();
//
//                 lock (_sendLock)
//                 {
//                     foreach (var msg in _sendBuffer.Values)
//                         msg.Release();
//                     _sendBuffer.Clear();
//                     _sendTimes.Clear();
//                     _nextSendSeq = 0;
//                 }
//
//                 lock (_recvLock)
//                 {
//                     foreach (var msg in _recvBuffer.Values)
//                         msg.Release();
//                     _recvBuffer.Clear();
//                     _expectedRecvSeq = 0;
//                 }
//
//                 _retransmitThread?.Join();
//                 return _inner.Stop(reason);
//             }
//
//             public SendResult Send(UnionDataList message)
//             {
//                 if (message.GetDataSize() > MessageMaxByteSize)
//                 {
//                     message.Release();
//                     return SendResult.MessageTooBig;
//                 }
//
//                 int seq;
//                 lock (_sendLock)
//                 {
//                     seq = _nextSendSeq++;
//                 }
//
//                 message.Acquire();
//                 message.PutFirst(seq);
//                 message.PutFirst(MessageType.Data);
//
//                 lock (_sendLock)
//                 {
//                     _sendBuffer[seq] = message;
//                     _sendTimes[seq] = DateTime.UtcNow;
//                 }
//
//                 _retransmitEvent.Set();
//
//                 message.Acquire();
//                 var result = _inner.TrySend(message);
//
//                 if (result != SendResult.Ok && result != SendResult.BufferOverflow)
//                 {
//                     lock (_sendLock)
//                     {
//                         if (_sendBuffer.TryGetValue(seq, out var buffered))
//                         {
//                             buffered.Release();
//                             _sendBuffer.Remove(seq);
//                             _sendTimes.Remove(seq);
//                         }
//                     }
//                 }
//
//                 return SendResult.Ok;
//             }
//
//             private void OnInnerReceived(UnionDataList message)
//             {
//                 byte type;
//                 if (!message.TryPopFirst(out type))
//                 {
//                     message.Release();
//                     return;
//                 }
//
//                 int seq;
//                 if (!message.TryPopFirst(out seq))
//                 {
//                     message.Release();
//                     return;
//                 }
//
//                 if (type == MessageType.Ack)
//                 {
//                     lock (_sendLock)
//                     {
//                         if (_sendBuffer.TryGetValue(seq, out var buffered))
//                         {
//                             buffered.Release();
//                             _sendBuffer.Remove(seq);
//                             _sendTimes.Remove(seq);
//                         }
//                     }
//                     message.Release();
//                 }
//                 else if (type == MessageType.Data)
//                 {
//                     SendAck(seq);
//
//                     var handler = OnReceived;
//                     if (handler == null)
//                     {
//                         message.Release();
//                         return;
//                     }
//
//                     DeliverOrBuffer(seq, message, handler);
//                 }
//                 else
//                 {
//                     message.Release();
//                 }
//             }
//
//             private void SendAck(int seq)
//             {
//                 var ack = _memory.SmallObjectsPool.GetPool<UnionDataList>().Acquire();
//                 ack.PutFirst(seq);
//                 ack.PutFirst(MessageType.Ack);
//                 _inner.TrySend(ack);
//             }
//
//             private void DeliverOrBuffer(int seq, UnionDataList message, Action<UnionDataList> deliver)
//             {
//                 uint delta = (uint)unchecked(seq - _expectedRecvSeq);
//
//                 if ((int)delta < 0)
//                 {
//                     message.Release();
//                     return;
//                 }
//
//                 if (delta > MaxRecvWindow)
//                 {
//                     message.Release();
//                     return;
//                 }
//
//                 lock (_recvLock)
//                 {
//                     _deliveryCache.Clear();
//
//                     if (seq == _expectedRecvSeq)
//                     {
//                         _deliveryCache.Add(message);
//                         _expectedRecvSeq++;
//
//                         while (_recvBuffer.TryGetValue(_expectedRecvSeq, out var buffered))
//                         {
//                             _recvBuffer.Remove(_expectedRecvSeq);
//                             _deliveryCache.Add(buffered);
//                             _expectedRecvSeq++;
//                         }
//                     }
//                     else
//                     {
//                         if (!_recvBuffer.ContainsKey(seq))
//                         {
//                             message.Acquire();
//                             _recvBuffer[seq] = message;
//                         }
//                         message.Release();
//                     }
//                 }
//
//                 try
//                 {
//                     foreach (var msg in _deliveryCache)
//                         deliver(msg);
//                 }
//                 finally
//                 {
//                     _deliveryCache.Clear();
//                 }
//             }
//
//             private void RetransmitLoop()
//             {
//                 while (!_stopped)
//                 {
//                     _retransmitEvent.WaitOne(RetransmitInterval);
//
//                     if (_stopped)
//                         break;
//
//                     List<(UnionDataList msg, int seq)> toRetransmit;
//                     lock (_sendLock)
//                     {
//                         var now = DateTime.UtcNow;
//                         toRetransmit = new List<(UnionDataList, int)>();
//
//                         foreach (var kvp in _sendTimes)
//                         {
//                             if (now - kvp.Value > RetransmitTimeout)
//                             {
//                                 if (_sendBuffer.TryGetValue(kvp.Key, out var msg))
//                                 {
//                                     msg.Acquire();
//                                     toRetransmit.Add((msg, kvp.Key));
//                                 }
//                             }
//                         }
//
//                         foreach (var (_, seq) in toRetransmit)
//                             _sendTimes[seq] = now;
//                     }
//
//                     foreach (var (msg, _) in toRetransmit)
//                     {
//                         _inner.TrySend(msg);
//                     }
//                 }
//             }
//         }
//
//         private sealed class ReliableServerWrapper : INoAckRawReliableServer
//         {
//             private readonly INoAckRawUnreliableServer _inner;
//             private readonly IMemoryRental _memory;
//             private readonly ILogger _log;
//
//             private readonly object _lock = new();
//             private readonly Dictionary<IEndPoint, int> _nextSendSeqs = new();
//             private readonly Dictionary<IEndPoint, Dictionary<int, UnionDataList>> _sendBuffers = new();
//             private readonly Dictionary<IEndPoint, Dictionary<int, DateTime>> _sendTimes = new();
//
//             private readonly Dictionary<IEndPoint, int> _expectedRecvSeqs = new();
//             private readonly Dictionary<IEndPoint, SortedDictionary<int, UnionDataList>> _recvBuffers = new();
//             private readonly List<(IEndPoint, UnionDataList)> _deliveryCache = new();
//
//             private Thread? _retransmitThread;
//             private volatile bool _stopped;
//             private readonly AutoResetEvent _retransmitEvent = new AutoResetEvent(false);
//
//             public TransportType Type => TransportType.NoAckRawReliable;
//
//             public ReliableServerWrapper(INoAckRawUnreliableServer inner, IMemoryRental? memoryOverride, ILogger? loggerOverride)
//             {
//                 _inner = inner;
//                 _log = loggerOverride ?? inner.Log;
//                 _memory = memoryOverride ?? inner.Memory;
//
//                 _inner.OnReceived += OnInnerReceived;
//             }
//
//             public event Action<IEndPoint, UnionDataList>? OnReceived;
//
//             public int MessageMaxByteSize => _inner.MessageMaxByteSize - HeaderSize;
//
//             public string Name => _inner.Name;
//             public bool IsValid => _inner.IsValid;
//             public bool IsStarted => _inner.IsStarted;
//             public ILogger Log => _log;
//             public IMemoryRental Memory => _memory;
//             
//             public void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
//             {
//             }
//
//             public bool Start(Action<StopReason> onStopped)
//             {
//                 _stopped = false;
//
//                 if (!_inner.Start(onStopped))
//                 {
//                     _stopped = true;
//                     return false;
//                 }
//
//                 _retransmitThread = new Thread(RetransmitLoop)
//                 {
//                     IsBackground = true,
//                     Name = "ReliableServerWrapper.Retransmit"
//                 };
//                 _retransmitThread.Start();
//                 return true;
//             }
//
//             public bool Stop(StopReason? reason = null)
//             {
//                 _stopped = true;
//                 _retransmitEvent.Set();
//
//                 lock (_lock)
//                 {
//                     foreach (var buffer in _sendBuffers.Values)
//                         foreach (var msg in buffer.Values)
//                             msg.Release();
//                     _sendBuffers.Clear();
//                     _sendTimes.Clear();
//                     _nextSendSeqs.Clear();
//
//                     foreach (var buffer in _recvBuffers.Values)
//                         foreach (var msg in buffer.Values)
//                             msg.Release();
//                     _recvBuffers.Clear();
//                     _expectedRecvSeqs.Clear();
//                 }
//
//                 _retransmitThread?.Join();
//                 return _inner.Stop(reason);
//             }
//
//             public SendResult Send(IEndPoint destination, UnionDataList message)
//             {
//                 if (message.GetDataSize() > MessageMaxByteSize)
//                 {
//                     message.Release();
//                     return SendResult.MessageTooBig;
//                 }
//
//                 int seq;
//                 lock (_lock)
//                 {
//                     if (!_nextSendSeqs.TryGetValue(destination, out seq))
//                         seq = 0;
//                     _nextSendSeqs[destination] = seq + 1;
//                 }
//
//                 message.Acquire();
//                 message.PutFirst(seq);
//                 message.PutFirst(MessageType.Data);
//
//                 lock (_lock)
//                 {
//                     if (!_sendBuffers.TryGetValue(destination, out var buffer))
//                     {
//                         buffer = new Dictionary<int, UnionDataList>();
//                         _sendBuffers[destination] = buffer;
//                         _sendTimes[destination] = new Dictionary<int, DateTime>();
//                     }
//                     buffer[seq] = message;
//                     _sendTimes[destination][seq] = DateTime.UtcNow;
//                 }
//
//                 _retransmitEvent.Set();
//
//                 message.Acquire();
//                 var result = _inner.TrySend(destination, message);
//
//                 if (result != SendResult.Ok && result != SendResult.BufferOverflow)
//                 {
//                     lock (_lock)
//                     {
//                         if (_sendBuffers.TryGetValue(destination, out var buffer) &&
//                             buffer.TryGetValue(seq, out var buffered))
//                         {
//                             buffered.Release();
//                             buffer.Remove(seq);
//                             _sendTimes[destination].Remove(seq);
//                         }
//                     }
//                 }
//
//                 return SendResult.Ok;
//             }
//
//             private void OnInnerReceived(IEndPoint sender, UnionDataList message)
//             {
//                 byte type;
//                 if (!message.TryPopFirst(out type))
//                 {
//                     message.Release();
//                     return;
//                 }
//
//                 int seq;
//                 if (!message.TryPopFirst(out seq))
//                 {
//                     message.Release();
//                     return;
//                 }
//
//                 if (type == MessageType.Ack)
//                 {
//                     lock (_lock)
//                     {
//                         if (_sendBuffers.TryGetValue(sender, out var buffer) &&
//                             buffer.TryGetValue(seq, out var buffered))
//                         {
//                             buffered.Release();
//                             buffer.Remove(seq);
//                             _sendTimes[sender].Remove(seq);
//                         }
//                     }
//                     message.Release();
//                 }
//                 else if (type == MessageType.Data)
//                 {
//                     SendAck(sender, seq);
//
//                     var handler = OnReceived;
//                     if (handler == null)
//                     {
//                         message.Release();
//                         return;
//                     }
//
//                     DeliverOrBuffer(sender, seq, message, handler);
//                 }
//                 else
//                 {
//                     message.Release();
//                 }
//             }
//
//             private void SendAck(IEndPoint destination, int seq)
//             {
//                 var ack = _memory.SmallObjectsPool.GetPool<UnionDataList>().Acquire();
//                 ack.PutFirst(seq);
//                 ack.PutFirst(MessageType.Ack);
//                 _inner.TrySend(destination, ack);
//             }
//
//             private void DeliverOrBuffer(IEndPoint sender, int seq, UnionDataList message, Action<IEndPoint, UnionDataList> deliver)
//             {
//                 lock (_lock)
//                 {
//                     if (!_expectedRecvSeqs.TryGetValue(sender, out var expected))
//                         expected = 0;
//
//                     uint delta = (uint)unchecked(seq - expected);
//
//                     if ((int)delta < 0)
//                     {
//                         message.Release();
//                         return;
//                     }
//
//                     if (delta > MaxRecvWindow)
//                     {
//                         message.Release();
//                         return;
//                     }
//
//                     _deliveryCache.Clear();
//
//                     if (seq == expected)
//                     {
//                         _deliveryCache.Add((sender, message));
//                         expected++;
//                         _expectedRecvSeqs[sender] = expected;
//
//                         if (_recvBuffers.TryGetValue(sender, out var buffer))
//                         {
//                             while (buffer.TryGetValue(expected, out var buffered))
//                             {
//                                 buffer.Remove(expected);
//                                 _deliveryCache.Add((sender, buffered));
//                                 expected++;
//                             }
//                             _expectedRecvSeqs[sender] = expected;
//                         }
//                     }
//                     else
//                     {
//                         if (!_recvBuffers.TryGetValue(sender, out var buffer))
//                         {
//                             buffer = new SortedDictionary<int, UnionDataList>();
//                             _recvBuffers[sender] = buffer;
//                         }
//                         if (!buffer.ContainsKey(seq))
//                         {
//                             message.Acquire();
//                             buffer[seq] = message;
//                         }
//                         message.Release();
//                     }
//                 }
//
//                 try
//                 {
//                     foreach (var (ep, msg) in _deliveryCache)
//                         deliver(ep, msg);
//                 }
//                 finally
//                 {
//                     _deliveryCache.Clear();
//                 }
//             }
//
//             private void RetransmitLoop()
//             {
//                 while (!_stopped)
//                 {
//                     _retransmitEvent.WaitOne(RetransmitInterval);
//
//                     if (_stopped)
//                         break;
//
//                     var now = DateTime.UtcNow;
//                     var toRetransmit = new List<(IEndPoint ep, UnionDataList msg, int seq)>();
//
//                     lock (_lock)
//                     {
//                         foreach (var kvp in _sendTimes)
//                         {
//                             var ep = kvp.Key;
//                             var times = kvp.Value;
//                             if (!_sendBuffers.TryGetValue(ep, out var buffer))
//                                 continue;
//
//                             foreach (var timeKvp in times)
//                             {
//                                 if (now - timeKvp.Value > RetransmitTimeout)
//                                 {
//                                     if (buffer.TryGetValue(timeKvp.Key, out var msg))
//                                     {
//                                         msg.Acquire();
//                                         toRetransmit.Add((ep, msg, timeKvp.Key));
//                                     }
//                                 }
//                             }
//                         }
//
//                         foreach (var item in toRetransmit)
//                         {
//                             if (_sendTimes.TryGetValue(item.ep, out var epTimes) && epTimes.ContainsKey(item.seq))
//                                 epTimes[item.seq] = now;
//                         }
//                     }
//
//                     foreach (var (ep, msg, _) in toRetransmit)
//                     {
//                         _inner.TrySend(ep, msg);
//                     }
//                 }
//             }
//         }
//     }
// }
