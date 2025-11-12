using System;

namespace Shared.Concurrent
{
    public class ConcurrentManualLockGate
    {
        private readonly Action mOnClose;

        private volatile int mState;

        public ConcurrentManualLockGate(Action onClose)
        {
            mOnClose = onClose;
            mState = EncodeState(true, 0);
            Enter();
        }

        private static void DecodeState(int state, out bool isOpen, out int count)
        {
            isOpen = (state & 1) != 0;
            count = state >> 1;
        }

        private static int EncodeState(bool isOpen, int count)
        {
            return (count << 1) + (isOpen ? 1 : 0);
        }

        public void TryClose()
        {
            while (true)
            {
                int oldState = mState;
                bool isOpen;
                int count;
                DecodeState(oldState, out isOpen, out count);

                if (isOpen)
                {
                    isOpen = false;
                    if (System.Threading.Interlocked.CompareExchange(ref mState, EncodeState(isOpen, count), oldState) == oldState)
                    {
                        Exit();
                        return;
                    }
                }
                else
                {
                    return;
                }
            }
        }

        public bool Enter()
        {
            while (true)
            {
                int oldState = mState;
                bool isOpen;
                int count;
                DecodeState(oldState, out isOpen, out count);

                if (isOpen)
                {
                    count += 1;
                    if (System.Threading.Interlocked.CompareExchange(ref mState, EncodeState(isOpen, count), oldState) == oldState)
                    {
                        return true;
                    }
                    continue;
                }

                break;
            }

            return false;
        }

        public void Exit()
        {
            while (true)
            {
                int oldState = mState;
                bool isOpen;
                int count;
                DecodeState(oldState, out isOpen, out count);

                count -= 1;

                if (System.Threading.Interlocked.CompareExchange(ref mState, EncodeState(isOpen, count), oldState) == oldState)
                {
                    if (count == 0)
                    {
                        mOnClose();
                    }

                    break;
                }
            }
        }
    }
}