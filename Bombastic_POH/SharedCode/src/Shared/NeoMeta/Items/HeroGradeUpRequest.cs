//using Serializer.BinarySerializer;
//
//namespace Shared.NeoMeta.Items
//{
//    public class HeroGradeUpRequest : IDataStruct
//    {
//        public short HeroItemDescId;
//
//        public HeroGradeUpRequest()
//        {
//        }
//
//        public HeroGradeUpRequest(short heroItemDescId)
//        {
//            HeroItemDescId = heroItemDescId;
//        }
//
//        public bool Serialize(IBinarySerializer dst)
//        {
//            dst.Add(ref HeroItemDescId);
//
//            return true;
//        }
//
//        public enum ResultCode : byte
//        {
//            Ok = 0,
//            RequirementsNotMatch = 1,
//            PriceNotMatch = 2,
//            MaxGrade = 3
//        }
//    }
//}
