using System;
using System.Collections.Generic;
using Serializer.BinarySerializer;
using Serializer.Extensions.ID;
using Serializer.Factory;
using Shared.Buffer;

namespace Shared.LogicSynchronizer
{
    interface ISyncStreamWriter
    {
        DataWriter<BufferWriter> Writer { get; }
        DataWriter<BufferWriter> SizesWriter { get; }

        ID<ISyncContextCtl> NewContextId();
    }

    internal class SubContextSet<TKey>
        where TKey : IEquatable<TKey>
    {
        public struct SubContextInfo
        {
            public readonly TKey ContextId;
            public readonly ISyncContextCtl Context;

            public SubContextInfo(TKey contextId, ISyncContextCtl context)
            {
                ContextId = contextId;
                Context = context;
            }
        }

        private readonly Dictionary<TKey, int> mContexts = new Dictionary<TKey, int>();
        private readonly List<SubContextInfo> mContextList = new List<SubContextInfo>();

        public void Append(TKey contextId, ISyncContextCtl subContext)
        {
            mContexts.Add(contextId, mContextList.Count);
            mContextList.Add(new SubContextInfo(contextId, subContext));
        }

        public ISyncContextCtl Remove(TKey contextId)
        {
            int pos;
            if (mContexts.TryGetValue(contextId, out pos))
            {
                ISyncContextCtl removed = mContextList[pos].Context;

                int count = mContextList.Count;
                if (pos != count - 1)
                {
                    mContextList[pos] = mContextList[count - 1];
                    mContexts[mContextList[pos].ContextId] = pos;
                }

                mContextList.RemoveAt(count - 1);

                mContexts.Remove(contextId);

                return removed;
            }

            return null;
        }

        public List<SubContextInfo>.Enumerator GetEnumerator()
        {
            return mContextList.GetEnumerator();
        }

        public bool TryGet(TKey contextId, out ISyncContextCtl context)
        {
            int pos;
            if (mContexts.TryGetValue(contextId, out pos))
            {
                context = mContextList[pos].Context;
                return true;
            }

            context = null;
            return false;
        }
    }

    public interface ISyncContextView
    {
        string Name { get; }
        string GetStreamName(StreamId id);
        event Action BufferApplied;
        StreamId NewDataStream(ILogicStream logicStream);
        Session Send(StreamId stream);
    }

    public interface ISyncContextCtl : ISyncContextView
    {
        ID<ISyncContextCtl> ID { get; }
        bool IsValid();

        ISyncContextCtl SubContextAdded(DataReader<BufferReader> reader);

        bool ApplyStream(StreamId streamId, DataReader<BufferReader> reader);

        void Free();
    }

    internal interface ISyncContextCtl<TChildrenKey> : ISyncContextCtl
        where TChildrenKey : IDataStruct, IEquatable<TChildrenKey>
    {
        void RemoveSubContext(TChildrenKey key);
    }

    public interface ISyncContextView<TChildKey> : ISyncContextView
        where TChildKey : IDataStruct, IEquatable<TChildKey>
    {
        SyncContext<TChildKey, TSubContextChildKey> NewSubContext<TSubContextChildKey>(TChildKey id, string debugName = null)
            where TSubContextChildKey : IDataStruct, IEquatable<TSubContextChildKey>;
        SyncContext<TChildKey, VoidKey> NewSubContext(TChildKey id, string debugName = null);
        bool TryGetContext<TSubContextChildKey>(TChildKey id, out SyncContext<TChildKey, TSubContextChildKey> context)
            where TSubContextChildKey : IDataStruct, IEquatable<TSubContextChildKey>;
    }

    public interface ISyncContextViewForWrapper<out TKey> : ISyncContextView
    {
        TKey Key { get; }
    }

    public struct Session : IDisposable
    {
        private readonly DataWriter<BufferWriter> mWriter;
        private readonly DataWriter<BufferWriter> mSizesWriter;
        private readonly int mPreCount;

        internal Session(DataWriter<BufferWriter> writer, DataWriter<BufferWriter> sizesWriter)
        {
            mWriter = writer;
            mSizesWriter = sizesWriter;
            mPreCount = mWriter.Writer.Count;
        }

        public void Dispose()
        {
            ((IDataWriter)mSizesWriter).AddUInt16((ushort)(mWriter.Writer.Count - mPreCount));
        }

        public IDataWriter Writer
        {
            get { return mWriter; }
        }
    }

    public interface ILogicStream
    {
        bool Receive(StreamId streamId, IDataReader reader);
    }

