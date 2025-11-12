using Serializer.BinarySerializer;
using Serializer.Extensions;
using Shared.Battle;

namespace Shared.Protocol
{
    public class BattleOverallLoadingProgress : IDataStruct
    {
        public class BattleProgressPair : IDataStruct
        {
            public PlayerRole Role;
            public byte Progress;

            public bool Serialize(IBinarySerializer saver)
            {
                saver.Add(ref Role);
                saver.Add(ref Progress);
                return true;
            }
        }

        public BattleProgressPair[] ProgressPairs;

        public BattleOverallLoadingProgress() { }

        public BattleOverallLoadingProgress(BattleProgressPair[] progressPairs)
        {
            ProgressPairs = progressPairs;
        }

        public bool Serialize(IBinarySerializer saver)
        {
            saver.Add(ref ProgressPairs);
            return true;
        }
    }
}
