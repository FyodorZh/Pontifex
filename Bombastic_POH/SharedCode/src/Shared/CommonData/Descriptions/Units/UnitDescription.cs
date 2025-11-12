using System.Collections.Generic;

namespace Shared
{
    namespace CommonData
    {
        public static class UnitTagHelper
        {
            public static bool ContainsSubstring(string source, string subString, bool ignoreCase)
            {
                if (!ignoreCase)
                {
                    return source.Contains(subString);
                }
                else
                {
                    return source.ToUpper().Contains(subString.ToUpper());
                }
            }
        }

        public interface IInitRPGParams
        {
            IntGrowParameter Health { get; }
            FloatGrowParameter HealthRegenerationBase { get; }
            FloatGrowParameter Armor { get; }
            FloatGrowParameter MagicResistance { get; }

            FloatGrowParameter AttackDamage { get; }
            FloatGrowParameter ArmorPenetration { get; }
            FloatGrowParameter CriticalStrikeChanceBase { get; }
            FloatGrowParameter CriticalStrikeDamageBase { get; }
            FloatGrowParameter AttackSpeedBase { get; }
            FloatGrowParameter LifeStealBase { get; }

            FloatGrowParameter AbilityPower { get; }
            FloatGrowParameter MagicPenetration { get; }
            FloatGrowParameter SpellVampBase { get; }
            FloatGrowParameter TenacityBase { get; }

            FloatGrowParameter SpeedBase { get; }

            InitRPGCustomParams CustomBase { get; }
        }

        public interface IUnitDescription
        {
            short DescriptionId { get; }
            string Name { get; }
        }

        public interface IInitUnitParams : IUnitDescription
        {
            Battle.UnitClassType ClassType { get; }
            UnitShapesMapData[] UnitLogicShapes { get; }
            bool IsFlipLocked { get; }
            float VisibilityRadius { get; }
            DeltaTime ReviveTimeout { get; }
        }

        public interface IInitParams : IInitUnitParams, IInitRPGParams
        {
            int[] BaseDeathExp { get; }
            int[] BaseDeathGold { get; }
        }

        public sealed class UnitDescription : IInitParams
        {
            public const string SKIN_BASE_NAME = "Base";

            public short DescriptionId { get { return mData.DescriptionId; } }

            public short BikeSkinId { get { return mData.BikeSkinId; } }

            //public PlayerRole Role { get; private set; }
            public Battle.UnitClassType ClassType { get { return mData.UnitClassType; } }
            public CharacterRole CharacterRole { get { return mData.CharacterRole; } }

            public IAbilitySetData Abilities { get { return mData.Abilities; } }
            public IAbilityCastAnchorsSet CastAnchors { get { return mData.CastAnchors; } }

            public string Name { get { return mData.Name; } }
            public string DisplayName { get { return mLocalizedDisplayName; } }
            public string Description { get { return mLocalizedDescription; } }
            public UnitShapesMapData[] UnitLogicShapes { get { return mData.UnitLogicShapes; } }
            public bool IsFlipLocked { get { return mData.IsFlipLocked; } }
            public float VisibilityRadius { get { return mData.VisibilityRadius; } }

            public int[] BaseDeathExp { get { return mData.BaseDeathExp; } }
            public int[] BaseDeathGold { get { return mData.BaseDeathGold; } }
            public DeltaTime ReviveTimeout { get { return mData.ReviveTimeout; } }

            public IntGrowParameter Health { get { return mData.Health; } }
            public FloatGrowParameter HealthRegenerationBase { get { return mData.HealthRegenerationBase; } }
            public FloatGrowParameter MagicResistance { get { return mData.MagicResistance; } }
            public FloatGrowParameter Armor { get { return mData.Armor; } }