    public class SyncContext<TOwnerKey, TChildrenKey> : ISyncContextViewForWrapper<TOwnerKey>, ISyncContextCtl<TChildrenKey>, ISyncContextView<TChildrenKey>
        where TOwnerKey : IDataStruct, IEquatable<TOwnerKey>
        where TChildrenKey : IDataStruct, IEquatable<TChildrenKey>
    {
        private ID<ISyncContextCtl> mID;

        public ID<ISyncContextCtl> ID
        {
            get { return mID; }
        }

        private readonly ISyncContextCtl<TOwnerKey> mOwner;
        private readonly TOwnerKey mContextKey;
        private readonly string mDebugName;

        private readonly ISyncStreamWriter mStreamWriter;

        private readonly List<ILogicStream> mLogicStreams = new List<ILogicStream>();
        private readonly SubContextSet<TChildrenKey> mSubContexts = new SubContextSet<TChildrenKey>();

        public event Action BufferApplied;

        internal SyncContext(ID<ISyncContextCtl> id, ISyncContextCtl<TOwnerKey> owner, ISyncStreamWriter streamWriter, TOwnerKey contextKey, string debugName = null)
        {
            mID = id;

            mOwner = owner;
            mStreamWriter = streamWriter;

            mContextKey = contextKey;
            mDebugName = debugName;
        }

        public void Free()
        {
            foreach (var subInfo in mSubContexts)
            {
                subInfo.Context.Free();
            }

            mID = ID<ISyncContextCtl>.Invalid;
        }

        public bool IsValid()
        {
            return mID.IsValid;
        }

        public SyncContext<TChildrenKey, TSubContextChildrenKey> NewSubContext<TSubContextChildrenKey>(TChildrenKey key, string debugName = null)
            where TSubContextChildrenKey : IDataStruct, IEquatable<TSubContextChildrenKey>
        {
            var context = new SyncContext<TChildrenKey, TSubContextChildrenKey>(mStreamWriter.NewContextId(), this, mStreamWriter, key, debugName);
            mSubContexts.Append(key, context);

            using (var scope = Send(SynchronizerConsts.AddContextStreamId))
            {
                scope.Writer.AddId(context.mID);
                scope.Writer.Add(ref key);
            }

            //Log.i("NewSyncContext '{0}'", context.Name);
            return context;
        }

        public SyncContext<TChildrenKey, VoidKey> NewSubContext(TChildrenKey id, string debugName = null)
        {
            return NewSubContext<VoidKey>(id, debugName);
        }

        public void RemoveSubContext(TChildrenKey key)
        {
            ISyncContextCtl contextToRemove = mSubContexts.Remove(key);
            if (contextToRemove != null)
            {
                if (IsValid())
                {
                    mStreamWriter.Writer.AddId(contextToRemove.ID);
                    mStreamWriter.Writer.AddStreamId(SynchronizerConsts.RemoveContextStreamId);
                }
                contextToRemove.Free();
            }
        }

        public void Remove()
        {
            if (mOwner != null)
            {
                mOwner.RemoveSubContext(mContextKey);
            }
        }

        public bool TryGetContext<TSubContextChildKey>(TChildrenKey id, out SyncContext<TChildrenKey, TSubContextChildKey> context)
            where TSubContextChildKey : IDataStruct, IEquatable<TSubContextChildKey>
        {
            ISyncContextCtl contextView;
            if (mSubContexts.TryGet(id, out contextView))
            {
                context = (SyncContext<TChildrenKey, TSubContextChildKey>)contextView;
                return true;
            }
            context = null;
            return false;
        }

        public StreamId NewDataStream(ILogicStream logicStream)
        {
            mLogicStreams.Add(logicStream);
            StreamId streamId = new StreamId((ushort) (mLogicStreams.Count + 1));

            return streamId;
        }

        private int GetStreamIndex(StreamId stream)
        {
            return stream.Id - 2;
        }

        public Session Send(StreamId stream)
        {
            if (GetStreamIndex(stream) >= mLogicStreams.Count)
            {
                throw new Exception();
            }

            mStreamWriter.Writer.AddId(mID);
            mStreamWriter.Writer.AddStreamId(stream);
            return new Session(mStreamWriter.Writer, mStreamWriter.SizesWriter);
        }

        public ISyncContextCtl SubContextAdded(DataReader<BufferReader> reader)
        {
            TChildrenKey subKey = default(TChildrenKey);
            reader.Add(ref subKey);

            ISyncContextCtl contextView;
            mSubContexts.TryGet(subKey, out contextView);

//            Log.i("SyncContext SubContextAdded: key = " + mContextKey + ", childKey = " + subKey);
            return contextView;
        }

        public bool ApplyStream(StreamId streamId, DataReader<BufferReader> reader)
        {
            int streamIndex = GetStreamIndex(streamId);
            if (streamIndex < mLogicStreams.Count)
            {
                var stream = mLogicStreams[streamIndex];

                if (stream.Receive(streamId, reader))
                {
                    //todo вызывать один раз в конце тика а не каждый апплай стрима
                    if (BufferApplied != null)
                    {
                        BufferApplied();
                    }

                    return true;
                }

                return false;
            }
            else
            {
                Log.e("Wrong sync buffer ID={0}", streamId.Id);
                return false;
            }
        }

        public string GetStreamName(StreamId id)
        {
            int streamIndex = GetStreamIndex(id);
            var stream = mLogicStreams[streamIndex];
            return stream.GetType().Name;
        }

        public string Name
        {
            get
            {
                string name;
                if (mDebugName != null)
                {
                    name = "[" + mDebugName + "]#" + mContextKey;
                }
                else
                {
                    name = "#" + mContextKey;
                }

                if (mOwner != null)
                {
                    return mOwner.Name + "." + name;
                }
                return name;
            }
        }

        public TOwnerKey Key
        {
            get { return mContextKey; }
        }
    }
}
