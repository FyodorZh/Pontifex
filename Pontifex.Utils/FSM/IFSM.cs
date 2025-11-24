namespace Pontifex.Utils.FSM
{
    public delegate bool StateChangeReaction<in TState>(TState oldState, TState newState);

    /// <summary>
    /// Абстракция стейтмашины
    /// </summary>
    /// <typeparam name="TState"> Тип стейта </typeparam>
    public interface IFSM<TState>
    {
        /// <summary>
        /// Стартовый (дефолтный) стейт
        /// </summary>
        TState InitState { get; }

        /// <summary>
        /// Текущий стейт
        /// </summary>
        TState State { get; }

        /// <summary>
        /// Сбрасывает стейт в деолтное положение
        /// </summary>
        void Reset();

        /// <summary>
        /// Попытка перевести текущий стейт в новый. При переводе вызывается делегат, который может отменить перевод в новый стейт
        /// </summary>
        /// <param name="nextState"></param>
        /// <param name="onStateChanged"></param>
        void SetState(TState nextState, StateChangeReaction<TState>? onStateChanged = null);
    }

    /// <summary>
    /// Настройка стейт машины
    /// </summary>
    public interface IFSM_Ctl<in TState>
    {
        /// <summary>
        /// Регистрирует допустимый переход между двумя стейтами
        /// </summary>
        /// <returns></returns>
        bool AddTransition(TState fromState, TState toState);

        /// <summary>
        /// Регистрирует допустимые переходы в пакетном режиме
        /// </summary>
        /// <returns></returns>
        bool AddTransitions(TState[] fromStates, TState toState);
    }

    /// <summary>
    /// Тредобезопасная стейт машина. Все методы можно вызывать как угодно.
    /// Делегаты переданные в SetState() метод вызываются строго последовательно друг относительно друга
    /// </summary>
    /// <typeparam name="TState"></typeparam>
    public interface IConcurrentFSM<TState> : IFSM<TState>
    {
    }
}