            public FloatGrowParameter AttackDamage { get { return mData.AttackDamage; } }
            public FloatGrowParameter ArmorPenetration { get { return mData.ArmorPenetration; } }
            public FloatGrowParameter CriticalStrikeChanceBase { get { return mData.CriticalStrikeChanceBase; } }
            public FloatGrowParameter CriticalStrikeDamageBase { get { return mData.CriticalStrikeDamageBase; } }
            public FloatGrowParameter AttackSpeedBase { get { return mData.AttackSpeedBase; } }
            public FloatGrowParameter LifeStealBase { get { return mData.LifeStealBase; } }

            public FloatGrowParameter SpeedBase { get { return mData.SpeedBase; } }

            public InitRPGCustomParams CustomBase { get { return mData.CustomBase; } }

            public FloatGrowParameter AbilityPower { get { return mData.AbilityPower; } }
            public FloatGrowParameter MagicPenetration { get { return mData.MagicPenetration; } }
            public FloatGrowParameter SpellVampBase { get { return mData.SpellVampBase; } }
            public FloatGrowParameter TenacityBase { get { return mData.TenacityBase; } }
            public IUnitRunesData UnitRunes { get { return mData.UnitRunes; } }

            public byte[] ActiveRunes { get { return null; } }

            public IEquipmentData Equipment { get { return mData.Equipment; } }

            public int MetaBotSparksCount { get { return mData.MetaBotSparks != null ? mData.MetaBotSparks.Length : 0; } }
            public short[] MetaBotSparks { get { return mData.MetaBotSparks; } }

            private HashSet<short> metaBotSparksSet;
            public HashSet<short> MetaBotSparksSet
            {
                get
                {
                    if (metaBotSparksSet == null)
                    {
                        metaBotSparksSet = new HashSet<short>(MetaBotSparks);
                    }
                    return metaBotSparksSet;
                }
            }

            public MetaBotRuneSet[] MetaBotRuneSets { get { return mData.MetaBotRuneSets; } }
            public IMVPCoefficients MVPCoefficients { get { return mData.MVPCoefficients; } }
            public IBattleItemsPreset[] BattleItemsPresets { get { return mData.BattleItemsPresets; } }

            public int BattleItemsPresetsCount { get { return BattleItemsPresets == null ? 0 : BattleItemsPresets.Length; } }
            public short[] GetBattleItemsPreset(int presetIndex)
            {
                if (presetIndex < 0 || presetIndex >= BattleItemsPresetsCount)
                {
                    return null;
                }

                return BattleItemsPresets[presetIndex].Preset;
            }

            private readonly IUnitDescriptionData mData;
            private readonly UnitDescriptionResources mUnitDescriptionResources;
            private string mLocalizedDisplayName;
            private string mLocalizedDescription;

            public UnitDescription(IUnitDescriptionData data)
            {
                mData = data;
                mUnitDescriptionResources = new UnitDescriptionResources(mData.FolderName);
            }

            public void SetLocalizedDisplayName(string name)
            {
                mLocalizedDisplayName = name;
            }

            public void SetLocalizedDescription(string description)
            {
                mLocalizedDescription = description;
            }

            public IUnitSkinSetData SkinData
            {
                get { return mData.SkinData; }
            }

            public short GetMetaBotSpark(int index)
            {
                if (index < 0 || index >= MetaBotSparksCount)
                {
                    return 0;
                }

                return MetaBotSparks[index];
            }

            public string GetAbilityIcon(IDSByte<IAbilitySlotId> abilityTypeId, int abilityItemIndex = 0)
            {
                string result = null;
                IAbilitySlotData selectedSlot = null;
                var slots = mData.Abilities.Slots;
                foreach (var slot in slots)
                {
                    if (slot.SlotTypeId == abilityTypeId)
                    {
                        selectedSlot = slot;
                        break;
                    }
                }

                if (selectedSlot != null)
                {
                    result = selectedSlot.SlotItems[abilityItemIndex].AbilityIcon;
                }

                return result;
            }
        }
    }
}