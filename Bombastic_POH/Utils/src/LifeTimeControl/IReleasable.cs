namespace Shared
{
    /// <summary>
    /// Любой объект допускающий владение собой
    /// </summary>
    public interface IReleasable
    {
        /// <summary>
        /// Информирует объект об завершении факта владения
        /// </summary>
        void Release();
    }
}