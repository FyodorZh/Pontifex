using System.Collections.Generic;
using Serializer.BinarySerializer;

namespace Shared
{
    namespace CommonData
    {
        public interface IEquipmentItemLevelData
        {
            int Power { get; }
            int Count { get; }
            IEnumerable<IBuffData> Data { get; }
            void FillBuffs(IList<IBuffData> buffs);
        }

        public class EquipmentItemLevelData : IEquipmentItemLevelData, IDataStruct
        {
            public int Power;
            public BuffData[] Data;

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref Power);
                dst.Add(ref Data);

                return true;
            }

            #region IEquipmentItemLevelData

            int IEquipmentItemLevelData.Power { get { return Power; } }
            public int Count { get { return Data != null ? Data.Length : 0; } }
            IEnumerable<IBuffData> IEquipmentItemLevelData.Data { get { return Data; } }

            public void FillBuffs(IList<IBuffData> buffs)
            {
                if (Data == null || buffs == null)
                {
                    return;
                }

                foreach (BuffData buff in Data)
                {
                    buffs.Add(buff);
                }
            }

            #endregion
        }
    }
}
