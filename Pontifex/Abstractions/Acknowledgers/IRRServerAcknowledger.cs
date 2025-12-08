using Pontifex.Utils;

namespace Transport.Abstractions.Acknowledgers
{
    /// <summary>
    /// Реализует бизнесс-логика
    /// </summary>
    public interface IRRServerAcknowledger<out THandler>
        where THandler : Handlers.Server.IAckRRServerHandler
    {
        THandler TryAck(UnionDataList ackData);
    }
}
