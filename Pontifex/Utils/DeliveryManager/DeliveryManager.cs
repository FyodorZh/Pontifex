using System;
using System.Collections.Generic;
using Actuarius.Collections;
using Actuarius.Memory;

namespace Pontifex.DeliveryManager
{
    internal class DeliveryManager : IDeliveryManager
    {
        public event Action<DeliveryId, IMultiRefByteArray, short>? Received;
        public event Action<DeliveryId>? FailedToDeliver;
        public event Action<DeliveryId>? Delivered;

        private const byte TypeUserSingle = 0;
        private const byte TypeUserMulti = 1;
        private const byte TypeDeliveryInfo = 2;

        private const int SingleOverhead = 5;
        private const int MultiOverhead = 7;
        private const int DeliveryInfoOverhead = 3;
        private const int DeliveryInfoElementSize = 3;

        private const int SafetyMargin = 4;
        private const int DeduplicatorCapacity = 1024;
        private const int TransportMessageQueueCapacity = 5000;

        private readonly Deduplicator _deduplicator;
        private readonly DeliveryDispatcher _dispatcher;
        private readonly UnorderedDeliveryRecipient _recipient;
        private readonly IPool<IMultiRefByteArray, int> _bytesPool;

        private readonly int _messageMaxByteSize;
        private readonly List<DeliveryInfo> _confirmationList = new List<DeliveryInfo>();
        private readonly IQueue<IMultiRefByteArray> _queueToSend;

        public DeliveryManager(int messageMaxByteSize, IPool<IMultiRefByteArray, int> bytesPool)
        {
            _messageMaxByteSize = messageMaxByteSize;
            _bytesPool = bytesPool;
            _deduplicator = new Deduplicator(DeduplicatorCapacity);
            _dispatcher = new DeliveryDispatcher(TransportMessageQueueCapacity);
            _recipient = new UnorderedDeliveryRecipient(bytesPool);
            _queueToSend = new SystemQueue<IMultiRefByteArray>();

            _dispatcher.OnDelivered += id =>
            {
                var onDelivered = Delivered;
                if (onDelivered != null)
                {
                    onDelivered(id);
                }
            };

            _dispatcher.OnFailedToDeliver += id =>
            {
                var onFailed = FailedToDeliver;
                if (onFailed != null)
                {
                    onFailed(id);
                }
            };
        }

        public void Clear()
        {
            _dispatcher.Clear();
            _recipient.Clear();

            while (_queueToSend.TryPop(out var msg))
            {
                msg.Release();
            }
        }

        private int SingleChunkDeliveryMaxSize => _messageMaxByteSize - SingleOverhead - SafetyMargin;

        private int MultiChunkDeliveryChunkMaxSize => _messageMaxByteSize - MultiOverhead - SafetyMargin;

        public int DeliveryMaxByteSize => MultiChunkDeliveryChunkMaxSize * 255;

        public SendResult ScheduleDelivery(DeliveryId id, IMultiRefByteArray data, short responseProcessTime = 0)
        {
            if (data == null || !data.IsValid)
            {
                return SendResult.InvalidMessage;
            }

            try
            {
                int dataSize = data.Count;

                if (dataSize <= SingleChunkDeliveryMaxSize)
                {
                    var serialized = SerializeUserSingle(id, data, responseProcessTime);
                    _queueToSend.Put(serialized);
                    return SendResult.Ok;
                }

                if (dataSize <= DeliveryMaxByteSize)
                {
                    int maxChunkSize = MultiChunkDeliveryChunkMaxSize;

                    int chunksNumber = (dataSize + maxChunkSize - 1) / maxChunkSize;
                    if (chunksNumber > 255)
                    {
                        return SendResult.MessageTooBig;
                    }

                    for (int i = 0; i < chunksNumber; ++i)
                    {
                        int offset = i * maxChunkSize;
                        int count = Math.Min(maxChunkSize, dataSize - offset);

                        var chunkData = CopySegment(data, offset, count);
                        var serialized = SerializeUserMulti(id, chunkData, (byte)i, (byte)chunksNumber, responseProcessTime);
                        chunkData.Release();
                        _queueToSend.Put(serialized);
                    }

                    return SendResult.Ok;
                }

                return SendResult.MessageTooBig;
            }
            finally
            {
                data.Release();
            }
        }

