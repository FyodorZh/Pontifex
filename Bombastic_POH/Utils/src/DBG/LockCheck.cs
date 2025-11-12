using System;
using System.Collections.Generic;

namespace DBG
{
    public static class Lock
    {
        private static readonly Dictionary<object, Dictionary<object, string>> mGraph = new Dictionary<object, Dictionary<object, string>>();

        [ThreadStatic]
        private static List<object> mLockStack;

        [System.Diagnostics.Conditional("DEBUG")]
        public static void Push(object lockObject)
        {
            if (mLockStack == null)
            {
                mLockStack = new List<object>();
            }

            lock (mGraph)
            {
                Dictionary<object, string> map;
                if (!mGraph.TryGetValue(lockObject, out map))
                {
                    map = new Dictionary<object, string>();
                    mGraph.Add(lockObject, map);
                }

                for (int i = 0; i < mLockStack.Count; ++i)
                {
                    object parent = mLockStack[i];
                    if (!ReferenceEquals(parent, lockObject))
                    {
                        if (!map.ContainsKey(parent))
                        {
                            map.Add(parent, new System.Diagnostics.StackTrace(1).ToString());
                        }

                        string stack;
                        if (mGraph[parent].TryGetValue(lockObject, out stack))
                        {
                            Log.e("Lock usage error detected!!! {0}\n{1}", stack, new System.Diagnostics.StackTrace(1).ToString());
                        }
                    }
                }
            }

            mLockStack.Add(lockObject);
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void Pop()
        {
            mLockStack.RemoveAt(mLockStack.Count - 1);
        }
    }
}
