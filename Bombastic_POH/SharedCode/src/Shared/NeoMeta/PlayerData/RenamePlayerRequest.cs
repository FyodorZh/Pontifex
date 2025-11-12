using Serializer.BinarySerializer;

namespace Shared.NeoMeta
{
    public class RenamePlayerRequest : IDataStruct
    {
        public RenamePlayerRequest()
        {
        }

        public RenamePlayerRequest(string newPlayerName)
        {
            NewPlayerName = newPlayerName;
        }

        public string NewPlayerName;

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref NewPlayerName);
            return true;
        }

        public class Response : IDataStruct
        {
            public Response()
            {                
            }

            public Response(RenameStatus status)
            {
                Status = status;
            }

            public RenameStatus Status { get; set; }

            public bool Serialize(IBinarySerializer dst)
            {
                var rawStatus = (byte)Status;
                dst.Add(ref rawStatus);
                Status = (RenameStatus)rawStatus;
                return true;
            }

            public enum RenameStatus : byte
            {
                Incorrect,
                Busy,
                Success
            }
        }
    }
}
