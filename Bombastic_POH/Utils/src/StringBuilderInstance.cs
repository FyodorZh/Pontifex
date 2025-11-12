using System;
using System.Collections.Generic;
using System.Text;

namespace Shared
{
    public class StringBuilderInstance
    {
        public class StringBuilderHolder : IDisposable
        {
            public readonly StringBuilder SB = new StringBuilder();
            
            public void Dispose()
            {
                SB.Length = 0;
                mSBList.Push(this);
            }
        }

        [ThreadStatic]
        private static Stack<StringBuilderHolder> mSBList;

        public static StringBuilderHolder Get()
        {
            var list = mSBList;
            if (list == null)
            {
                list = new Stack<StringBuilderHolder>();
                mSBList = list;
            }

            if (list.Count > 0)
            {
                return list.Pop();
            }

            return new StringBuilderHolder();
        }
    }
}
