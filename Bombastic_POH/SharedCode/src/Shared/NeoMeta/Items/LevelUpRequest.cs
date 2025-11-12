//using Serializer.BinarySerializer;
//
//namespace Shared.NeoMeta.Items
//{
//    public class LevelUpRequest : IDataStruct
//    {
//        public short ItemDescId;
//
//        public LevelUpRequest()
//        {
//        }
//
//        public LevelUpRequest(short heroItemDescId)
//        {
//            ItemDescId = heroItemDescId;
//        }
//
//        public bool Serialize(IBinarySerializer dst)
//        {
//            dst.Add(ref ItemDescId);
//
//            return true;
//        }
//
//        public class Response : IDataStruct
//        {
//            public ResultCode Result;
//
//            public Response()
//            {
//            }
//
//            public Response(ResultCode result)
//            {
//                Result = result;
//            }
//
//            public bool Serialize(IBinarySerializer dst)
//            {
//                var tmp = (byte)Result;
//                dst.Add(ref tmp);
//
//                if (dst.isReader)
//                {
//                    Result = (ResultCode)tmp;
//                }
//
//                return true;
//            }
//        }
//
//        public enum ResultCode : byte
//        {
//            Ok = 0,
//            RequirementsNotMatch = 1,
//            PriceNotMatch = 2,
//            MaxLevel = 4
//        }
//    }
//}
