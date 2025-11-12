//using Serializer.BinarySerializer;
//using Shared.CommonData.Attributes;
//
//namespace Shared.CommonData.Plt
//{
//    public class RpgParams : IDataStruct
//    {
//        [EditorField]
//        private RpgParam[] _items;
//
//        public RpgParams()
//        {
//        }
//
//        public RpgParams(RpgParam[] items)
//        {
//            _items = items;
//        }
//
//        public RpgParam[] Items
//        {
//            get { return _items; }
//        }
//
//        public bool Serialize(IBinarySerializer dst)
//        {
//            dst.Add(ref _items);
//
//            return true;
//        }
//    }
//}