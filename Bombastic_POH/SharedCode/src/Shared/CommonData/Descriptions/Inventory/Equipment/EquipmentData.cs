using System;
using System.Collections.Generic;
using Serializer.BinarySerializer;

namespace Shared
{
    namespace CommonData
    {
        public interface IEquipmentData
        {
            int Count { get; }
            IEnumerable<IEquipmentItemData> Items { get; }

            IEquipmentItemData GetItem(int id);
            IEquipmentItemLevelData GetItemLevelData(byte id, byte level);

            int GetPower(byte id, byte level, bool additive);
            int GetPower(byte[] equipLevels, int maxRunesLevel, bool additive);
            void GetBuffs(byte id, byte level, IList<IBuffData> buffs, bool additive);
            void GetBuffs(byte[] equipLevels, IList<IBuffData> buffs, bool additive);
        }

        public static class MergeBuffsHelper
        {
            public static List<IBuffData> MergeBuffs(List<IBuffData> buffs)
            {
                if (buffs == null || buffs.Count == 0)
                {
                    return buffs;
                }

                List<IBuffData> newBuffs = new List<IBuffData>();

                foreach (var buff in buffs)
                {
                    bool bFound = false;
                    foreach (var newBuff in newBuffs)
                    {
                        if (newBuff.IsSame(buff))
                        {
                            (newBuff as BuffDataRaw).AddValue(buff.Value);
                            bFound = true;
                            break;
                        }
                    }
                    if (!bFound)
                    {
                        newBuffs.Add(new BuffDataRaw(buff));
                    }
                }

                return newBuffs;
            }
        }

        public class PowerBuffData : IDataStruct
        {
            public int BasePower;
            public int[] RunesPower;

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref BasePower);
                dst.Add(ref RunesPower);

                return true;
            }
        }

        public class EquipmentData : IEquipmentData, IDataStruct
        {
            public PowerBuffData PowerBuff;
            public EquipmentItemData[] Items;
            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref PowerBuff);
                dst.Add(ref Items);

                return true;
            }

            #region IEquipmentData

            IEnumerable<IEquipmentItemData> IEquipmentData.Items { get { return Items; } }

            public int Count { get { return Items != null ? Items.Length : 0; } }

            public IEquipmentItemData GetItem(int id)
            {
                if (id < 0 || id >= Count)
                {
                    return null;
                }
                return Items[id];
            }

            public IEquipmentItemLevelData GetItemLevelData(byte id, byte level)
            {
                IEquipmentItemData item = GetItem(id);
                if (item == null)
                {
                    return null;
                }
                return item.GetLevelData(level);
            }

            public int GetPower(byte id, byte level, bool additive)
            {               
                IEquipmentItemData item = GetItem(id);
                if (item == null)
                {
                    return 0;
                }

                return item.GetPower(level, additive);
            }

            public int GetPower(byte[] equipLevels, int maxRunesLevel, bool additive)
            {
                if (equipLevels == null)
                {
                    return 0;
                }

                int power = 0;
                if (PowerBuff != null)
                {
                    power += PowerBuff.BasePower;

                    int runsLevelCount = Math.Min(maxRunesLevel, PowerBuff.RunesPower != null ? PowerBuff.RunesPower.Length : 0);
                    for (int i = 0; i < runsLevelCount; i++)
                    {
                        power += PowerBuff.RunesPower[i];
                    }
                }

                int count = Math.Min(equipLevels.Length, Count);
                for (int i = 0; i < count; i++)
                {
                    IEquipmentItemData item = Items[i];
                    if (item != null)
                    {
                        power += item.GetPower(equipLevels[i], additive);
                    }
                }
                return power;
            }

            public void GetBuffs(byte id, byte level, IList<IBuffData> buffs, bool additive)
            {
                IEquipmentItemData item = GetItem(id);
                if (item == null)
                {
                    return;
                }
                item.GetBuffs(level, buffs, additive);
            }

            public void GetBuffs(byte[] equipLevels, IList<IBuffData> buffs, bool additive)
            {
                if (equipLevels == null)
                {
                    return;
                }

                int count = Math.Min(equipLevels.Length, Count);
                for (int i = 0; i < count; i++)
                {
                    IEquipmentItemData item = Items[i];
                    if (item != null)
                    {
                        item.GetBuffs(equipLevels[i], buffs, additive);
                    }
                }
            }

            #endregion
        }
    }
}
