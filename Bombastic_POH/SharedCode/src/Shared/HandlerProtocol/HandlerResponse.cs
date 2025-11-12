using Serializer.BinarySerializer;

namespace Shared.HandlerProtocol
{
    public class HandlerResponse : IDataStruct
    {
        private byte code;

        public enum ResponseCode : byte
        {
            Ok = 1,
            ServerError = 2
        }

        public HandlerResponse()
        {
        }

        public static HandlerResponse Success(IDataStruct data = null)
        {
            return new HandlerResponse
            {
                Data = data,
                code = (byte)ResponseCode.Ok
            };
        }

        public static HandlerResponse Error()
        {
            return new HandlerResponse
            {
                code = (byte)ResponseCode.ServerError
            };
        }

        public IDataStruct Data;

        public ResponseCode Code
        {
            get { return (ResponseCode)code; }
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref Data);
            dst.Add(ref code);

            return true;
        }

        public bool IsOk()
        {
            return Code == ResponseCode.Ok;
        }
    }
}
