using System;
using System.Collections.Generic;
using Actuarius.Collections;

namespace Shared
{
    public partial class PriorityQueue<Key, Data> : PriorityQueue<Key, Data>.IPriorityQueueCtl
        where Key : IComparable<Key>
    {
        protected interface IPriorityQueueCtl
        {
            bool RemoveAtIndex(int id);
            bool UpdateKeyAtIndex(int id, Key key);
            Key KeyAt(int idx);
            Data DataAt(int idx);
        }

        private PriorityQueue(PriorityQueue<Key, Data> queue)
        {
            mKeys.AddRange(queue.mKeys);
            mData.AddRange(queue.mData);
        }

        private Key[] _Keys
        {
            get
            {
                PriorityQueue<Key, Data> queue = new PriorityQueue<Key, Data>(this);
                List<Key> keys = new List<Key>();
                while (queue.Count > 0)
                {
                    keys.Add(queue.TopKey());
                    queue.Dequeue();
                }
                return keys.ToArray();
            }
        }

        public struct DataRef
        {
            private IPriorityQueueCtl mOwner;
            private readonly int mId;

            public DataRef(PriorityQueue<Key, Data> owner, int id)
            {
                mOwner = owner;
                mId = id;
            }

            public bool IsValid
            {
                get { return mOwner != null; }
            }

            public Data ShowData()
            {
                if (mOwner != null)
                {
                    return mOwner.DataAt(mId);
                }
                return default(Data);
            }

            public Key ShowKey()
            {
                if (mOwner != null)
                {
                    return mOwner.KeyAt(mId);
                }
                return default(Key);
            }

            public bool Update(Key newKey)
            {
                if (mOwner != null)
                {
                    var res = mOwner.UpdateKeyAtIndex(mId, newKey);
                    mOwner = null;
                    return res;
                }
                return false;
            }

            public bool Remove()
            {
                if (mOwner != null)
                {
                    return mOwner.RemoveAtIndex(mId);
                }
                return false;
            }
        }

        public struct Enumerator : IEnumerator<DataRef>
        {
            private readonly PriorityQueue<Key, Data> mOwner;

            private readonly int mLastExclusiveId;
            private readonly int mStride;

            private int mIndex;
            private DataRef mCurrent;

            public Enumerator(PriorityQueue<Key, Data> owner, int beforeFirstId, int lastExclusiveId)
            {
                mOwner = owner;

                mLastExclusiveId = lastExclusiveId;
                mStride = (beforeFirstId < lastExclusiveId) ? 1 : -1;

                mIndex = beforeFirstId;
                mCurrent = default(DataRef);
            }

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                mIndex += mStride;
                if (mIndex != mLastExclusiveId)
                {
                    mCurrent = new DataRef(mOwner, mIndex);
                    return true;
                }

                mIndex = mLastExclusiveId - mStride;
                mCurrent = default(DataRef);
                return false;
            }

            public DataRef Current
            {
                get
                {
                    return mCurrent;
                }
            }

            object System.Collections.IEnumerator.Current
            {
                get
                {
                    throw new System.InvalidOperationException("Don't use this getter");
                }
            }

            void System.Collections.IEnumerator.Reset()
            {
                mIndex = 0;
                mCurrent = default(DataRef);
            }
        }

        public struct Enumerable
        {
            private readonly PriorityQueue<Key, Data> mOwner;
            private readonly QueueEnumerationOrder mOrder;

            public Enumerable(PriorityQueue<Key, Data> owner, QueueEnumerationOrder order)
            {
                mOwner = owner;
                mOrder = order;
            }

            public Enumerator GetEnumerator()
            {
                switch (mOrder)
                {
                    case QueueEnumerationOrder.HeadToTail:
                        return new Enumerator(mOwner, 0, mOwner.Count + 1);
                    case QueueEnumerationOrder.TailToHead:
                        return new Enumerator(mOwner, mOwner.Count + 1, 0);
                    default:
                        throw new ArgumentOutOfRangeException("order");
                }
            }
        }

        public struct DataEnumerable
        {
            private readonly List<Data> mData;
            public DataEnumerable(List<Data> data)
            {
                mData = data;
            }

            public List<Data>.Enumerator GetEnumerator()
            {
                var en = mData.GetEnumerator();
                en.MoveNext();
                return en;
            }
        }

//         [UT.UT("PriorityQueue")]
//         private static void UT(UT.IUTest test)
//         {
//             PriorityQueue<int, int> queue = new PriorityQueue<int, int>();
//             test.Equal(queue.Count, 0);
//
//             System.Collections.Generic.SortedList<int, int> list = new System.Collections.Generic.SortedList<int, int>();
//             int val;
//
//             for (int i = 1; i < 10000; ++i)
//             {
//                 val = i;
//
//                 queue.Enqueue(val, val);
//                 list.Add(val, val);
//
//                 val = -i;
//
//                 queue.Enqueue(val, val);
//                 list.Add(val, val);
//
//                 int a = queue.Dequeue();
//                 var en = list.GetEnumerator();
//                 en.MoveNext();
//                 int b = en.Current.Value;
//                 list.Remove(b);
//
//                 test.Equal(a, b);
//             }
//
//             /*
//             System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
//             sw.Reset();
//             sw.Start();
//             for (int i = 1; i < 10000; ++i)
//             {
//                 val = i;
//                 queue.Enqueu(val, val);
//                 val = -i;
//                 queue.Enqueu(val, val);
//                 val = queue.Dequeue();
//             }
//             sw.Stop();
//             Log.i("Time " + sw.ElapsedMilliseconds);
//                         
//             System.Collections.Generic.SortedDictionary<int, int> sdic = new System.Collections.Generic.SortedDictionary<int, int>();
//
//             sw.Reset();
//             sw.Start();
//             for (int i = 1; i < 10000; ++i)
//             {
//                 val = i;
//                 sdic.Add(val, val);
//                 val = -i;
//                 sdic.Add(val, val);
//                 var en = sdic.GetEnumerator();
//                 en.MoveNext();
//                 val = en.Current.Value;
//                 sdic.Remove(val);
//             }
//             sw.Stop();
//             Log.i("Time " + sw.ElapsedMilliseconds);
//
//             System.Collections.Generic.SortedList<int, int> slist = new System.Collections.Generic.SortedList<int, int>();
//
//             sw.Reset();
//             sw.Start();
//             for (int i = 1; i < 10000; ++i)
//             {
//                 val = i;
//                 slist.Add(val, val);
//                 val = -i;
//                 slist.Add(val, val);
//                 var en = slist.GetEnumerator();
//                 val = en.Current.Value;
//                 en.MoveNext();
//                 slist.Remove(val);
//             }
//             sw.Stop();
//             Log.i("Time " + sw.ElapsedMilliseconds);
//             */
//         }
    }
}