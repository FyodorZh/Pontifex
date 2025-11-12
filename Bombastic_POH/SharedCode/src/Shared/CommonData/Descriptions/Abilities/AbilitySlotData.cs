﻿using System.Collections.Generic;
using Serializer.BinarySerializer;
using Serializer.Extensions;
using Shared.CommonData.Attributes;

namespace Shared
{
    namespace CommonData
    {
        public interface IAbilitySlotId
        {
        }

        public interface IAbilitySlotData
        {
            AbilitySlotType SlotType { get; }
            IDSByte<IAbilitySlotId> SlotTypeId { get; }
            DeltaTime GlobalCooldown { get; }
            int NotEmptySlotItemsCount { get; }
            int SlotItemsCount { get; }
            IAbilitySlotItemData[] SlotItems { get; }
            string OverviewHeader { get; }
            string Overview { get; }

            ushort CastCountLimit { get; }
            byte CastsInRound { get; }
            DeltaTime ClipReloadCooldown { get; }
            DeltaTime StartBackgroundReloadOffset { get; }
            bool IsAutoClipReload { get; }
            bool IsAutoConsumeCast { get; }
            byte RechargePoints { get; }
            byte RechargePointsPerSec { get; }
            bool ZeroChargesAtStart { get; }
            bool ModifyClipCount { get; }

            IDSByte<IAbilitySlotId> UpgradeSynonymId { get; }
        }

        public class AbilitySlotData : IAbilitySlotData, IDataStruct
        {
            [EditorField]
            public AbilitySlotType SlotType = AbilitySlotType.Invalid;
            [EditorField]
            public int SlotTypeId = -1;
            [EditorField]
            public DeltaTime GlobalCooldown;
            [EditorField]
            public AbilitySlotItemData[] SlotItems;
            [EditorField]
            public string OverviewHeader;
            [EditorField]
            public string Overview;

            [EditorField]
            public ushort CastCountLimit;
            [EditorField]
            public byte CastsInRound;
            [EditorField]
            public DeltaTime ClipReloadCooldown;
            [EditorField]
            public DeltaTime StartBackgroundReloadOffset;
            [EditorField]
            public bool IsAutoClipReload;
            [EditorField]
            public bool IsAutoConsumeCast;
            [EditorField]
            public byte RechargePoints;
            [EditorField]
            public byte RechargePointsPerSec;
            [EditorField]
            public bool ZeroChargesAtStart;
            [EditorField]
            public bool ModifyClipCount;

            [EditorField]
            public int UpgradeSynonymId = -1;

            public AbilitySlotData() { }

            public AbilitySlotData(
                AbilitySlotType slotType,
                int slotTypeId,
                DeltaTime globalCooldown,
                AbilitySlotItemData[] slotItems,
                string overviewHeader,
                string overview,
                ushort castCountLimit,
                byte castsInRound,
                DeltaTime clipReloadCooldown,
                DeltaTime startBackgroundReloadOffset,
                bool isAutoClipReload,
                bool isAutoConsumeCast,
                byte rechargePoints,
                byte rechargePointsPerSec,
                bool zeroChargesAtStart,
                bool modifyClipCount,
                int upgradeSynonymId)
            {
                SlotType = slotType;
                SlotTypeId = slotTypeId;
                GlobalCooldown = globalCooldown;
                SlotItems = slotItems;
                OverviewHeader = overviewHeader;
                Overview = overview;
                CastCountLimit = castCountLimit;
                CastsInRound = castsInRound;
                ClipReloadCooldown = clipReloadCooldown;
                StartBackgroundReloadOffset = startBackgroundReloadOffset;
                IsAutoClipReload = isAutoClipReload;
                IsAutoConsumeCast = isAutoConsumeCast;
                RechargePoints = rechargePoints;
                RechargePointsPerSec = rechargePointsPerSec;
                ZeroChargesAtStart = zeroChargesAtStart;
                ModifyClipCount = modifyClipCount;
                UpgradeSynonymId = upgradeSynonymId;
            }

            public bool Serialize(IBinarySerializer dst)
            {
                byte type = (byte)SlotType;
                dst.Add(ref type);
                SlotType = (AbilitySlotType)type;

                sbyte st = (sbyte)SlotTypeId;
                dst.Add(ref st);
                SlotTypeId = st;

                dst.AddDeltaTime(ref GlobalCooldown);
                dst.Add(ref SlotItems);
                dst.Add(ref OverviewHeader);
                dst.Add(ref Overview);
                dst.Add(ref CastCountLimit);
                dst.Add(ref CastsInRound);
                dst.AddDeltaTime(ref ClipReloadCooldown);
                dst.AddDeltaTime(ref StartBackgroundReloadOffset);
                dst.Add(ref IsAutoClipReload);
                dst.Add(ref IsAutoConsumeCast);
                dst.Add(ref RechargePoints);
                dst.Add(ref RechargePointsPerSec);
                dst.Add(ref ZeroChargesAtStart);
                dst.Add(ref ModifyClipCount);
                dst.Add(ref UpgradeSynonymId);

                return true;
            }

            AbilitySlotType IAbilitySlotData.SlotType { get { return SlotType; } }
            IDSByte<IAbilitySlotId> IAbilitySlotData.SlotTypeId { get { return IDSByte<IAbilitySlotId>.DeserializeFrom((sbyte)SlotTypeId); } }
            DeltaTime IAbilitySlotData.GlobalCooldown { get { return GlobalCooldown; } }
            IAbilitySlotItemData[] IAbilitySlotData.SlotItems { get { return SlotItems; } }
            IDSByte<IAbilitySlotId> IAbilitySlotData.UpgradeSynonymId { get { return IDSByte<IAbilitySlotId>.DeserializeFrom((sbyte)UpgradeSynonymId); } }

            string IAbilitySlotData.OverviewHeader { get { return OverviewHeader; } }
            string IAbilitySlotData.Overview { get { return Overview; } }
            public int SlotItemsCount { get { return SlotItems != null ? SlotItems.Length : 0; } }
            ushort IAbilitySlotData.CastCountLimit { get { return CastCountLimit; } }
            byte IAbilitySlotData.CastsInRound { get { return CastsInRound; } }
            DeltaTime IAbilitySlotData.ClipReloadCooldown { get { return ClipReloadCooldown; } }
            DeltaTime IAbilitySlotData.StartBackgroundReloadOffset { get { return StartBackgroundReloadOffset; } }
            bool IAbilitySlotData.IsAutoClipReload { get { return IsAutoClipReload; } }
            bool IAbilitySlotData.IsAutoConsumeCast { get { return IsAutoConsumeCast; } }
            byte IAbilitySlotData.RechargePoints { get { return RechargePoints; } }
            byte IAbilitySlotData.RechargePointsPerSec { get { return RechargePointsPerSec; } }
            bool IAbilitySlotData.ZeroChargesAtStart { get { return ZeroChargesAtStart; } }
            bool IAbilitySlotData.ModifyClipCount { get { return ModifyClipCount; } }

            public int NotEmptySlotItemsCount
            {
                get
                {
                    int notEmptyCount = 0;
                    int count = SlotItemsCount;
                    for (int i = 0; i < count; i++)
                    {
                        if (SlotItems[i].AbilityId >= 0)
                        {
                            notEmptyCount++;
                        }
                    }
                    return notEmptyCount;
                }
            }
        }
    }
}