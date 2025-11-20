using Shared;
using System;
using System.Collections.Generic;
using Actuarius.Collections;
using Actuarius.Memory;
using Serializer.Factory;
using Shared.Buffer;
using Shared.Pooling;
using Transport.Abstractions;
using Transport.Abstractions.Controls;

namespace Transport.Protocols.Reliable.Delivery
{
    internal class DeliveryManager : IDeliveryManager
    {
        public event Action<DeliveryId, IMultiRefByteArray, short> Received;
        public event Action<DeliveryId> FailedToDeliver;

        public event Action<DeliveryId> Delivered;

        private readonly Deduplicator mDeduplicator;
        private readonly DeliveryDispatcher mDispatcher;
        private readonly UnorderedDeliveryRecipient mRecipient;

        private readonly int mMessageMaxByteSize;

        private readonly MessageSerializer mSerializer;

        private readonly ISingleReaderWriterConcurrentQueue<IUserMessage> mQueueToSend = new SingleReaderWriterConcurrentQueue<IUserMessage>(100);

        private readonly IConcurrentPool<DeliveryInfoMessage> mDeliveryInfoMessagesPool = new CollectableObjectConcurrentPool<DeliveryInfoMessage>(new LimitedConcurrentQueue<DeliveryInfoMessage>(100));
        private readonly IConcurrentPool<UserSingleMessage> mSingleMessagesPool = new CollectableObjectConcurrentPool<UserSingleMessage>(new LimitedConcurrentQueue<UserSingleMessage>(100));
        private readonly IConcurrentPool<UserMultiMessage> mMultiMessagesPool = new CollectableObjectConcurrentPool<UserMultiMessage>(new LimitedConcurrentQueue<UserMultiMessage>(100));

        private readonly List<DeliveryInfo> mConfirmationList = new List<DeliveryInfo>();

        private static readonly int mModelTypeOverhead = BufferElement.MaxSize(BufferElementType.UInt16);
        private static readonly int mUserMessageOverhead = mModelTypeOverhead + BufferElement.MaxSize(BufferElementType.UInt16) + BufferElement.MaxSize(BufferElementType.AbstractArray);

        private static readonly int mDeliveryInfoMessageOverhead = mModelTypeOverhead + BufferElement.MaxSize(BufferElementType.UInt16);
        private static readonly int mUserSingleMessageOverhead = mUserMessageOverhead;
        private static readonly int mUserMultiMessageOverhead = mUserMessageOverhead + 2 * BufferElement.MaxSize(BufferElementType.Byte);

        private static readonly int mDeliveryInfoMessageElementOverhead = BufferElement.MaxSize(BufferElementType.UInt16) + BufferElement.MaxSize(BufferElementType.Byte);

        public readonly ILogger Log;

        public DeliveryManager(int messageMaxByteSize, ILogger logger, IDeliveryControllerSink deliveryController)
        {
            mDeduplicator = new Deduplicator(1024);

            mDispatcher = new DeliveryDispatcher(ReliableInfo.TransportMessageQueueCapacity,
                deliveredId =>
                {
                    var onDelivered = Delivered;
                    if (onDelivered != null)
                    {
                        onDelivered(deliveredId);
                    }
                },
                failedId =>
                {
                    var onFailed = FailedToDeliver;
                    if (onFailed != null)
                    {
                        onFailed(failedId);
                    }
                }, deliveryController);
            mRecipient = new UnorderedDeliveryRecipient();

            mMessageMaxByteSize = messageMaxByteSize;

            mSerializer = new MessageSerializer(new MessageFactory(mDeliveryInfoMessagesPool, mSingleMessagesPool, mMultiMessagesPool));

            Log = logger;
        }

        public void Clear()
        {
            mDispatcher.Clear();
            mRecipient.Clear();

            mQueueToSend.ForAll(msg => msg.Release());
        }

        private int SignleChunkDeliveryMaxSize
        {
            get { return mMessageMaxByteSize - mUserSingleMessageOverhead - 4; } // -4 just to be sure
        }

