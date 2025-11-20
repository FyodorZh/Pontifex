using Actuarius.Memory;

namespace Shared
{
    /// <summary>
    /// Абстрактное тредобезопасное хранилище последовательности байт с контролем владения.
    /// Обещает быть иммутабельной!!!
    /// </summary>
    public interface IMultiRefLowLevelByteArray : IMultiRefByteArray, IReadOnlyByteArray
    {
    }
}