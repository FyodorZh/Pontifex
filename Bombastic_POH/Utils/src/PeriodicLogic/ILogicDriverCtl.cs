namespace Shared.Utils
{
    /// <summary>
    /// Отдаётся IPeriodicLogic для управления драйвером
    /// </summary>
    public interface ILogicDriverCtl
    {
        /// <summary>
        /// Останавливает процесс
        /// </summary>
        void Stop();

        /// <summary>
        /// Запущен ли процесс
        /// </summary>
        bool IsStarted { get; }

        /// <summary>
        /// Запрос на получение кванта вне очереди
        /// </summary>
        /// <returns> TRUE если запрос удовлетворён </returns>
        bool InvokeLogic();

        /// <summary>
        /// Логер
        /// </summary>
        ILogger Log { get; }
    }
}