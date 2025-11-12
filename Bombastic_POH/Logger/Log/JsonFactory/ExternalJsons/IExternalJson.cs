namespace JsonFactory
{
    /// <summary>
    /// Представляет внешний json объект произвольной формы
    /// </summary>
    public interface IExternalJson
    {
        /// <summary>
        /// Записывает json объект в поток
        /// </summary>
        /// <param name="output"></param>
        void WriteTo(System.IO.TextWriter output);

        /// <summary>
        /// Записывает json объект в поток экранируя кавычки
        /// </summary>
        /// <param name="output"></param>
        void WriteToAsText(System.IO.TextWriter output);

        /// <summary>
        /// Сообщает объекту о прекращении его использования
        /// </summary>
        void Release();
    }
}
