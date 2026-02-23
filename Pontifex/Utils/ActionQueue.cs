using System;
using Actuarius.Collections;
using Actuarius.Concurrent;
using Actuarius.Memory;
using Scriba;

namespace Pontifex.Utils
{
    /// <summary>
    /// Сериализует обработку экшинов.
    /// Каждый экшин будет получать вызов TAction.Invoke() строго последовательно друг относительно друга
    /// </summary>
    /// <typeparam name="TAction"></typeparam>
    public class ActionQueue<TAction>: IConsumer<TAction>, IReleasableResource
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

        private readonly ConcurrentSerializedExecutor _serializedExecutor;
        private readonly ConcurrentQueueValve<TAction> _queue;

        public ActionQueue(IConcurrentQueue<TAction> queue)
        {
            _serializedExecutor = new ConcurrentSerializedExecutor(OnTick);
            _queue = new ConcurrentQueueValve<TAction>(queue, (action) =>
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
            if (_queue.Put(action))
            {
                _serializedExecutor.ScheduleOneInvokation();
                return true;
            }

            return false;
        }

        private void OnTick()
        {
            while (_queue.TryPop(out var action))
            {
                try
                {
                    action.Invoke();
                }
                catch (Exception e)
                {
                    Log.wtf(e);
                }
            }
        }

        public void Release()
        {
            _queue.CloseValve();
        }
    }
}