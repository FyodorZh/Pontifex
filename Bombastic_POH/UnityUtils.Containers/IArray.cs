namespace Shared
{
    public interface IROArray<TObject> : ICountable
    {
        TObject this[int id] { get; }
    }

    public interface IArray<TObject> : IROArray<TObject>
    {
        new TObject this[int id] { get; set; }
    }

    public static class IROArray_Ext
    {
        public static AsEnumerable<TObject> Enumerate<TObject>(this IROArray<TObject> list)
        {
            return new AsEnumerable<TObject>(list);
        }

        public struct AsEnumerable<TObject>
        {
            private readonly IROArray<TObject> mList;

            public AsEnumerable(IROArray<TObject> list)
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
            private readonly IROArray<TObject> mList;
            private readonly int mCount;
            private int mIdx;

            public Enumerator(IROArray<TObject> list)
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