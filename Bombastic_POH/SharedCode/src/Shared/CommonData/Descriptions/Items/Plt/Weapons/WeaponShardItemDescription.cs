using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class WeaponShardItemDescription : ShardItemDescription
    {
        [EditorField, EditorLink("Items", "Items")]
        private short _weaponItemDescId;

        public WeaponShardItemDescription()
        {
        }

        public WeaponShardItemDescription(short weaponItemDescId, string name)
        {
            _weaponItemDescId = weaponItemDescId;
        }

        public override ItemType ItemDescType2
        {
            get { return Shared.CommonData.Plt.ItemType.WeaponShard; }
        }

        public short WeaponItemDescId
        {
            get { return _weaponItemDescId; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _weaponItemDescId);

            return base.Serialize(dst);
        }
    }
}
