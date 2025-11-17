using System;
using Fundamentum.Collections;

namespace Shared.Concurrent
{
    /// <summary>
    /// Сериализует обработку экшинов.
    /// Каждый экшин будет получать вызов TAction.Invoke() строго последовательно друг относительно друга
    /// </summary>
    /// <typeparam name="TAction"></typeparam>
    public class ActionQueue<TAction>: IConsumer<TAction>, IReleasable
        where TAction : struct, ActionQueue<TAction>.IAction
    {
        public interface IAction
        {
            /// <summary>
            /// Основное действие находится здесь, логика работает строго последовательно
            /// </summary>
            void Invoke();

            /// <summary>
            /// Если джоб не будет исполнен, то он получает Fail()
            /// Никаких гарантий относительно тредов нет!!!
            /// </summary>
            void Fail();
        }

        private readonly ConcurrentSerializedTicker mTicker;
        private readonly ConcurrentQueueValve<TAction> mQueue;

        public ActionQueue(IConcurrentQueue<TAction> queue)
        {
            mTicker = new ConcurrentSerializedTicker(OnTick);
            mQueue = new ConcurrentQueueValve<TAction>(queue, (action) =>
            {
                try
                {
                    action.Fail();
                }
                catch (Exception e)
                {
                    Log.wtf(e);
                }
            });
        }

        public bool Put(TAction action)
        {
            if (mQueue.Put(action))
            {
                mTicker.Tick();
                return true;
            }

            return false;
        }

        private void OnTick()
        {
            TAction action;
            if (!mQueue.TryPop(out action))
            {
                return;
            }

            try
            {
                action.Invoke();
            }
            catch (Exception e)
            {
                Log.wtf(e);
            }
        }

        public void Release()
        {
            mQueue.CloseValve();
        }
    }
}