        private int MultiChunkDeliveryChunkMaxSize
        {
            get { return mMessageMaxByteSize - mUserMultiMessageOverhead - 4; } // -4 just to be sure
        }

        public int DeliveryMaxByteSize
        {
            get { return MultiChunkDeliveryChunkMaxSize * 255; }
        }

        public SendResult ScheduleDelivery(DeliveryId id, IMultiRefByteArray data, short responseProcessTime = 0)
        {
            if (data == null)
            {
                return SendResult.InvalidMessage;
            }

            try
            {
                if (data.IsValid)
                {
                    int dataSize = data.Count;

                    if (dataSize <= SignleChunkDeliveryMaxSize)
                    {
                        UserSingleMessage msg = mSingleMessagesPool.Acquire();

                        msg.Init(id, data.Acquire());
                        msg.ResponseProcessTime = responseProcessTime;

                        mQueueToSend.Put(msg);
                        return SendResult.Ok;
                    }

                    if (dataSize <= DeliveryMaxByteSize)
                    {
                        int maxChunkSize = MultiChunkDeliveryChunkMaxSize;

                        int chunksNumber = (dataSize + maxChunkSize - 1) / maxChunkSize;
                        if (chunksNumber > 255)
                        {
                            Log.e("Wrong math here. Check it!");
                            return SendResult.MessageToBig; // wrong math.
                        }

                        for (int i = 0; i < chunksNumber; ++i)
                        {
                            int offset = i * maxChunkSize;
                            int count = Math.Min(maxChunkSize, dataSize - offset);
                            IMultiRefByteArray chunkData = data.Sub(offset, count);

                            UserMultiMessage msg = mMultiMessagesPool.Acquire();

                            msg.Init(id, chunkData, (byte)i, (byte)chunksNumber);
                            msg.ResponseProcessTime = responseProcessTime;

                            mQueueToSend.Put(msg);
                        }
                        return SendResult.Ok;
                    }
                    return SendResult.MessageToBig;
                }
                return SendResult.InvalidMessage;
            }
            finally
            {
                data.Release();
            }
        }

        public bool ProcessIncoming(Message message)
        {
            IMessage incomingMessage = null;
            try
            {
                var id = message.Id;
                Deduplicator.Result duplicity;
                if (id != MessageId.Void)
                {
                    duplicity = mDeduplicator.Received(id.Id);
                }
                else
                {
                    duplicity = Deduplicator.Result.New;
                }

                if (duplicity == Deduplicator.Result.Overflow)
                {
                    Log.e("Deduplicator overflow. Consider increase deduplicator buffer size");
                    return false;
                }

                IMemoryBufferHolder memBuffer;
                if (!ConcurrentUsageMemoryBufferPool.Instance.AllocateAndDeserialize(message.Data, out memBuffer))
                {
                    return false;
                }
                using (var bufferAccessor = memBuffer.ExposeAccessorOnce())
                {
                    incomingMessage = mSerializer.Deserialize(bufferAccessor.Buffer);
                }


                DeliveryInfoMessage infoMessage = incomingMessage as DeliveryInfoMessage;
                if (infoMessage != null && duplicity == Deduplicator.Result.New)
                {
                    foreach (DeliveryInfo confirmedDelivery in infoMessage.ConfirmedDeliveries)
                    {
                        mDispatcher.Delivered(confirmedDelivery);
                    }
                    return true;
                }

                IUserMessage userMessage = incomingMessage as IUserMessage;
                if (userMessage != null)
                {
                    mConfirmationList.Add(userMessage.DeliveryInfo);

                    if (duplicity == Deduplicator.Result.New)
                    {
                        DeliveryId messageId = userMessage.Id;
                        short responseProcessTime = userMessage.ResponseProcessTime;

                        IMultiRefByteArray incomingData;

                        UserMultiMessage multiMessage = userMessage as UserMultiMessage;
                        if (multiMessage != null)
                        {
                            incomingData = mRecipient.Received(multiMessage);
                        }
                        else // UserSingleMessage
                        {
                            incomingData = mRecipient.Received((UserSingleMessage)userMessage);
                        }

                        if (incomingData.IsValid && Received != null)
                        {
                            Received(messageId, incomingData, responseProcessTime);
                        }
                        else
                        {
                            incomingData.Release();
                        }
                    }
                    return true;
                }

                return false;
            }
            finally
            {
                message.Release();
                if (incomingMessage != null)
                {
                    incomingMessage.Release();
                }
            }
        }

