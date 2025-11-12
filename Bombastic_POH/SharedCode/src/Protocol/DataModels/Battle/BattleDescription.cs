using Serializer.BinarySerializer;
using Serializer.Extensions;
using Serializer.Extensions.ID;
using Shared.Battle;

namespace Shared.Protocol
{
    public class BattleDescription : IDataStruct
    {
        private BattleTopology mTopology;
        private PlayerRole mRole;
        private IDLong<IClient> mClientId;
        private BattleScriptType mScriptType;
        private MatchMakerBasketType mBasketType;
        private PlayerDataSet mPlayersData;
        private string mMissionId;
        private int mPlayerLevel;
        private long mBattleId;
        private bool mIsInBattle;

        private ResourcesList mRequiredResources;

        public BattleTopology Topology { get { return mTopology; } }
        public PlayerRole Role { get { return mRole; } }
        public IDLong<IClient> ClientId { get { return mClientId; } }
        public BattleScriptType ScriptType { get { return mScriptType; } }
        public MatchMakerBasketType BasketType { get { return mBasketType; } }
        public PlayerDataSet PlayersData { get { return mPlayersData; } }
        public int PlayerLevel { get { return mPlayerLevel; } }
        public long BattleId { get { return mBattleId; } }
        public bool IsInBattle { get { return mIsInBattle; } }
        public string MissionId { get { return mMissionId; } }

        public BattleDescription()
        {
        }

        public BattleDescription(BattleTopology topologyModel, PlayerRole role, IDLong<IClient> clientId, string missionId, BattleScriptType scriptType, MatchMakerBasketType basketType, PlayerDataSet playersData, long battleId, ResourcesList requiredResources, bool isInBattle)
        {
            mTopology = topologyModel;
            mRole = role;
            mClientId = clientId;
            mMissionId = missionId;
            mScriptType = scriptType;
            mBasketType = basketType;
            mPlayersData = playersData;
            mBattleId = battleId;
            mRequiredResources = requiredResources;
            mIsInBattle = isInBattle;
        }

        public bool Serialize(IBinarySerializer saver)
        {
            saver.Add(ref mTopology);
            saver.Add(ref mRole);
            saver.AddId(ref mClientId);
            saver.Add(ref mPlayerLevel);

            byte sct = (byte) mScriptType;
            saver.Add(ref sct);
            mScriptType = (BattleScriptType) sct;

            byte bt = (byte) mBasketType;
            saver.Add(ref bt);
            mBasketType = (MatchMakerBasketType) bt;

            if (saver.isReader)
            {
                mPlayersData = new PlayerDataSet();
            }
            mPlayersData.Serialize(saver);

            saver.Add(ref mBattleId);
            saver.Add(ref mMissionId);
            saver.Add(ref mIsInBattle);

            saver.Add(ref mRequiredResources);
            return true;
        }
    }
}