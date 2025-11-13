#define TRACE_HISTORY

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
        /// TRUE если объект имеет хотя бы одного владельца
        /// </summary>
        bool IsAlive { get; }

        /// <summary>
        /// Увеличивает число владельцев на 1
        /// </summary>
        void AddRef();
    }

    public static class IMultiRef_Ext
    {
        public static T Acquire<T>(this T element)
            where T : class, IMultiRef
        {
            element.AddRef();
            return element;
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

        private readonly RefCounter _refCounter = new RefCounter();

        protected virtual bool TraceEnabled => false;

#if TRACE_HISTORY
        private readonly ActionHistoryTracer _tracer = new ActionHistoryTracer();

        private void Trace(string name)
        {
            #if TRACE_FILTER_BY_TYPE
            if (TraceEnabled)
            #endif
            {
                _tracer.RecordEvent(name);
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
                _refCounter.Init();
            }
        }

#if TRACE_DESTRUCTOR
        ~MultiRefImpl()
        {
            if (_refCounter.IsValid)
            {
                OnRefCountError(ErrorType.Leak);
            }
        }
#endif

        protected abstract void OnReleased();

        protected virtual void OnRefCountError(ErrorType error)
        {
            string text = $"Invalid MultiRef object of type {GetType()} usage. Error = {error}";
            Log.w(text);
#if TRACE_HISTORY
            var history = _tracer.Export();
            foreach (var stack in history)
            {
                Log.w(stack.Action + "\n" + stack.Stack.ToString());
            }
#endif
            Debug.Assert(false, text);
        }

        public bool IsAlive => _refCounter.IsValid;

        public void AddRef()
        {
#if TRACE_HISTORY_ALL
            Trace("AddRef");
#endif
            if (_refCounter.AddRef() == 0)
            {
                OnRefCountError(ErrorType.AddRefOfReleasedObject);
            }
        }

        public void Release()
        {
            int cnt = _refCounter.Release();

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
            _tracer.Clear();
            Trace("Revive");
#endif
            if (!_refCounter.Revive())
            {
                OnRefCountError(ErrorType.WrongReviveUsage);
                return false;
            }

            return true;
        }

        protected void AssertNoUsage()
        {
            if (_refCounter.IsValid)
            {
                OnRefCountError(ErrorType.NoUsageAssertionFail);
            }
        }

        public override string ToString()
        {
            return "RefCount:" + _refCounter;
        }
    }
}