using System.Collections.Generic;
using Actuarius.Collections;

namespace Shared
{
    public class TrivialStack<TData> : IStack<TData>
    {
        private readonly Stack<TData> mStack = new Stack<TData>();
        
        public int Count => mStack.Count;

        public bool Put(TData value)
        {
            mStack.Push(value);
            return true;
        }

        public bool TryPop(out TData value)
        {
            if (mStack.Count > 0)
            {
                value = mStack.Pop();
                return true;
            }

            value = default(TData);
            return false;
        }
    }
}