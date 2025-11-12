using Serializer.BinarySerializer;

namespace Shared.Protocol
{
    public class CheatData : IDataStruct
    {
        public const int MAX_LENGTH = 64;

        public string command;

        public CheatData()
        {
        }

        public CheatData(string command)
        {
            this.command = command;
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref command);
            return true;
        }

        public bool validate()
        {
            if (null == command)
            {
                return false;
            }
            var c = command.Trim();
            return c.Length > 0 && c.Length <= MAX_LENGTH;
        }
    }
}