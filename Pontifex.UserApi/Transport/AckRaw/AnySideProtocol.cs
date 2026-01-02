using System;
using System.Diagnostics;
using Pontifex.Utils;
using Serializer.BinarySerializer;
using Serializer.Factory;
using Shared;
using Shared.Utils;
using Transport;
using Transport.Abstractions;
using Transport.Abstractions.Endpoints;
using Transport.Abstractions.Handlers;
using Shared.Buffer;
using Shared.Concurrent;
using Shared.Pooling;
using Shared.Serialization;
using Transport.Endpoints;
using Transport.StopReasons;

namespace NewProtocol
{
    internal interface IAnyProtocolCtl
    {
        void SetProtocolHashes(IModelsHashDB protocolModelHashes);
    }

    public abstract class AnySideProtocol<TEndpoint> : IPeriodicLogic, IAnyProtocolCtl, IRawBaseHandler, IDataModelSender
        where TEndpoint : class, IAckRawBaseEndpoint
    {
        private Protocol mProtocol;
        private IDeclaration[] mProtocolDeclarations;

        private IConcurrentDataStructFactory mFactory;

        private ILogicDriverCtl mLogicDriver;

        public event Action Stopped;

        public event Action Connected;

        protected ILogger Log { get; private set; }

        protected string ProtocolHash { get; private set; }

        private volatile TEndpoint mEndPoint;

        private StopReason mCurrentReasonToStop = StopReason.Void;

        private IModelsHashDB mProtocolModelHashes;

        private readonly Intention mIntentionToInvalidateProtocol = new Intention();

        private readonly IDateTimeProvider mTimeProvider;

        protected AnySideProtocol(IDateTimeProvider timeProvider)
        {
            mTimeProvider = timeProvider;
        }

        void IAnyProtocolCtl.SetProtocolHashes(IModelsHashDB protocolModelHashes)
        {
            mProtocolModelHashes = protocolModelHashes;
        }

        protected abstract Protocol ConstructProtocol();

        protected virtual void BindToProtocol(Protocol protocol)
        {
        }

        protected virtual IConcurrentDataStructFactory ConstructFactory(Type[] modelTypes)
        {
            return new ModelTinyFactory(modelTypes);
        }

        protected abstract bool TryStart();

        protected virtual void OnTick(DateTime now)
        {
        }

        protected abstract void OnDisconnected(StopReason reason);

        protected abstract void OnStopped(StopReason reason);

        /// <summary>
        /// Асинхронно, но максимально быстро разрушает подключение.
        /// Удалённый контрагент остаётся в неведении. Вероятно будет отключен по своему таймауту.
        /// </summary>
        public void Stop(StopReason reason = null)
        {
            if (reason == null)
            {
                reason = StopReason.UserIntention;
            }

            System.Threading.Interlocked.CompareExchange(ref mCurrentReasonToStop, reason, StopReason.Void);

            var driver = mLogicDriver;
            if (driver != null)
            {
                driver.Stop();
            }

            mLogicDriver = null;
        }

        protected void InvalidateProtocol()
        {
            mIntentionToInvalidateProtocol.Set();
        }

        bool IPeriodicLogic.LogicStarted(ILogicDriverCtl driver)
        {
            mLogicDriver = driver;

            mProtocol = ConstructProtocol();
            BindToProtocol(mProtocol);

            IProtocol protocol = mProtocol;
            var protocolInfo = protocol.GetInfo(mProtocolModelHashes);
            mFactory = ConstructFactory(protocolInfo.FactoryModels);

            ProtocolHash = protocolInfo.Hash;

            mProtocolDeclarations = protocol.Declarations;
            for (ushort i = 0; i < mProtocolDeclarations.Length; ++i)
            {
                mProtocolDeclarations[i].Prepare(i, this);
            }

            Log = driver.Log;

            return TryStart();
        }

        void IPeriodicLogic.LogicTick()
        {
            if (mIntentionToInvalidateProtocol.TryToRealize())
            {
                for (ushort i = 0; i < mProtocolDeclarations.Length; ++i)
                {
                    mProtocolDeclarations[i].Stop();
                }
            }

            OnTick(mTimeProvider.Now);
        }

        void IPeriodicLogic.LogicStopped()
        {
            mLogicDriver = null;

            var reason = mCurrentReasonToStop;
            if (reason == StopReason.Void)
            {
                reason = new Transport.StopReasons.Unknown("protocol");
            }

            var endPoint = mEndPoint;
            if (endPoint != null)
            {
                endPoint.Disconnect(reason);
            }

            OnStopped(reason);

            var stopped = Stopped;
            if (stopped != null)
            {
                stopped();
            }

            if (mIntentionToInvalidateProtocol.SetAndRealize())
            {
                for (ushort i = 0; i < mProtocolDeclarations.Length; ++i)
                {
                    mProtocolDeclarations[i].Stop();
                }
            }
        }

        protected TEndpoint EndPoint
        {
            get { return mEndPoint; }
        }

        public IEndPoint RemoteEndPoint
        {
            get
            {
                var endpoint = mEndPoint;
                if (endpoint != null)
                {
                    return endpoint.RemoteEndPoint;
                }

                return VoidEndPoint.Instance;
            }
        }

        protected void OnConnected(TEndpoint endpoint)
        {
            mEndPoint = endpoint;
            var connected = Connected;
            if (connected != null)
            {
                connected();
            }
        }

        void IRawBaseHandler.OnDisconnected(StopReason reason)
        {
            System.Threading.Interlocked.CompareExchange(ref mCurrentReasonToStop, reason, StopReason.Void);

            mEndPoint = null;
            OnDisconnected(reason);
            Stop();
        }

        void IRawBaseHandler.OnReceived(IMemoryBufferHolder receivedBuffer)
        {
            using (var bufferAccessor = receivedBuffer.ExposeAccessorOnce())
            {
                ushort declId;
                bool isDataStruct;
                if (bufferAccessor.Buffer.PopFirst().AsUInt16(out declId) &&
                    bufferAccessor.Buffer.PopFirst().AsBoolean(out isDataStruct))
                {
                    if (declId < mProtocolDeclarations.Length)
                    {
                        if (isDataStruct)
                        {
                            IMultiRefByteArray bytes;
                            if (bufferAccessor.Buffer.PopFirst().AsAbstractArray(out bytes))
                            {
                                CollectableDataReader reader = ConcurrentPools.Acquire<CollectableDataReader>();
                                reader.Init(bytes.ToLowLevelByteArray(), mFactory, null);
                                bytes.Release();

                                try
                                {
                                    mProtocolDeclarations[declId].OnReceived(new ReceivedMessage(reader.Reader));
                                }
                                catch (Exception ex)
                                {
                                    Log.wtf(ex);
                                    Stop(new ExceptionFail("protocol", ex));
                                }

                                reader.Release();
                            }
                            else
                            {
                                Log.e("Failed to deserialize protocol message");
                                Stop(new TextFail("protocol", "Failed to deserialize message"));
                            }
                        }
                        else
                        {
                            try
                            {
                                mProtocolDeclarations[declId].OnReceived(new ReceivedMessage(bufferAccessor.Acquire()));
                            }
                            catch (Exception ex)
                            {
                                Log.wtf(ex);
                                Stop(new ExceptionFail("protocol", ex));
                            }
                        }
                    }
                    else
                    {
                        Log.e("Failed to parse protocol message. Invalid id.");
                        Stop(new TextFail("protocol", "Failed to parse ID"));
                    }
                }
                else
                {
                    Log.e("Failed to parse protocol message");
                    Stop(new TextFail("protocol", "Failed to parse message"));
                }
            }
        }

        public abstract bool IsServerMode { get; }

        SendResult IDataModelSender.Send(ushort declId, IMemoryBufferHolder data)
        {
            using (var bufferAccessor = data.ExposeAccessorOnce())
            {
                bufferAccessor.Buffer.PushBoolean(false); // raw data
                bufferAccessor.Buffer.PushUInt16(declId);
                SendResult res = DoSend(bufferAccessor.Acquire());
                if (res != SendResult.Ok)
                {
                    Stop(new TextFail("protocol", "Failed to send request#{0} with result: {1}", declId, res));
                }
                return res;
            }
        }

        SendResult IDataModelSender.Send<TDataStruct>(ushort declId, TDataStruct data)
        {
            CollectableDataWriter writer = ConcurrentPools.Acquire<CollectableDataWriter>();
            writer.Init(mFactory);
            try
            {
                data.Serialize(writer.Writer);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                Stop(new ExceptionFail("protocol", ex));
                return SendResult.InvalidMessage;
            }

            IMultiRefLowLevelByteArray bytes = writer.ToLowLevelByteArray();
            writer.Release();

            using (var bufferAccessor = ConcurrentUsageMemoryBufferPool.Instance.Allocate().ExposeAccessorOnce())
            {
                bufferAccessor.Buffer.PushAbstractArray(bytes);
                bufferAccessor.Buffer.PushBoolean(true); // data struct
                bufferAccessor.Buffer.PushUInt16(declId);
                SendResult res = DoSend(bufferAccessor.Acquire());
                if (res != SendResult.Ok)
                {
                    Stop(new TextFail("protocol", "Failed to send request#{0} with result: {1}", declId, res));
                }
                return res;
            }
        }

        private SendResult DoSend(IMemoryBufferHolder message)
        {
            var endPoint = mEndPoint;
            if (endPoint != null)
            {
                return endPoint.Send(message);
            }
            else
            {
                message.Release();
                return SendResult.NotConnected;
            }
        }

        public class MemoryBufferWriter : DataWriter<BufferWriter>, IInitable<IDataStructFactory, IMemoryBufferHolder>
        {
            public MemoryBufferWriter()
                : base(new BufferWriter())
            {
            }

            public bool Init(IDataStructFactory factory, IMemoryBufferHolder buffer)
            {
                SetFactory(factory);
                Writer.SetBuffer(buffer);
                return buffer != null && buffer.IsValid;
            }

            public void DeInit()
            {
                Writer.SetBuffer(null);
                SetFactory(TrivialFactory.Instance);
            }
        }

        public class MemoryBufferReader : DataReader<BufferReader>, IInitable<IDataStructFactory, IBytesAllocator, IMemoryBuffer>
        {
            public bool Init(IDataStructFactory factory, IBytesAllocator allocator, IMemoryBuffer buffer)
            {
                Init(factory, allocator);
                Reader.SetBuffer(buffer);
                return buffer != null;
            }

            public void DeInit()
            {
                Init(TrivialFactory.Instance, null);
                Reader.SetBuffer(null);
            }
        }

    }
}