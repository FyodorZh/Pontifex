using System;

namespace Pontifex.Utils.FSM
{
    public delegate void StateChangedReaction<in TState>(TState oldState, TState newState);
    public delegate bool StateChangingPredicate<in TState>(TState oldState, TState newState);

    /// <summary>
    /// Абстракция стейтмашины
    /// </summary>
    /// <typeparam name="TState"> Тип стейта </typeparam>
    public interface IFSMBase<TState>
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
        /// Сбрасывает стейт в дефолтное положение
        /// </summary>
        void Reset();
    }

    public interface IFSM<TState> : IFSMBase<TState>
    {
        /// <summary>
        /// Попытка перевести текущий стейт в новый. При переводе вызывается делегат, который может отменить перевод в новый стейт
        /// </summary>
        /// <param name="nextState"></param>
        /// <param name="onStateChanging"></param>
        bool SetState(TState nextState, StateChangingPredicate<TState>? onStateChanging = null);
    }

    /// <summary>
    /// Тредобезопасная стейт машина. Все методы можно вызывать как угодно.
    /// Делегаты переданные в SetState() метод вызываются строго последовательно друг относительно друга
    /// </summary>
    /// <typeparam name="TState"></typeparam>
    public interface IConcurrentFSM<TState> : IFSMBase<TState>
    {
        /// <summary>
        /// Попытка перевести текущий стейт в новый. При переводе вызывается делегат, который может отменить перевод в новый стейт
        /// </summary>
        /// <param name="nextState"></param>
        /// <param name="onStateChanging"></param>
        void SetState(TState nextState, StateChangingPredicate<TState>? onStateChanging = null);
    }
}