        public bool ProcessIncoming(IMultiRefByteArray incomingData)
        {
            byte type = incomingData.Array[incomingData.Offset];

            if (type == TypeDeliveryInfo)
            {
                ProcessDeliveryInfo(incomingData);
                incomingData.Release();
                return true;
            }

            if (type == TypeUserSingle || type == TypeUserMulti)
            {
                DeliveryId packetId;
                if (TryParseDeliveryId(incomingData, out packetId))
                {
                    var duplicity = _deduplicator.Received(packetId.Id);
                    if (duplicity == Deduplicator.Result.Overflow)
                    {
                        incomingData.Release();
                        return false;
                    }

                    _confirmationList.Add(ParseDeliveryInfo(incomingData));

                    if (duplicity == Deduplicator.Result.New)
                    {
                        IMultiRefByteArray? userData;

                        if (type == TypeUserMulti)
                        {
                            DeliveryId msgId;
                            byte partId, partsNumber;
                            short responseProcessTime;
                            IMultiRefByteArray chunkData;
                            ParseUserMulti(incomingData, out msgId, out responseProcessTime, out partId, out partsNumber, out chunkData);

                            userData = _recipient.ReceivedMulti(msgId, partId, partsNumber, chunkData);
                            chunkData.Release();
                        }
                        else
                        {
                            DeliveryId msgId;
                            short responseProcessTime;
                            IMultiRefByteArray msgData;
                            ParseUserSingle(incomingData, out msgId, out responseProcessTime, out msgData);

                            userData = _recipient.ReceivedSingle(msgData);
                            msgData.Release();
                        }

                        if (userData != null)
                        {
                            var onReceived = Received;
                            if (onReceived != null)
                            {
                                onReceived(packetId, userData, 0);
                            }
                            userData.Release();
                        }
                    }
                }

                incomingData.Release();
                return true;
            }

            incomingData.Release();
            return false;
        }

        public void ProcessOutgoing(IDeliveryAttemptScheduler scheduler, DateTime now, IConsumer<IMultiRefByteArray> dst)
        {
            IMultiRefByteArray? userMessage;
            while (_queueToSend.TryPop(out userMessage))
            {
                DeliveryInfo info = ParseDeliveryInfo(userMessage);
                userMessage.AddRef();
                var result = _dispatcher.ScheduleDeliver(info, userMessage, now);
                switch (result)
                {
                    case DeliveryDispatcher.ScheduleResult.BufferOverflow:
                    {
                        var onFailed = FailedToDeliver;
                        if (onFailed != null)
                        {
                            onFailed(info.Id);
                        }
                        break;
                    }
                    case DeliveryDispatcher.ScheduleResult.IdIsNotUnique:
                        break;
                    case DeliveryDispatcher.ScheduleResult.Ok:
                        break;
                }

                userMessage.Release();
            }

            if (_confirmationList.Count > 0)
            {
                int packSize = (_messageMaxByteSize - DeliveryInfoOverhead - SafetyMargin) / DeliveryInfoElementSize;

                int pos = 0;
                while (pos < _confirmationList.Count)
                {
                    int len = Math.Min(packSize, _confirmationList.Count - pos);
                    var infoMsg = SerializeDeliveryInfo(_confirmationList, pos, len);
                    dst.Put(infoMsg);
                    pos += len;
                }

                _confirmationList.Clear();
            }

            _dispatcher.TryToDeliver(dst, scheduler, now);
        }

        private void ProcessDeliveryInfo(IMultiRefByteArray data)
        {
            int offset = data.Offset + 1;
            ushort count = ReadUInt16LE(data.Array, offset);
            offset += 2;

            for (int i = 0; i < count; ++i)
            {
                ushort id = ReadUInt16LE(data.Array, offset);
                offset += 2;
                byte chunkId = data.Array[offset];
                offset += 1;

                _dispatcher.ConfirmDelivered(new DeliveryInfo(new DeliveryId(id), chunkId));
            }
        }

        private IMultiRefByteArray SerializeUserSingle(DeliveryId id, IMultiRefByteArray data, short responseProcessTime)
        {
            int totalSize = SingleOverhead + data.Count;
            var buffer = _bytesPool.Acquire(totalSize);

            int offset = buffer.Offset;
            buffer.Array[offset] = TypeUserSingle;
            WriteUInt16LE(buffer.Array, offset + 1, id.Id);
            WriteInt16LE(buffer.Array, offset + 3, responseProcessTime);
            data.CopyTo(buffer.Array, offset + SingleOverhead, 0, data.Count);

            return buffer;
        }

