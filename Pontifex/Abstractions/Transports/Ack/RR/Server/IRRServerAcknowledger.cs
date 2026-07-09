using Pontifex.Utils;

namespace Pontifex.Ack.RR
{
    /// <summary>
    /// Реализует бизнесс-логика
    /// </summary>
    public interface IRRServerAcknowledger<out THandler>
        where THandler : IAckRRServerHandler
    {
        THandler TryAck(UnionDataList ackData);
    }
}
