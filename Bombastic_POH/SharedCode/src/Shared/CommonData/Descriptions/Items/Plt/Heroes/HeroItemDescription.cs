using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class HeroItemDescription : ItemBaseDescription,
        IWithLevels,
        IWithGrades
    {
        [EditorField("Hero Id")]
        private short _heroDescId;

        [EditorField, EditorLink("Items", "Heroes Rarities")]
        private short _heroRarityDescId;

        [EditorField, EditorLink("Items", "Heroes Classes")]
        private short[] _heroClassDescIds;

        [EditorField]
        private ItemLevel[] _grades;

        [EditorField]
        private bool _autoLevelUp;

        [EditorField]
        private ItemLevel[] _levels;

        [EditorField]
        private short _startGrade;

        [EditorField, EditorLink("Items", "Items")]
        private short _defaultWeaponItemDescriptionId;

        [EditorField]
        private float _order;

        [EditorField, EditorLink("Items", "Items")]
        private short[] _heroesSkinsDescIds;

        public HeroItemDescription()
        {
        }

        public override ItemType ItemDescType2
        {
            get { return Shared.CommonData.Plt.ItemType.Hero; }
        }

        public short HeroDescId
        {
            get { return _heroDescId; }
        }

        public short HeroRarityDescId
        {
            get { return _heroRarityDescId; }
        }

        public short[] HeroClassDescIds
        {
            get { return _heroClassDescIds; }
        }

        public ItemLevel[] Grades
        {
            get { return _grades; }
        }

        public bool AutoLevelUp
        {
            get { return _autoLevelUp; }
        }

        public ItemLevel[] Levels
        {
            get { return _levels; }
        }

        public short StartGrade
        {
            get { return _startGrade > 0 ? _startGrade : (short)1; }
        }

        public short DefaultWeaponItemDescriptionId
        {
            get { return _defaultWeaponItemDescriptionId; }
        }

        public float Order
        {
            get { return _order; }
        }

        public short[] HeroesSkinsDescIds
        {
            get { return _heroesSkinsDescIds; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _heroDescId);
            dst.Add(ref _heroRarityDescId);
            dst.Add(ref _heroClassDescIds);
            dst.Add(ref _grades);
            dst.Add(ref _autoLevelUp);
            dst.Add(ref _levels);
            dst.Add(ref _startGrade);
            dst.Add(ref _defaultWeaponItemDescriptionId);
            dst.Add(ref _order);
            dst.Add(ref _heroesSkinsDescIds);

            return base.Serialize(dst);
        }

        public override string ToString()
        {
            return string.Format("{0}, HeroDescId: {1}, Name: {2}, HeroRarityDescId: {3}, HeroClassDescIds: {4}, Grades: {5}, AutoLevelUp: {6}, Levels: {7}, StartGrade: {8}", base.ToString(), HeroDescId, Name, HeroRarityDescId, HeroClassDescIds, Grades, AutoLevelUp, Levels, StartGrade);
        }

        public void AccumulateRpgParams(ref System.Collections.Generic.Dictionary<short, float> result, short grade, short level)
        {
            float oldPower;
            result.TryGetValue(RpgParam.POWER, out oldPower);

            float oldPowerCoef;
            result.TryGetValue(RpgParam.ADDITIONAL_ITEM_POWER_PERCENT, out oldPowerCoef);

            _levels.AccumulateRpgParams(ref result, level);
            _grades.AccumulateRpgParams(ref result, grade);

            float powerCoef;
            if (result.TryGetValue(RpgParam.ADDITIONAL_ITEM_POWER_PERCENT, out powerCoef))
            {
                float power;
                if (result.TryGetValue(RpgParam.POWER, out power))
                {
                    result[RpgParam.POWER] = (power - oldPower) * (1.0f + powerCoef - oldPowerCoef);
                    result.Remove(RpgParam.ADDITIONAL_ITEM_POWER_PERCENT);
                }
            }
        }

        public float GetRpgParam(short paramId, short grade, short level)
        {
            return GetRpgParam(Levels, Grades, paramId, grade, level);
        }

        public static float GetRpgParam(ItemLevel[] levels, ItemLevel[] grades, short paramId, short grade, short level)
        {
            float result = levels.GetRpgParamValue(paramId, level) +
                           grades.GetRpgParamValue(paramId, grade);

            if (paramId == RpgParam.POWER)
            {
                float powerCoef = levels.GetRpgParamValue(RpgParam.ADDITIONAL_ITEM_POWER_PERCENT, level) +
                                  grades.GetRpgParamValue(RpgParam.ADDITIONAL_ITEM_POWER_PERCENT, grade);
                result *= 1.0f + powerCoef;
            }

            return result;
        }

        public byte[] GetRunesData(short targetGrade)
        {
            byte[] runes = null;
            int activeRunes = targetGrade - StartGrade;
            if (activeRunes > 0)
            {
                runes = new byte[activeRunes];
                for (byte i = 0; i < activeRunes; ++i)
                {
                    runes[i] = i;
                }
            }

            return runes;
        }
    }
}
