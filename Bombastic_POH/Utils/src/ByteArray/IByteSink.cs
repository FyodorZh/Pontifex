namespace Shared
{
    /// <summary>
    /// Аккумулятор байтов
    /// </summary>
    public interface IByteSink
    {
        /// <summary>
        /// Вставляет 1 байт.
        /// Бросает исключение в случае неуспеха
        /// </summary>
        /// <param name="val"></param>
        void Push(byte val);

        /// <summary>
        /// Вставляет массив байт.
        /// Бросает исключение в случае неуспеха
        /// </summary>
        /// <param name="bytes"></param>
        void Push<TBytes>(TBytes bytes) where TBytes : IByteArray;
    }
}