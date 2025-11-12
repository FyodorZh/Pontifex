using Serializer.BinarySerializer;
using Shared.Battle;

namespace Shared.Protocol
{
    public class FirstBattleDescription : IDataStruct
    {
        public long battleId;
        public PlayerDataSet playersSet;

        public FirstBattleDescription()
        {
        }

        public FirstBattleDescription(long battleId, PlayerDataSet playersSet)
        {
            this.battleId = battleId;
            this.playersSet = playersSet;
        }

        #region Implementation of IDataStruct

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref battleId);
            dst.Add(ref playersSet);
            return true;
        }

        #endregion

    }
}