        private IMultiRefByteArray SerializeUserMulti(DeliveryId id, IMultiRefByteArray chunkData, byte partId, byte partsNumber, short responseProcessTime)
        {
            int totalSize = MultiOverhead + chunkData.Count;
            var buffer = _bytesPool.Acquire(totalSize);

            int offset = buffer.Offset;
            buffer.Array[offset] = TypeUserMulti;
            WriteUInt16LE(buffer.Array, offset + 1, id.Id);
            WriteInt16LE(buffer.Array, offset + 3, responseProcessTime);
            buffer.Array[offset + 5] = partId;
            buffer.Array[offset + 6] = partsNumber;
            chunkData.CopyTo(buffer.Array, offset + MultiOverhead, 0, chunkData.Count);

            return buffer;
        }

        private IMultiRefByteArray SerializeDeliveryInfo(List<DeliveryInfo> confirmations, int start, int count)
        {
            int totalSize = DeliveryInfoOverhead + count * DeliveryInfoElementSize;
            var buffer = _bytesPool.Acquire(totalSize);

            int offset = buffer.Offset;
            buffer.Array[offset] = TypeDeliveryInfo;
            WriteUInt16LE(buffer.Array, offset + 1, (ushort)count);

            int pos = offset + DeliveryInfoOverhead;
            for (int i = start; i < start + count; ++i)
            {
                WriteUInt16LE(buffer.Array, pos, confirmations[i].Id.Id);
                pos += 2;
                buffer.Array[pos] = confirmations[i].ChunkId;
                pos += 1;
            }

            return buffer;
        }

        private static bool TryParseDeliveryId(IMultiRefByteArray data, out DeliveryId id)
        {
            id = default;
            if (data.Count < 2)
            {
                return false;
            }
            ushort rawId = ReadUInt16LE(data.Array, data.Offset + 1);
            id = new DeliveryId(rawId);
            return true;
        }

        private static DeliveryInfo ParseDeliveryInfo(IMultiRefByteArray data)
        {
            int offset = data.Offset;
            byte type = data.Array[offset];

            if (type == TypeUserSingle)
            {
                ushort id = ReadUInt16LE(data.Array, offset + 1);
                return new DeliveryInfo(new DeliveryId(id), 0);
            }

            if (type == TypeUserMulti)
            {
                ushort id = ReadUInt16LE(data.Array, offset + 1);
                byte chunkId = data.Array[offset + 5];
                return new DeliveryInfo(new DeliveryId(id), chunkId);
            }

            return default;
        }

        private static DeliveryId ParseDeliveryId(IMultiRefByteArray data)
        {
            ushort id = ReadUInt16LE(data.Array, data.Offset + 1);
            return new DeliveryId(id);
        }

        private static void ParseUserSingle(IMultiRefByteArray data, out DeliveryId id, out short responseProcessTime, out IMultiRefByteArray userData)
        {
            int offset = data.Offset;
            id = new DeliveryId(ReadUInt16LE(data.Array, offset + 1));
            responseProcessTime = ReadInt16LE(data.Array, offset + 3);
            int dataOffset = offset + SingleOverhead;
            int dataCount = data.Count - SingleOverhead;
            userData = CopySegment(data, dataOffset - data.Offset, dataCount);
        }

        private static void ParseUserMulti(IMultiRefByteArray data, out DeliveryId id, out short responseProcessTime, out byte partId, out byte partsNumber, out IMultiRefByteArray chunkData)
        {
            int offset = data.Offset;
            id = new DeliveryId(ReadUInt16LE(data.Array, offset + 1));
            responseProcessTime = ReadInt16LE(data.Array, offset + 3);
            partId = data.Array[offset + 5];
            partsNumber = data.Array[offset + 6];
            int dataOffset = offset + MultiOverhead;
            int dataCount = data.Count - MultiOverhead;
            chunkData = CopySegment(data, dataOffset - data.Offset, dataCount);
        }

        private static IMultiRefByteArray CopySegment(IMultiRefByteArray source, int relativeOffset, int count)
        {
            var copy = new byte[count];
            Buffer.BlockCopy(source.Array, source.Offset + relativeOffset, copy, 0, count);
            return new MultiRefByteArray(copy);
        }

        private static void WriteUInt16LE(byte[] buffer, int offset, ushort value)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        }

        private static void WriteInt16LE(byte[] buffer, int offset, short value)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        }

        private static ushort ReadUInt16LE(byte[] buffer, int offset)
        {
            return (ushort)(buffer[offset] | (buffer[offset + 1] << 8));
        }

        private static short ReadInt16LE(byte[] buffer, int offset)
        {
            return (short)(buffer[offset] | (buffer[offset + 1] << 8));
        }
    }
}
