using Serializer.BinarySerializer;
using Serializer.Extensions;

namespace Shared
{
    namespace CommonData
    {
        public interface IBattleItemsPreset
        {
            string Name { get; }
            short[] Preset { get; }
        }

        public class BattleItemsPreset : IBattleItemsPreset, IDataStruct
        {
            public string Name;
            public short[] Preset;

            short[] IBattleItemsPreset.Preset
            {
                get { return Preset; }
            }

            string IBattleItemsPreset.Name
            {
                get { return Name; }
            }

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref Preset);
                dst.Add(ref Name);

                return true;
            }
        }

        public interface IUnitDescriptionData : IDataStruct//TODO: add geters
        {
            Shared.Battle.UnitClassType UnitClassType { get; }
            CharacterRole CharacterRole { get; }
            short DescriptionId { get; }
            IAbilitySetData Abilities { get; }
            IAbilityCastAnchorsSet CastAnchors { get; }
            string Name { get; }
            UnitShapesMapData[] UnitLogicShapes { get; }
            bool IsFlipLocked { get; }
            short BikeSkinId { get; }
            float VisibilityRadius { get; }
            int[] BaseDeathExp { get; }
            int[] BaseDeathGold { get; }
            DeltaTime ReviveTimeout { get; }
            FloatGrowParameter HealthRegenerationBase { get; }
            IntGrowParameter Health { get; }
            FloatGrowParameter MagicResistance { get; }
            FloatGrowParameter Armor { get; }
            FloatGrowParameter AttackDamage { get; }
            FloatGrowParameter ArmorPenetration { get; }
            FloatGrowParameter CriticalStrikeChanceBase { get; }
            FloatGrowParameter CriticalStrikeDamageBase { get; }
            FloatGrowParameter AttackSpeedBase { get; }
            FloatGrowParameter LifeStealBase { get; }
            FloatGrowParameter SpeedBase { get; }
            InitRPGCustomParams CustomBase { get; }
            FloatGrowParameter AbilityPower { get; }
            FloatGrowParameter MagicPenetration { get; }
            FloatGrowParameter SpellVampBase { get; }
            FloatGrowParameter TenacityBase { get; }
            IUnitRunesData UnitRunes { get; }
            string FolderName { get; }

            IUnitSkinSetData SkinData { get; }
            IEquipmentData Equipment { get; }

            short[] MetaBotSparks { get; }

            MetaBotRuneSet[] MetaBotRuneSets { get; }
            IMVPCoefficients MVPCoefficients { get; }

            BattleItemsPreset[] BattleItemsPresets { get; }
        }

        public class UnitDescriptionData : IUnitDescriptionData
        {
            public Shared.Battle.UnitClassType UnitClassType;
            public CharacterRole CharacterRole;
            public short DescriptionId;
            public AbilitySetData Abilities;
            public AbilityCastAnchorsSet CastAnchors;
            public string Name;
            public UnitShapesMapData[] UnitLogicShapes;
            public bool IsFlipLocked;
            public short bikeSkinId;
            public float VisibilityRadius;
            public int[] BaseDeathExp;
            public int[] BaseDeathGold;
            public DeltaTime ReviveTimeout;
            public FloatGrowParameter HealthRegenerationBase;
            public IntGrowParameter Health;
            public FloatGrowParameter MagicResistance;
            public FloatGrowParameter Armor;
            public FloatGrowParameter AttackDamage;
            public FloatGrowParameter ArmorPenetration;
            public FloatGrowParameter CriticalStrikeChanceBase;
            public FloatGrowParameter CriticalStrikeDamageBase;
            public FloatGrowParameter AttackSpeedBase;
            public FloatGrowParameter LifeStealBase;
            public FloatGrowParameter SpeedBase;
            public InitRPGCustomParams CustomBase;
            public FloatGrowParameter AbilityPower;
            public FloatGrowParameter MagicPenetration;
            public FloatGrowParameter SpellVampBase;
            public FloatGrowParameter TenacityBase;
            public UnitRunesData UnitRunes;
            public string FolderName;

            public UnitSkinSetData mSkinData;

            public EquipmentData mEquipment;
            
            public short[] MetaBotSparks;

            public MetaBotRuneSet[] MetaBotRuneSets;

            public MVPCoefficients MVPCoeffs;
            public BattleItemsPreset[] BattleItemsPresets;

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref DescriptionId);
                dst.Add(ref Abilities);
                dst.Add(ref CastAnchors);
                dst.Add(ref Name);
                dst.Add(ref UnitLogicShapes);
                dst.Add(ref IsFlipLocked);
                dst.Add(ref bikeSkinId);
                dst.Add(ref VisibilityRadius);
                dst.Add(ref BaseDeathExp);
                dst.Add(ref BaseDeathGold);
                dst.AddDeltaTime(ref ReviveTimeout);
                dst.Add(ref Health);
                dst.Add(ref HealthRegenerationBase);
                dst.Add(ref MagicResistance);
                dst.Add(ref Armor);
                dst.Add(ref AttackDamage);
                dst.Add(ref ArmorPenetration);
                dst.Add(ref CriticalStrikeChanceBase);
                dst.Add(ref CriticalStrikeDamageBase);
                dst.Add(ref AttackSpeedBase);
                dst.Add(ref LifeStealBase);
                dst.Add(ref SpeedBase);
                dst.Add(ref CustomBase);
                dst.Add(ref AbilityPower);
                dst.Add(ref MagicPenetration);
                dst.Add(ref SpellVampBase);
                dst.Add(ref TenacityBase);
                dst.Add(ref UnitRunes);
                dst.Add(ref FolderName);

                int classType = (int)UnitClassType;
                dst.Add(ref classType);
                UnitClassType = (Shared.Battle.UnitClassType)classType;

                int characterRole = (int)CharacterRole;
                dst.Add(ref characterRole);
                CharacterRole = (CharacterRole)characterRole;

                dst.Add(ref mSkinData);
                dst.Add(ref mEquipment);
                dst.Add(ref MetaBotSparks);
                dst.Add(ref MetaBotRuneSets);
                dst.Add(ref MVPCoeffs);

                dst.Add(ref BattleItemsPresets);

                return true;
            }

            #region IUnitDescriptionData Members

            Shared.Battle.UnitClassType IUnitDescriptionData.UnitClassType { get { return UnitClassType; } }
            CharacterRole IUnitDescriptionData.CharacterRole  { get {  return CharacterRole; } }
            short IUnitDescriptionData.DescriptionId  { get {  return DescriptionId; } }
            IAbilitySetData IUnitDescriptionData.Abilities { get { return Abilities; } }
            IAbilityCastAnchorsSet IUnitDescriptionData.CastAnchors { get { return CastAnchors; } }
            string IUnitDescriptionData.Name  { get {  return Name; } }
            UnitShapesMapData[] IUnitDescriptionData.UnitLogicShapes { get { return UnitLogicShapes; } }
            bool IUnitDescriptionData.IsFlipLocked { get { return IsFlipLocked; } }
            short IUnitDescriptionData.BikeSkinId { get { return bikeSkinId; } }
            float IUnitDescriptionData.VisibilityRadius  { get {  return VisibilityRadius; } }
            int[] IUnitDescriptionData.BaseDeathExp  { get {  return BaseDeathExp; } }
            int[] IUnitDescriptionData.BaseDeathGold { get { return BaseDeathGold; } }
            DeltaTime IUnitDescriptionData.ReviveTimeout  { get {  return ReviveTimeout; } }
            FloatGrowParameter IUnitDescriptionData.HealthRegenerationBase { get { return HealthRegenerationBase; } }
            IntGrowParameter IUnitDescriptionData.Health  { get {  return Health; } }
            FloatGrowParameter IUnitDescriptionData.MagicResistance  { get {  return MagicResistance; } }
            FloatGrowParameter IUnitDescriptionData.Armor  { get {  return Armor; } }
            FloatGrowParameter IUnitDescriptionData.AttackDamage  { get {  return AttackDamage; } }
            FloatGrowParameter IUnitDescriptionData.ArmorPenetration  { get {  return ArmorPenetration; } }
            FloatGrowParameter IUnitDescriptionData.CriticalStrikeChanceBase { get { return CriticalStrikeChanceBase; } }
            FloatGrowParameter IUnitDescriptionData.CriticalStrikeDamageBase { get { return CriticalStrikeDamageBase; } }
            FloatGrowParameter IUnitDescriptionData.AttackSpeedBase { get { return AttackSpeedBase; } }
            FloatGrowParameter IUnitDescriptionData.LifeStealBase { get { return LifeStealBase; } }
            FloatGrowParameter IUnitDescriptionData.SpeedBase { get { return SpeedBase; } }
            InitRPGCustomParams IUnitDescriptionData.CustomBase { get { return CustomBase; } }
            FloatGrowParameter IUnitDescriptionData.AbilityPower  { get {  return AbilityPower; } }
            FloatGrowParameter IUnitDescriptionData.MagicPenetration  { get {  return MagicPenetration; } }
            FloatGrowParameter IUnitDescriptionData.SpellVampBase { get { return SpellVampBase; } }
            FloatGrowParameter IUnitDescriptionData.TenacityBase { get { return TenacityBase; } }
            IUnitRunesData IUnitDescriptionData.UnitRunes  { get {  return UnitRunes; } }
            string IUnitDescriptionData.FolderName  { get {  return FolderName; } }

            IUnitSkinSetData IUnitDescriptionData.SkinData { get { return mSkinData; } }
            IEquipmentData IUnitDescriptionData.Equipment { get { return mEquipment; } }
            short[] IUnitDescriptionData.MetaBotSparks { get { return MetaBotSparks; } }
            MetaBotRuneSet[] IUnitDescriptionData.MetaBotRuneSets { get { return MetaBotRuneSets; } }
            IMVPCoefficients IUnitDescriptionData.MVPCoefficients { get { return MVPCoeffs; } }
            BattleItemsPreset[] IUnitDescriptionData.BattleItemsPresets { get { return BattleItemsPresets; } }

            #endregion
        }
    }
}