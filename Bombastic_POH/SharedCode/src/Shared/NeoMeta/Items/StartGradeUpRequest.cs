//using Serializer.BinarySerializer;
//
//namespace Shared.NeoMeta.Items
//{
//    public class StartGradeUpRequest : IDataStruct
//    {
//        public short ItemDescId;
//
//        public StartGradeUpRequest()
//        {
//        }
//
//        public StartGradeUpRequest(short itemDescId)
//        {
//            ItemDescId = itemDescId;
//        }
//
//        public bool Serialize(IBinarySerializer dst)
//        {
//            dst.Add(ref ItemDescId);
//
//            return true;
//        }
//
//        public enum ResultCode : byte
//        {
//            Ok = 0,
//            RequirementsNotMatch = 1,
//            PriceNotMatch = 2,
//            MaxGrade = 3,
//            CantUpgrading = 4
//        }
//    }
//}
