using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class WeaponItemDescription : EquipmentItemDescription
    {
        [EditorField(EditorFieldParameter.UnityTexture)]
        public string _screenshot;

        [EditorField]
        private AbilitySlotData _mainAbility;

        [EditorField(EditorFieldParameter.UnityAsset)]
        private string _weaponPrefab;

        [EditorField(EditorFieldParameter.UnityAsset)]
        private string _weaponPreviewPrefab;

        public WeaponItemDescription()
        {
        }

        public override ItemType ItemDescType2
        {
            get { return Shared.CommonData.Plt.ItemType.Weapon; }
        }

        public string Screenshot
        {
            get { return _screenshot; }
        }

        public AbilitySlotData MainAbility
        {
            get { return _mainAbility; }
        }

        public string WeaponPrefab
        {
            get { return _weaponPrefab; }
        }

        public string WeaponPreviewPrefab
        {
            get { return _weaponPreviewPrefab; }
        }

        public AbilitySlotData MainAbilityCopy
        {
            get { return new AbilitySlotData(
                _mainAbility.SlotType,
                _mainAbility.SlotTypeId,
                _mainAbility.GlobalCooldown,
                _mainAbility.SlotItems,
                _mainAbility.OverviewHeader,
                _mainAbility.Overview,
                _mainAbility.CastCountLimit,
                _mainAbility.CastsInRound,
                _mainAbility.ClipReloadCooldown,
                _mainAbility.StartBackgroundReloadOffset,
                _mainAbility.IsAutoClipReload,
                _mainAbility.IsAutoConsumeCast,
                _mainAbility.RechargePoints,
                _mainAbility.RechargePointsPerSec,
                _mainAbility.ZeroChargesAtStart,
                _mainAbility.ModifyClipCount,
                _mainAbility.UpgradeSynonymId); }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            base.Serialize(dst);

            dst.Add(ref _screenshot);
            dst.Add(ref _mainAbility);
            dst.Add(ref _weaponPrefab);
            dst.Add(ref _weaponPreviewPrefab);

            return true;
        }


    }
}
