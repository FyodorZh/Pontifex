//#define TRACE_HISTORY

using System;
using System.Diagnostics;

namespace Shared
{
    /// <summary>
    /// Объект у которого может быть только один владелец
    /// </summary>
    public interface ISingleRef : IReleasable
    {
    }

    public abstract class SingleRefImpl : ISingleRef
    {
        protected enum ErrorType
        {
            ReleaseOfReleasedObject,
            Leak,
            WrongReviveUsage,
        }

        private volatile int mIsValid;

#if TRACE_HISTORY
        private readonly ActionHistoryTracer mTracer = new ActionHistoryTracer();
#endif

        protected SingleRefImpl()
        {
            mIsValid = 1;
#if TRACE_HISTORY
            mTracer.RecordEvent("Ctor");
#endif
        }

        ~SingleRefImpl()
        {
            if (mIsValid != 0)
            {
                OnRefCountError(ErrorType.Leak);
            }
        }

        protected abstract void OnReleased();

        protected virtual void OnRefCountError(ErrorType error)
        {
#if TRACE_HISTORY
            var history = mTracer.Export();
#endif
            string text = "Invalid SingleRef object of type " + GetType() + " usage. Error = " + error;
            Log.w(text);
            Debug.Assert(false, text);
        }

        public void Release()
        {
            if (System.Threading.Interlocked.Exchange(ref mIsValid, 0) == 1)
            {
                OnReleased();
                //GC.SuppressFinalize(this);
            }
            else
            {
                OnRefCountError(ErrorType.ReleaseOfReleasedObject);
            }
        }

        protected void Revive()
        {
            if (System.Threading.Interlocked.Exchange(ref mIsValid, 1) != 0)
            {
                OnRefCountError(ErrorType.WrongReviveUsage);
            }
        }
    }
}