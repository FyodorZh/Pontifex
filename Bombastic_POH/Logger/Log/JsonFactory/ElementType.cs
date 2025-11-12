namespace JsonFactory
{
    public enum ElementType : byte
    {
        /// <summary>
        /// Неизвестный тип. Ошибка.
        /// </summary>
        Unknown,

        /// <summary>
        /// Строка
        /// </summary>
        String,

        /// <summary>
        /// Строка в виде комбинации (string format, object[] params). 
        /// </summary>
        StringFormat,

        /// <summary>
        /// Дробное число (double)
        /// </summary>
        Number,

        /// <summary>
        /// Булеан
        /// </summary>
        Bool,

        /// <summary>
        /// Целое число (64bit)
        /// </summary>
        Long,

        /// <summary>
        /// Внешний провайдер json объекта произвольной формы: {....}
        /// </summary>
        Json,

        /// <summary>
        /// json объект
        /// </summary>
        Object,

        /// <summary>
        /// json массив
        /// </summary>
        Array,
    }
}
