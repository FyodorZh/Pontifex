using Pontifex.Utils;

namespace Pontifex.RR.Unreliable.Ack
{
    /// <summary>
    /// Реализует бизнесс-логика
    /// </summary>
    public interface IRRServerAcknowledger<out THandler>
        where THandler : IRRAckServerHandler
    {
        THandler TryAck(UnionDataList ackData);
    }
}
