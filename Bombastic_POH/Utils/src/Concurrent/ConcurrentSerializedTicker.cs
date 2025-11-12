using System;

namespace Shared.Concurrent
{
    /// <summary>
    /// Сериализирует конкурентные вызовы Tick().
    /// На каждый Tick() будет однократно вызыван onTick().
    /// Гарантируется строгая последовательность вызовов onTick()
    /// </summary>
    public class ConcurrentSerializedTicker
    {
        private readonly Action mOnTick;

        private int mCount;

        public ConcurrentSerializedTicker(Action onTick)
        {
            mOnTick = onTick;
        }

        public void Tick()
        {
            if (System.Threading.Interlocked.Increment(ref mCount) == 1)
            {
                while (true)
                {
                    mOnTick();
                    if (System.Threading.Interlocked.Decrement(ref mCount) == 0)
                    {
                        break;
                    }
                }
            }
        }
    }
}