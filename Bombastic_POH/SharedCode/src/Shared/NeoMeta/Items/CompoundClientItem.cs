//using Serializer.BinarySerializer;
//using Shared.CommonData.Plt;
//
//namespace Shared.NeoMeta.Items
//{
//    public class CompoundClientItem : Item
//        , IWithCount
//    {
//        public CompoundClientItem()
//        {
//        }
//
//        public CompoundClientItem(ID<Item> itemId, short descId, int count)
//            : base(itemId, descId)
//        {
//            Count = count;
//        }
//
//        public override byte ItemDescType
//        {
//            get { return ItemType.CompoundId; }
//        }
//
//        public int Count;
//
//        int IWithCount.Count
//        {
//            get { return Count; }
//        }
//
//        public override bool Serialize(IBinarySerializer dst)
//        {
//            dst.Add(ref Count);
//
//            return base.Serialize(dst);
//        }
//
//        public override string ToString()
//        {
//            return string.Format("{0}, Count: {1}", base.ToString(), Count);
//        }
//    }
//}