        public IMacroOwner<Message> ProcessOutgoing(IDeliveryAttemptScheduler scheduler, DateTime now)
        {
            var outDataToSend = ConcurrentPools.Acquire<CollectableMacroOwner<Message>>();

            foreach (var userMessageToSend in mQueueToSend.Enumerate())
            {
                IMemoryBufferHolder serializedBuffer = mSerializer.Serialize(userMessageToSend);
                IMultiRefByteArray bufferAsArray = serializedBuffer; // lazy serialization

                switch (mDispatcher.ScheduleDeliver(userMessageToSend.DeliveryInfo, bufferAsArray, now))
                {
                    case DeliveryDispatcher.ScheduleResult.BufferOverflow:
                        if (FailedToDeliver != null)
                        {
                            FailedToDeliver(userMessageToSend.DeliveryInfo.Id);
                        }
                        break;
                    case DeliveryDispatcher.ScheduleResult.IdIsNotUnique:
                        Log.e("Error: message id collision");
                        break;
                    case DeliveryDispatcher.ScheduleResult.Ok:
                        break;
                    default:
                        Log.e("Error: unknown ScheduleDeliver() result");
                        break;
                }

                userMessageToSend.Release();
            }

            if (mConfirmationList.Count > 0)
            {
                int packSize = (mMessageMaxByteSize - mDeliveryInfoMessageOverhead - 4) / mDeliveryInfoMessageElementOverhead; // 4 - для надёжности

                int pos = 0;
                while (pos < mConfirmationList.Count)
                {
                    int len = Math.Min(packSize, mConfirmationList.Count - pos);

                    DeliveryInfoMessage info = mDeliveryInfoMessagesPool.Acquire();

                    for (int i = pos; i < pos + len; ++i)
                    {
                        info.Append(mConfirmationList[i]);
                    }

                    IMultiRefByteArray bufferAsArray = mSerializer.Serialize(info);
                    outDataToSend.Put(new Message(MessageId.Void, bufferAsArray));

                    info.Release();

                    pos += len;
                }

                mConfirmationList.Clear();
            }

            mDispatcher.TryToDeliver(outDataToSend, scheduler, now);

            return outDataToSend;
        }

        private class MessageFactory : IDataStructFactory
        {
            private static readonly Type T0 = typeof(DeliveryInfoMessage);
            private static readonly Type T1 = typeof(UserSingleMessage);
            private static readonly Type T2 = typeof(UserMultiMessage);

            private readonly IConcurrentPool<DeliveryInfoMessage> mInfoMessagesPool;
            private readonly IConcurrentPool<UserSingleMessage> mSingleMessagesPool;
            private readonly IConcurrentPool<UserMultiMessage> mMultiMessagesPool;

            public MessageFactory(
                IConcurrentPool<DeliveryInfoMessage> infoMessagesPool,
                IConcurrentPool<UserSingleMessage> singleMessagesPool,
                IConcurrentPool<UserMultiMessage> multiMessagesPool)
            {
                mInfoMessagesPool = infoMessagesPool;
                mSingleMessagesPool = singleMessagesPool;
                mMultiMessagesPool = multiMessagesPool;
            }

            public bool SerializeModelIndex()
            {
                return true;
            }

            public short GetIndexForModel(Type modelType)
            {
                if (modelType == T0)
                    return 0;
                if (modelType == T1)
                    return 1;
                if (modelType == T2)
                    return 2;
                return -1;
            }

            public object CreateDataStruct(short modelIndex)
            {
                switch (modelIndex)
                {
                    case 0:
                        return mInfoMessagesPool.Acquire();
                    case 1:
                        return mSingleMessagesPool.Acquire();
                    case 2:
                        return mMultiMessagesPool.Acquire();
                    default:
                        return null;
                }
            }
        }
    }
}
