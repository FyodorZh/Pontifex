namespace Pontifex.Utils.FSM
{
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
}