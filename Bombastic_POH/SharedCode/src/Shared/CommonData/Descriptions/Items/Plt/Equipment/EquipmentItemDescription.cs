using Serializer.BinarySerializer;
using Serializer.Extensions;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class EquipmentItemDescription : ItemBaseDescription,
        IWithLevels,
        IWithGrades,
        ICanHaveInstances
    {
        [EditorField, EditorLink("Items", "Heroes Classes")]
        private short _heroClassDescId;

        [EditorField, EditorLink("Items", "Equipment Types")]
        private short _equipmentTypeDescId;

        [EditorField, EditorLink("Items", "Equipment Rarities")]
        private short _equipmentRarityDescId;

        [EditorField]
        private EquipmentItemLevel[] _levels;

        [EditorField]
        private bool _autoLevelUp;

        [EditorField]
        private EquipmentItemLevel[] _grades;

        [EditorField]
        private short _startGrade;

        [EditorField]
        private float _order;

        [EditorField, EditorLink("Items", "Rpg Params")]
        private short _mainRpgParameterDescriptionId;

        [EditorField]
        private FakeRpgParameter[] _fakeRpgParameters;

        public EquipmentItemDescription()
        {
        }

        public override ItemType ItemDescType2
        {
            get { return Shared.CommonData.Plt.ItemType.Equipment; }
        }
        
//        public override bool CanHaveInstances
//        {
//            get { return true; }
//        }

        public short HeroClassDescId
        {
            get { return _heroClassDescId; }
        }

        public short EquipmentTypeDescId
        {
            get { return _equipmentTypeDescId; }
        }

        public short EquipmentRarityDescId
        {
            get { return _equipmentRarityDescId; }
        }

        public EquipmentItemLevel[] Levels
        {
            get { return _levels; }
        }

        public bool AutoLevelUp
        {
            get { return _autoLevelUp; }
        }

        public EquipmentItemLevel[] Grades
        {
            get { return _grades; }
        }

        public short StartGrade
        {
            get { return _startGrade > 0 ? _startGrade : (short)1; }
        }

        public float Order
        {
            get { return _order; }
        }

        public short MainRpgParameterDescriptionId
        {
            get { return _mainRpgParameterDescriptionId; }
        }

        public FakeRpgParameter[] FakeRpgParameters
        {
            get { return _fakeRpgParameters; }
        }

        ItemLevel[] IWithLevels.Levels
        {
            get { return Levels; }
        }

        ItemLevel[] IWithGrades.Grades
        {
            get { return Grades; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            base.Serialize(dst);

            dst.Add(ref _heroClassDescId);
            dst.Add(ref _equipmentTypeDescId);
            dst.Add(ref _equipmentRarityDescId);
            dst.Add(ref _levels);
            dst.Add(ref _autoLevelUp);
            dst.Add(ref _grades);
            dst.Add(ref _startGrade);
            dst.Add(ref _order);
            dst.Add(ref _mainRpgParameterDescriptionId);
            dst.Add(ref _fakeRpgParameters);

            return true;
        }

        public void AccumulateRpgParams(ref System.Collections.Generic.Dictionary<short, float> result, short grade, short level)
        {
            _levels.AccumulateRpgParams(ref result, level);
            _grades.AccumulateRpgParams(ref result, grade);
        }

        public float GetRpgParam(short paramId, short grade, short level)
        {
            return GetRpgParam(Levels, Grades, paramId, grade, level);
        }

        public static float GetRpgParam(EquipmentItemLevel[] levels, EquipmentItemLevel[] grades, short paramId, short grade, short level)
        {
            return
                levels.GetRpgParamValue(paramId, level) +
                grades.GetRpgParamValue(paramId, grade);
        }

        public class FakeRpgParameter : IDataStruct
        {
            [EditorField(EditorFieldParameter.LocalizedString)]
            public string Name;

            [EditorField]
            public float Value;

            [EditorField, EditorLink("Items", "Rpg Params")]
            public short? BaseRpgParameter;

            [EditorField]
            public bool ShowAsSeconds;

            [EditorField]
            public bool InverseComparison;

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref Name);
                dst.Add(ref Value);
                dst.AddNullable(ref BaseRpgParameter);
                dst.Add(ref ShowAsSeconds);
                dst.Add(ref InverseComparison);

                return true;
            }
        }
    }
}
