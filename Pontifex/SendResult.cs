namespace Transport
{
    public enum SendResult : byte
    {
        /// <summary>
        /// Отправка данных удалась, о факте доставки ничего не известно
        /// </summary>
        Ok,

        /// <summary>
        /// Фактический размер отсылаемых данных превышает максимально допустимое значение
        /// </summary>
        MessageToBig,

        /// <summary>
        /// Отсылаемые данные не коррктны (null?)
        /// </summary>
        InvalidMessage,

        /// <summary>
        /// Некорректный адрес отправки
        /// </summary>
        InvalidAddress,

        /// <summary>
        /// Подключение ещё не установлено или уже разорвано
        /// </summary>
        NotConnected,

        /// <summary>
        /// Переполнен буфер отсылаемых сообщений
        /// </summary>
        BufferOverflow,

        /// <summary>
        /// Любая неклассифицированная ошибка
        /// </summary>
        Error
    }
}
