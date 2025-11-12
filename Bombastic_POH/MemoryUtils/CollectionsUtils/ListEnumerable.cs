using System.Collections.Generic;

namespace Shared
{
    public struct ListEnumerable<T>
    {
        private readonly List<T> mList;

        public ListEnumerable(List<T> list)
        {
            mList = list;
        }

        public List<T>.Enumerator GetEnumerator()
        {
            return mList.GetEnumerator();
        }

        public T this[int id]
        {
            get
            {
                return mList[id];
            }
        }

        public int Count
        {
            get
            {
                return mList.Count;
            }
        }
    }

    public struct IListEnumerable<T>
    {
        private readonly IList<T> mList;

        public IListEnumerable(IList<T> list)
        {
            mList = list;
        }

        public ListEnumerator<T> GetEnumerator()
        {
            return new ListEnumerator<T>(mList);
        }

        public T this[int id]
        {
            get
            {
                return mList[id];
            }
        }

        public int Count
        {
            get
            {
                return mList.Count;
            }
        }
    }

    public static class ListEnumerableExtension
    {
        public static ListEnumerable<T> ToEnumerable<T>(this List<T> list)
        {
            return new ListEnumerable<T>(list);
        }
    }
}
