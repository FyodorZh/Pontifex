//#define TRACE_HISTORY

#if TRACE_HISTORY
    #define TRACE_HISTORY_ALL
    #define TRACE_DESTRUCTOR
    #define TRACE_FILTER_BY_TYPE
#elif DEBUG
    //#define TRACE_DESTRUCTOR
#endif

using System.Diagnostics;

namespace Shared
{
    /// <summary>
    /// Объект у которого есть много владельцев
    /// </summary>
    public interface IMultiRef : IReleasable
    {
        /// <summary>
        /// TRUE если объект имеет хотябы одного владельца
        /// </summary>
        bool IsAlive { get; }

        /// <summary>
        /// Увеличивает число владельцев на 1
        /// </summary>
        void AddRef();
    }

    public static class Ext_IMultiRef
    {
        public static T Acquire<T>(this T element)
            where T : class, IMultiRef
        {
            element.AddRef();
            return element;
        }

        [Conditional("DEBUG")]
        public static void CheckAlive<T>(this T element)
            where T : IMultiRef
        {
            if (!element.IsAlive)
            {
                Log.e("Resource " + element + " is not alive");
            }
            Debug.Assert(element.IsAlive);
        }
    }

    public abstract class MultiRefImpl : IMultiRef
    {
        protected enum ErrorType
        {
            AddRefOfReleasedObject,
            ReleaseOfReleasedObject,
            WrongReviveUsage,
            Leak,
            NoUsageAssertionFail,
        }

        private RefCounter mRefCounter = new RefCounter();

        protected virtual bool TraceEnabled
        {
            get { return false; }
        }

#if TRACE_HISTORY
        private readonly ActionHistoryTracer mTracer = new ActionHistoryTracer();

        private void Trace(string name)
        {
            #if TRACE_FILTER_BY_TYPE
            if (TraceEnabled)
            #endif
            {
                mTracer.RecordEvent(name);
            }
        }
#endif

        protected MultiRefImpl(bool noInit)
        {
            if (!noInit)
            {
#if TRACE_HISTORY
                Trace("Ctor");
#endif
                mRefCounter.Init();
            }
        }

#if TRACE_DESTRUCTOR
        ~MultiRefImpl()
        {
            if (mRefCounter.IsValid)
            {
                OnRefCountError(ErrorType.Leak);
            }
        }
#endif

        protected abstract void OnReleased();

        protected virtual void OnRefCountError(ErrorType error)
        {
            string text = "Invalid MultiRef object of type " + GetType() + " usage. Error = " + error;
            Log.w(text);
#if TRACE_HISTORY
            var history = mTracer.Export();
            foreach (var stack in history)
            {
                Log.w(stack.Action + "\n" + stack.Stack.ToString());
            }
#endif
            Debug.Assert(false, text);
        }

        public bool IsAlive
        {
            get { return mRefCounter.IsValid; }
        }

        public void AddRef()
        {
#if TRACE_HISTORY_ALL
            Trace("AddRef");
#endif
            if (mRefCounter.AddRef() == 0)
            {
                OnRefCountError(ErrorType.AddRefOfReleasedObject);
            }
        }

        public void Release()
        {
            int cnt = mRefCounter.Release();

#if TRACE_HISTORY
        #if TRACE_HISTORY_ALL
            Trace("Release");
        #else
            if (cnt == 0)
            {
                Trace("Release");
            }
        #endif
#endif

            if (cnt == 0)
            {
                OnReleased();
            }
            else if (cnt < 0)
            {
                OnRefCountError(ErrorType.ReleaseOfReleasedObject);
            }
        }

        protected bool Revive()
        {
#if TRACE_HISTORY
            mTracer.Clear();
            Trace("Revive");
#endif
            if (!mRefCounter.Revive())
            {
                OnRefCountError(ErrorType.WrongReviveUsage);
                return false;
            }

            return true;
        }

        protected void AssertNoUsage()
        {
            if (mRefCounter.IsValid)
            {
                OnRefCountError(ErrorType.NoUsageAssertionFail);
            }
        }

        public override string ToString()
        {
            return "RefCount:" + mRefCounter;
        }
    }
}