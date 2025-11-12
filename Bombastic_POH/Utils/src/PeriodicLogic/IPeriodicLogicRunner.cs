namespace Shared.Utils
{
    /// <summary>
    /// Запускает логику с помощью внутреннего драйвера
    /// Можно использовать многократно
    /// </summary>
    public interface IPeriodicLogicRunner
    {
        ILogicDriverCtl Run(IPeriodicLogic logicToRun, DeltaTime period);
    }
}