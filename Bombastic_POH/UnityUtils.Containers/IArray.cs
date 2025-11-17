using Actuarius.Collections;

namespace Shared
{
    public static class IROArray_Ext
    {
        public static AsEnumerable<TObject> Enumerate<TObject>(this IReadOnlyArray<TObject> list)
        {
            return new AsEnumerable<TObject>(list);
        }

        public struct AsEnumerable<TObject>
        {
            private readonly IReadOnlyArray<TObject> mList;

            public AsEnumerable(IReadOnlyArray<TObject> list)
            {
                mList = list;
            }

            public Enumerator<TObject> GetEnumerator()
            {
                return new Enumerator<TObject>(mList);
            }
        }

        public struct Enumerator<TObject>// : IEnumerator<TObject>
        {
            private readonly IReadOnlyArray<TObject> mList;
            private readonly int mCount;
            private int mIdx;

            public Enumerator(IReadOnlyArray<TObject> list)
            {
                mList = list;
                mCount = list.Count;
                mIdx = -1;
            }

            public bool MoveNext()
            {
                mIdx += 1;
                return mIdx < mCount;
            }

            public TObject Current
            {
                get
                {
                    if (mIdx >= 0 && mIdx < mCount)
                    {
                        return mList[mIdx];
                    }

                    return default(TObject);
                }
            }
        }
    }
}