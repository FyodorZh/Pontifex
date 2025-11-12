using System.Collections.Generic;

namespace JsonFactory
{
    internal static class Pool<TObject>
        where TObject : class, new()
    {
        [System.ThreadStatic]
        private static List<TObject> mList;

        public static TObject New()
        {
            var list = mList;
            if (list == null)
            {
                list = new List<TObject>();
                mList = list;
            }

            if (list.Count > 0)
            {
                TObject res = list[list.Count - 1];
                list.RemoveAt(list.Count - 1);
                return res;
            }
            return new TObject();
        }

        public static void Free(TObject obj)
        {
            var list = mList;
            if (list == null)
            {
                list = new List<TObject>();
                mList = list;
            }

            list.Add(obj);
        }
    }
}
