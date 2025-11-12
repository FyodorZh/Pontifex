using Serializer.BinarySerializer;

namespace Shared.NeoMeta.PlayerData
{
    public partial class PlayerDataClient : IDataStruct
    {
        public int TimeOnline;
        public int LogicSessionOrderId;
        public int ConnectedOrderId;

        public PlayerDataClient()
        {
        }

        public PlayerDataClient(int timeOnline, int logicSessionOrderId, int connectedOrderId)
        {
            TimeOnline = timeOnline;
            LogicSessionOrderId = logicSessionOrderId;
            ConnectedOrderId = connectedOrderId;
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref TimeOnline);
            dst.Add(ref LogicSessionOrderId);
            dst.Add(ref ConnectedOrderId);

            return true;
        }
    }
}
