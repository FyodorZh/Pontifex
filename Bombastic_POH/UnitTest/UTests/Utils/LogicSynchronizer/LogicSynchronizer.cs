using System;
using NUnit.Framework;
using Serializer.BinarySerializer;
using Serializer.Factory;
using Shared;
using Shared.Buffer;
using Shared.LogicSynchronizer;

namespace TransportAnalyzer.UTests
{
    [TestFixture]
    class LogicSynchronizer
    {
        private interface IA
        {
            int Val { get; set; }
            void F(double value, char c);
        }

        private class A : IA
        {
            public int Val
            {
                get; set;
            }

            public double D;
            public char C;
            public void F(double value, char c)
            {
                D = value;
                C = c;
            }
        }

        private class WrapperA : IA, ILogicStream
        {
            private readonly IA mCore;
            private readonly ISyncContextView mContext;

            private readonly StreamId mValRef;
            private readonly StreamId mFRef;

            public WrapperA(IA core, ISyncContextView context)
            {
                mCore = core;
                mContext = context;

                mValRef = context.NewDataStream(this);
                mFRef = context.NewDataStream(this);
            }

            public bool Receive(StreamId streamId, IDataReader reader)
            {
                if (streamId == mValRef)
                {
                    int val = 0;
                    reader.Add(ref val);
                    mCore.Val = val;
                    return true;
                }
                else if (streamId == mFRef)
                {
                    double val = 0;
                    char c = ' ';
                    reader.Add(ref val);
                    reader.Add(ref c);
                    mCore.F(val, c);
                    return true;
                }
                else
                {
                    Log.e("Wrong streamId!");
                    return false;
                }
            }

            public int Val
            {
                get => mCore.Val;
                set
                {
                    using (var session = mContext.Send(mValRef))
                    {
                        session.Writer.Add(ref value);
                        mCore.Val = value;
                    }
                }
            }

            public void F(double value, char c)
            {
                using (var session = mContext.Send(mFRef))
                {
                    session.Writer.Add(ref value);
                    session.Writer.Add(ref c);
                    mCore.F(value, c);
                }
            }
        }

        [Test]
        public void Test()
        {
            IDSource<ISyncContextCtl> contextIdSource = new IDSourceImpl<ISyncContextCtl>(0, 1);
            Synchronizer<IntKey> s1 = new Synchronizer<IntKey>(contextIdSource, TrivialFactory.Instance, ConcurrentUsageMemoryBufferPool.Instance, true);
            Synchronizer<IntKey> s2 = new Synchronizer<IntKey>(contextIdSource, TrivialFactory.Instance, ConcurrentUsageMemoryBufferPool.Instance, true);

            A core1 = new A();
            A core2 = new A();
            IA wrapper1 = new WrapperA(core1, s1.RootContext.NewSubContext<ByteKey>(new IntKey(7)));
            IA wrapper2 = new WrapperA(core2, s2.RootContext.NewSubContext<ByteKey>(new IntKey(7)));

            {
                wrapper1.Val = 123;
                wrapper1.F(1.5, 'z');

                wrapper1.Val = 124;

                var buffer1 = s1.FlushSyncFrame();
                var buffer2 = s2.FlushSyncFrame();

                s1.ApplySyncFrame(buffer2);
                s2.ApplySyncFrame(buffer1);

                Assert.AreEqual(124, core1.Val);

                Assert.AreEqual(1.5, core1.D);
                Assert.AreEqual(1.5, core2.D);

                Assert.AreEqual('z', core1.C);
                Assert.AreEqual('z', core2.C);

                Assert.AreEqual(124, core2.Val);
            }

            s1.Free();
            s2.Free();

            GC.Collect();
        }
    }
}