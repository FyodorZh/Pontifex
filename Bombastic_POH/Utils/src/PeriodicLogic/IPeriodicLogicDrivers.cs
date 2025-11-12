namespace Shared.Utils
{
    /// <summary>
    /// Интерфейс драйвера логики.
    /// Позволяет запускать 1 логику
    /// </summary>
    public interface IPeriodicLogicDriver
    {
        /// <summary>
        /// Запускает логику. Можно использовать единожды.
        /// logic.LogicStarted() будет вызван синхронно текущего треда
        /// </summary>
        /// <param name="logic"> Запускаемая логика </param>
        /// <param name="logger"> Легер </param>
        /// <returns> Удалось ли запустить драйвер </returns>
        bool Start(IPeriodicLogic logic, ILogger logger);

        /// <summary>
        /// Период с которым тикает драйвер
        /// </summary>
        DeltaTime Period { get; }
    }
}