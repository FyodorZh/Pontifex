namespace Shared.Utils
{
    /// <summary>
    /// Интерфейс драйвера логики.
    /// Позволяет одновременно запускать несколько логик
    /// </summary>
    public interface IPeriodicMultiLogicDriver
    {
        /// <summary>
        /// Количество запущенных логик
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Инициализирует и запускает драйвер. Вызывается однократно
        /// </summary>
        /// <param name="logger"> Логер </param>
        /// <returns> Факт успешного запуска </returns>
        bool Start(ILogger logger);

        /// <summary>
        /// Добавляет логику к запущенному драйверу. Требует успешного вызова Start()
        /// </summary>
        /// <param name="logic"> Запускаемая логика </param>
        /// <returns> Успешно ли</returns>
        ILogicDriverCtl Append(IPeriodicLogic logic, DeltaTime period);

        /// <summary>
        /// Останавливает драйвер. После вызова этого метода Append() использовать нельзя
        /// </summary>
        void Stop();
    }
}