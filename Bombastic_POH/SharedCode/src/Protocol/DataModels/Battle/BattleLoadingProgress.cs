using Serializer.BinarySerializer;
using Serializer.Extensions;
using Shared.Battle;

namespace Shared.Protocol
{
    public class BattleLoadingProgress : IDataStruct
    {
        private PlayerRole mRole;
        private byte mProgress;

        public PlayerRole Role { get { return mRole; } }
        public int Progress { get { return mProgress; } }

        public BattleLoadingProgress() { }

        public BattleLoadingProgress(PlayerRole role, int progress)
        {
            mRole = role;
            mProgress = (byte)progress;
        }

        public BattleLoadingProgress(PlayerRole role, byte progress)
        {
            mRole = role;
            mProgress = progress;
        }

        public bool Serialize(IBinarySerializer saver)
        {
            saver.Add(ref mRole);
            saver.Add(ref mProgress);
            return true;
        }
    }
}
