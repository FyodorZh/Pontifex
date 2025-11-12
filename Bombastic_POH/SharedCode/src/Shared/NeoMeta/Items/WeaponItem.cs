namespace Shared.NeoMeta.Items
{
    public partial class WeaponItem : BaseEquipmentItemClient
    {
        public WeaponItem()
        {
        }

        public WeaponItem(ID<Item> itemId, short descId, short level, short grade, int state, ID<Item> equippedOnHeroItemId, int? upgradeEndTime)
            : base(itemId, descId, level, grade, state, equippedOnHeroItemId, upgradeEndTime)
        {
        }

        public override byte ItemDescType
        {
            get { return Shared.CommonData.Plt.ItemType.WeaponId; }
        }
    }
}
