using System;
using System.Threading;

namespace Transport.Protocols.MessageProtocol
{
    //TODO: Eliminate locks on client
    public class WaitResponse
    {
        private readonly object mMutex = new object();
        private readonly long mRequestId;
        public readonly short Command;
        private readonly Action<long> mOnTimeout;
        private readonly Timer mTimer;

        public readonly Action<Response> ResponseAction;

        private bool mExpiry;
        private bool mComplete;

        public WaitResponse(long requestId, short command, long timeout,
            Action<Response> responseAction, Action<long> onTimeout)
        {
            mRequestId = requestId;
            Command = command;
            ResponseAction = responseAction;
            mOnTimeout = onTimeout;
            mTimer = new Timer(OnTime, null, timeout, Timeout.Infinite);
        }

        private void OnTime(object state)
        {
            lock (mMutex)
            {
                if (mComplete)
                {
                    return;
                }
                mExpiry = true;
            }
            mOnTimeout.Invoke(mRequestId);
        }

        public bool CanComplete()
        {
            lock (mMutex)
            {
                if (!mExpiry)
                {
                    mComplete = true;
                    return true;
                }
            }
            return false;
        }

        public void Clear()
        {
            lock (mMutex)
            {
                mComplete = true;
                mTimer.Change(Timeout.Infinite, Timeout.Infinite);
                mTimer.Dispose();
            }
        }
    }
}
