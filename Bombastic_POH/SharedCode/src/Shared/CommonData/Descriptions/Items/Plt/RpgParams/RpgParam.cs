using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class RpgParam : IDataStruct
    {
        public static System.Collections.Generic.Dictionary<short, short> BATTLE_PARAMS_MAP = new System.Collections.Generic.Dictionary<short, short>
        {
            { MAX_HEALTH, (short)RpgParameter.MaxHealth },
            { ATTACK_DAMAGE, (short)RpgParameter.AttackDamage },
            { ABILITY_POWER, (short)RpgParameter.AbilityPower },
            { ARMOR, (short)RpgParameter.Armor },
            { CRIT_CHANCE, (short)RpgParameter.CriticalStrikeChance },
            { CRIT_POWER, (short)RpgParameter.CriticalStrikeDamage },
            { CD_REDUCTION, (short)RpgParameter.CooldownReduction },
            { VAMPIRIC, (short)RpgParameter.LifeSteal }
        };

        public const short POWER = 1;
        public const short MAX_HEALTH = 2;
        public const short ATTACK_DAMAGE = 3;
        public const short ABILITY_POWER = 4;
        public const short ARMOR = 5;
        public const short CRIT_CHANCE = 6;
        public const short CRIT_POWER = 7;
        public const short CD_REDUCTION = 8;
        public const short VAMPIRIC = 9;
        public const short HEALTH_PERCENT = 10;
        public const short ATTACK_DAMAGE_PERCENT = 11;
        public const short ADDITIONAL_ITEM_POWER_PERCENT = 12;//special param for HEALTH_PERCENT & ATTACK_DAMAGE_PERCENT balance logic, ask l.rastorguev PLT-5093

        [EditorField, EditorLink("Items", "Rpg Params")]
        private short _rpgParamDescId;
        [EditorField]
        private float _value;

        public RpgParam()
        {
        }

        public RpgParam(short rpgParamDescId, float value)
        {
            _rpgParamDescId = rpgParamDescId;
            _value = value;
        }

        public short RpgParamDescId
        {
            get { return _rpgParamDescId; }
        }

        public float Value
        {
            get { return _value; }
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _rpgParamDescId);
            dst.Add(ref _value);

            return true;
        }
    }
}