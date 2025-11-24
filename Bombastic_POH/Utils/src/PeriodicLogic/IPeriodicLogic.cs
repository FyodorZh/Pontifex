using System;
using Pontifex;

namespace Shared.Utils
{
    /// <summary>
    /// Бизнес логика реализует IPeriodicLogic для получения периодических квантов процессорного времени
    /// </summary>
    public interface IPeriodicLogic
    {
        /// <summary>
        /// Срабатывает единожды перед прочими методами
        /// </summary>
        /// <param name="driver"> Источник квантов. Тот кто выделяет процессорное время </param>
        /// <returns> FALSE - логика не смогла инициализироваться. Будет вызван LogicStopped() </returns>
        bool LogicStarted(ILogicDriverCtl driver);

        /// <summary>
        /// Срабатывает периодически после вызова LogicStarted()
        /// </summary>
        void LogicTick();

        /// <summary>
        /// Срабатывает единожды при завершении периодического процесса.
        /// </summary>
        void LogicStopped();
    }

    public static class PeriodicLogicChecker
    {
        public static IPeriodicLogic Test(this IPeriodicLogic core, Action<string> onFail)
        {
#if DEBUG
            return new Wrapper(core, onFail);
#else
            return core;
#endif
        }

        private class Wrapper : InvariantChecker<Wrapper.LogicState>, IPeriodicLogic
        {
            public enum LogicState
            {
                Constructed,
                Started,
                Stopped
            }

            private readonly IPeriodicLogic mLogic;

            private int mFlag = 0;

            public Wrapper(IPeriodicLogic logic, Action<string> onFail)
                : base(0, onFail)
            {
                mLogic = logic;
            }

            protected override int FromState(LogicState state)
            {
                return (int)state;
            }

            protected override LogicState ToState(int state)
            {
                return (LogicState)state;
            }

            bool IPeriodicLogic.LogicStarted(ILogicDriverCtl driver)
            {
                bool res;
                BeginCriticalSection(ref mFlag);
                {
                    CheckState(LogicState.Constructed);
                    res = mLogic.LogicStarted(driver);
                    if (res)
                    {
                        SetState(LogicState.Started);
                    }
                }
                EndCriticalSection(ref mFlag);
                return res;
            }

            void IPeriodicLogic.LogicTick()
            {
                BeginCriticalSection(ref mFlag);
                {
                    CheckState(LogicState.Started);
                    mLogic.LogicTick();
                }
                EndCriticalSection(ref mFlag);
            }

            void IPeriodicLogic.LogicStopped()
            {
                BeginCriticalSection(ref mFlag);
                {
                    if (State == LogicState.Stopped)
                    {
                        Fail();
                    }
                    SetState(LogicState.Stopped);
                    mLogic.LogicStopped();
                }
                EndCriticalSection(ref mFlag);
            }
        }
    }
}