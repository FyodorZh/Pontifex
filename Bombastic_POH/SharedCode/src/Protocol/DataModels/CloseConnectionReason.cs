namespace Shared.Protocol
{
    public enum CloseConnectionReason : byte
    {
        Unknown = 0,
        /// <summary>
        /// Игрок забанен
        /// </summary>
        PlayerBanned,
        /// <summary>
        /// Отключен из-за соединения с таким же clientId
        /// </summary>
        ConnectedFromOtherPlace,
        /// <summary>
        /// Клиент принудительно отключен
        /// </summary>
        KickClient,
        /// <summary>
        /// Закрытие из-за бездействия клиента
        /// </summary>
        InactiveTimeout,
        /// <summary>
        /// Отключение сервера
        /// </summary>
        ServerShutdown,
        /// <summary>
        /// E-HZ
        /// </summary>
        UnspecifiedError,
        /// <summary>
        /// Произошло истечение времени ожидания опознания клиента
        /// </summary>
        ConnectionTimeout
    }
}