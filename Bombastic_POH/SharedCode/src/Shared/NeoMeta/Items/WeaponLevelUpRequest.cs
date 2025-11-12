//using Serializer.BinarySerializer;
//
//namespace Shared.NeoMeta.Items
//{
//    public class WeaponLevelUpRequest : IDataStruct
//    {
//        public short WeaponItemDescId;
//
//        public WeaponLevelUpRequest()
//        {
//        }
//
//        public WeaponLevelUpRequest(short weaponItemDescId)
//        {
//            WeaponItemDescId = weaponItemDescId;
//        }
//
//        public bool Serialize(IBinarySerializer dst)
//        {
//            dst.Add(ref WeaponItemDescId);
//
//            return true;
//        }
//
//        public enum ResultCode : byte
//        {
//            Ok = 0,
//            RequirementsNotMatch = 1,
//            PriceNotMatch = 2,
//            MaxLevel = 3
//        }
//    }
//}
