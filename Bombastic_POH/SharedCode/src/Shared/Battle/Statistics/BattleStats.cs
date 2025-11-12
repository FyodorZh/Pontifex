using System.Collections.Generic;
using Serializer.BinarySerializer;
using Serializer.Extensions;

namespace Shared.Battle
{
    public struct StatsPart : IDataStruct
    {
        public int Kills;
        public int Deaths;
        public int Damage;
        public int Experience;

        public void AddKill()
        {
            Kills++;
        }

        public void AddExperience(int experience)
        {
            if (experience > 0)
            {
                Experience += experience;
            }
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref Kills);
            dst.Add(ref Deaths);
            dst.Add(ref Damage);
            dst.Add(ref Experience);

            return true;
        }
    }

    /// <summary>
    /// Сериализуется в JSON
    /// </summary>
    public class BattleStats : IDataStruct
    {
        public int Deaths;
        public int Assists;
        public StatsPart Hero;
        public StatsPart Creep;
        public StatsPart Tower;
        public StatsPart Neutral;
        public int DoubleKills;
        public int TripleKills;
        public int MaxKillingSpree;

        public void AddDamage(UnitClassValue targetUnitClass, string targetTag, int hpValue)
        {
            if (targetUnitClass.Matches(UnitClassType.Hero))
            {
                Hero.Damage += hpValue;
            }
            else if (targetUnitClass.Matches(UnitClassType.Minion))
            {
                Creep.Damage += hpValue;
            }
            else if (targetUnitClass.Matches(UnitClassType.Tower))
            {
                Tower.Damage += hpValue;
            }
            else if (targetUnitClass.Matches(UnitClassType.Neutral))
            {
                Neutral.Damage += hpValue;
            }
        }

        public virtual bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref Deaths);
            dst.Add(ref Assists);
            Hero.Serialize(dst);
            Creep.Serialize(dst);
            Tower.Serialize(dst);
            Neutral.Serialize(dst);
            dst.Add(ref DoubleKills);
            dst.Add(ref TripleKills);
            dst.Add(ref MaxKillingSpree);

            return true;
        }
    }

    /// <summary>
    /// Сериализуется в JSON
    /// </summary>
    public class BattlePlayerStats : BattleStats
    {
        public string Name;
        public Shared.CommonData.AccountType AccountType;
        public Spark UsedSpark;

        public byte Level;

        public List<long> BeginAfkTimes = new List<long>();
        public List<long> EndAfkTimes = new List<long>();
        public int AfkSeconds;

        public List<short> Items = new List<short>();

        public void SetUsedSpark(short descId, byte level)
        {
            UsedSpark = new Spark(descId, level);
        }

        public float GetKda()
        {
            return (float) (Hero.Kills + Assists) / (float) (Deaths > 0 ? Deaths : 1);
        }

        public void SetItems(IEnumerable<short> items)
        {
            Items.Clear();
            foreach (short itemId in items)
            {
                if (itemId != 0)
                {
                    Items.Add(itemId);
                }
            }
        }

        public bool ContainsItem(short descId)
        {
            int count = Items.Count;
            for (int i = 0; i < count; i++)
            {
                if (Items[i] == descId)
                {
                    return true;
                }
            }

            return false;
        }
        public override bool Serialize(IBinarySerializer dst)
        {
            if (!base.Serialize(dst))
            {
                return false;
            }

            dst.Add(ref Name);

            var atByte = (byte)AccountType;
            dst.Add(ref atByte);

            dst.Add(ref Level);

            if (dst.isReader)
            {
                AccountType = (Shared.CommonData.AccountType)atByte;
            }

            dst.AddList(ref BeginAfkTimes);
            dst.AddList(ref EndAfkTimes);
            dst.Add(ref AfkSeconds);

            dst.Add(ref UsedSpark);

            dst.AddList(ref Items);

            return true;
        }
    }

    public class BattleTeamStats : BattleStats
    {
        public override bool Serialize(IBinarySerializer dst)
        {
            if (!base.Serialize(dst))
            {
                return false;
            }

            return true;
        }
    }
}
