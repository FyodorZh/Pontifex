namespace Transport
{
    /// <summary>
    /// Причина разрушения подключения к транспортной системе.
    /// ТРЕБУЕТ ДОРАБОТКИ
    /// </summary>
    public enum DisconnectReason
    {
        /// <summary>
        /// Не удалось подключиться по одной из двух причин:
        /// - не удалось подключиться к транспорту
        /// - не удалось пройти acknowledgment
        /// </summary>
        ConnectionFailed,

        /// <summary>
        /// Отключение произошло после успешного acknowledgmentа
        /// </summary>
        Disconnected,

        /// <summary>
        /// Отключение было вызвано принудительно (вызов метода Disconnect)
        /// </summary>
        Forcibly
    }
}
