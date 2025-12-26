namespace Pontifex.Abstractions.Controls
{
    public interface ICCUController : IControl
    {
        /// <summary>
        /// Текщее количество активных клиентов
        /// </summary>
        int CCU { get; }
    }
}