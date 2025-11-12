using System;
using System.Collections.Generic;
using System.IO;using Serializer.BinarySerializer;
using Serializer.Extensions.ID;
using Serializer.Factory;
using Shared.Buffer;

namespace Shared.LogicSynchronizer
{
    internal static class SynchronizerConsts
    {
        public static readonly StreamId AddContextStreamId = new StreamId(0);
        public static readonly StreamId RemoveContextStreamId = new StreamId(1);
    }


    public class Synchronizer<TRootKey> : ISyncStreamWriter
        where TRootKey : struct, IDataStruct, IEquatable<TRootKey>
    {
        private readonly IDSource<ISyncContextCtl> mContextIdSource;

        private readonly IMemoryBufferPool mBufferPool;

        private readonly SyncContext<VoidKey, TRootKey> mRoot;

        private readonly StatisticsCollector mStatCollector;


        private readonly Dictionary<ID<ISyncContextCtl>, ISyncContextCtl> mContexts = new Dictionary<ID<ISyncContextCtl>, ISyncContextCtl>();

        private readonly DataReader<BufferReader> mStreamsReader;
        private readonly DataReader<BufferReader> mSizesReader;
        private readonly DataWriter<BufferWriter> mStreamsWriter;
        private readonly DataWriter<BufferWriter> mSizesWriter;

        public Synchronizer(IDSource<ISyncContextCtl> contextIdSource, IDataStructFactory factory, IMemoryBufferPool bufferPool, bool collectStatistics)
        {
            mContextIdSource = contextIdSource;
            mBufferPool = bufferPool;
            mStatCollector = collectStatistics ? new StatisticsCollector() : null;

            mStreamsReader = new DataReader<BufferReader>(factory, null);
            mSizesReader = new DataReader<BufferReader>(factory, null);
            mStreamsWriter = new DataWriter<BufferWriter>(new BufferWriter(), factory);
            mStreamsWriter.Writer.SetBuffer(mBufferPool.Allocate());
            mSizesWriter = new DataWriter<BufferWriter>(new BufferWriter(), factory);
            mSizesWriter.Writer.SetBuffer(mBufferPool.Allocate());

            var rootId = ID<ISyncContextCtl>.DeserializeFrom(-1);
            mRoot = new SyncContext<VoidKey, TRootKey>(rootId, null, this, default(VoidKey), "root");
            mContexts.Add(rootId, mRoot);
        }

        public void Free()
        {
            mRoot.Free();

            mStreamsReader.Reader.SetBuffer(null);
            mSizesReader.Reader.SetBuffer(null);
            mStreamsWriter.Writer.SetBuffer(null);
            mSizesWriter.Writer.SetBuffer(null);
        }

        public SyncContext<VoidKey, TRootKey> RootContext
        {
            get { return mRoot; }
        }

        public void ApplySyncFrame(IMemoryBufferHolder buffer)
        {
            using (var bufferAccessor = buffer.ExposeAccessorOnce())
            {
                IMemoryBufferHolder sizesBuffer;
                bufferAccessor.Buffer.PopFirst().AsBuffer(out sizesBuffer);


                using (var sizesBufferAccessor = sizesBuffer.ExposeAccessorOnce())
                {
                    try
                    {
                        mSizesReader.Reader.SetBuffer(sizesBufferAccessor.Buffer);
                        mStreamsReader.Reader.SetBuffer(bufferAccessor.Buffer);

                        while (!mStreamsReader.IsEndOfBuffer)
                        {
                            ID<ISyncContextCtl> remoteId = mStreamsReader.ReadId<ISyncContextCtl>();
                            StreamId streamId = mStreamsReader.ReadStreamId();

                            if (streamId == SynchronizerConsts.RemoveContextStreamId)
                            {
                                mContexts.Remove(remoteId);
                            }
                            else
                            {
                                ushort size = mSizesReader.ReadUInt16();

                                ISyncContextCtl context;
                                if (mContexts.TryGetValue(remoteId, out context) && context.IsValid())
                                {
                                    if (streamId == SynchronizerConsts.AddContextStreamId)
                                    {
                                        ID<ISyncContextCtl> remoteSubId = mStreamsReader.ReadId<ISyncContextCtl>();
                                        ISyncContextCtl subContext = context.SubContextAdded(mStreamsReader);

                                        if (subContext != null)
                                        {
                                            mContexts.Add(remoteSubId, subContext);
                                        }
                                    }
                                    else
                                    {
                                        if (mStatCollector != null)
                                        {
                                            mStatCollector.InformAboutIncoming(context, streamId, size);
                                        }
                                        if (!context.ApplyStream(streamId, mStreamsReader))
                                        {
                                            throw new Exception("Synchronization error!");
                                        }
                                    }
                                }
                                else
                                {
                                    mStreamsReader.Reader.SkipElements(size);
                                }
                            }
                        }
                    }
                    finally
                    {
                        DBG.Diagnostics.Assert(mSizesReader.IsEndOfBuffer);
                        mSizesReader.Reader.SetBuffer(null);
                        mStreamsReader.Reader.SetBuffer(null);
                    }
                }
            }
        }

        public IMemoryBufferHolder FlushSyncFrame()
        {
            IMemoryBufferHolder buffer = mStreamsWriter.Writer.ExtractBuffer();
            mStreamsWriter.Writer.SetBuffer(mBufferPool.Allocate());

            using (var bufferAccessor = buffer.ExposeAccessorOnce())
            {
                IMemoryBufferHolder sizesBuffer = mSizesWriter.Writer.ExtractBuffer();
                mSizesWriter.Writer.SetBuffer(mBufferPool.Allocate());

                bufferAccessor.Buffer.PushBuffer(sizesBuffer);
                return bufferAccessor.Acquire();
            }
        }

        public string FlushStatistics()
        {
            return mStatCollector != null ? mStatCollector.Flush() : null;
        }

        public DataWriter<BufferWriter> Writer
        {
            get { return mStreamsWriter; }
        }

        public DataWriter<BufferWriter> SizesWriter
        {
            get { return mSizesWriter; }
        }

        ID<ISyncContextCtl> ISyncStreamWriter.NewContextId()
        {
            return new ID<ISyncContextCtl>(mContextIdSource);
        }
    }
}
