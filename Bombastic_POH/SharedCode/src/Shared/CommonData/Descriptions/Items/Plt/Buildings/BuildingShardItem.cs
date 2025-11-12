//using Serializer.BinarySerializer;
//using Shared.CommonData.Attributes;
//
//namespace Shared.CommonData.Plt
//{
//    public class BuildingShardItem : ItemBaseDescription
//    {
//        [EditorField, EditorLink("Items", "Items")]
//        private short _buildingItemId;
//
//        public BuildingShardItem()
//        {
//        }
//
//        public BuildingShardItem(short buildingItemId)
//        {
//            _buildingItemId = buildingItemId;
//        }
//
//        public short BuildingItemId
//        {
//            get { return _buildingItemId; }
//        }
//
//        public override bool Serialize(IBinarySerializer dst)
//        {
//            base.Serialize(dst);
//
//            dst.Add(ref _buildingItemId);
//
//            return true;
//        }
//    }
//}
