namespace Shared.Concurrent
{
    /// <summary>
    /// Тредобезопасная стейтмашина реализующая логику информирования об отложенном желании сделать _одноразовое_ действие
    /// </summary>
    public sealed class Intention
    {
        public enum State
        {
            NoIntention = 0,  // желания ещё нет
            HasIntention = 1, // желание уже есть, но не реализованно
            HadIntention = 2  // желание было реализованно
        }

        private volatile int mIntention = 0;

        public State CurState
        {
            get { return (State)mIntention; }
        }

        /// <summary>
        /// Регистрирует желание
        /// </summary>
        /// <returns> true если желание зарегистрированно </returns>
        public bool Set()
        {
            int old = System.Threading.Interlocked.CompareExchange(ref mIntention, (int)State.HasIntention, (int)State.NoIntention);
            return old == (int)State.NoIntention;
        }

        /// <summary>
        /// Реализует зарегистрированное желание (если таковое имеется)
        /// </summary>
        /// <returns> true если желание реализованно </returns>
        public bool TryToRealize()
        {
            int old = System.Threading.Interlocked.CompareExchange(ref mIntention, (int)State.HadIntention, (int)State.HasIntention);
            return old == (int)State.HasIntention;
        }

        /// <summary>
        /// Одновременная регистрация желания и попытка реализовать его
        /// </summary>
        /// <returns> true если желание реализованно </returns>
        public bool SetAndRealize()
        {
            Set();
            return TryToRealize();
        }
    }
}