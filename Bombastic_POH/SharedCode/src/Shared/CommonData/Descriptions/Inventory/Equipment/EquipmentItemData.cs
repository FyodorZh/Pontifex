using System;
using System.Collections.Generic;
using Serializer.BinarySerializer;

namespace Shared
{
    namespace CommonData
    {
        public interface IEquipmentItemData
        {
            short RecipeId { get; }
            int Levels { get; }
            IEnumerable<IEquipmentItemLevelData> LevelsData { get; }
            IEquipmentItemLevelData GetLevelData(int level);
            int GetPower(int level, bool additive);
            void GetBuffs(int level, IList<IBuffData> buffs, bool additive);
        }

        public class EquipmentItemData : IEquipmentItemData, IDataStruct
        {
            public short RecipeId;
            public EquipmentItemLevelData[] LevelsData;

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref RecipeId);
                dst.Add(ref LevelsData);

                return true;
            }

            #region IEquipmentItemData

            short IEquipmentItemData.RecipeId { get { return RecipeId; } }
            IEnumerable<IEquipmentItemLevelData> IEquipmentItemData.LevelsData { get { return LevelsData; } }
            public int Levels { get { return LevelsData != null ? LevelsData.Length : 0; } }

            public IEquipmentItemLevelData GetLevelData(int level)
            {
                if (level <= 0 || level > Levels)
                {
                    return null;
                }
                return LevelsData[level - 1];
            }

            public int GetPower(int level, bool additive)
            {
                int power = 0;
                if (additive)
                {
                    level = Math.Min(level, Levels);
                    for (int i = 0; i < level; i++)
                    {
                        power += LevelsData[i].Power;
                    }
                }
                else
                {
                    var levelData = GetLevelData(level);
                    if (levelData != null)
                    {
                        power = levelData.Power;
                    }
                }
                return power;
            }

            public void GetBuffs(int level, IList<IBuffData> buffs, bool additive)
            {
                if (additive)
                {
                    level = Math.Min(level, Levels);
                    for (int i = 0; i < level; i++)
                    {
                        LevelsData[i].FillBuffs(buffs);
                    }
                }
                else
                {
                    var levelData = GetLevelData(level);
                    if (levelData != null)
                    {
                        levelData.FillBuffs(buffs);
                    }
                }
            }            

            #endregion
        }
    }
}
