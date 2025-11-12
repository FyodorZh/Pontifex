using Serializer.BinarySerializer;

namespace Shared
{
    public class MetaBotRuneSet : IDataStruct
    {
        public byte minLevel;
        public byte[] runeSet;

        public MetaBotRuneSet() { }

        public MetaBotRuneSet(byte minLevel, byte[] runeSet)
        {
            this.minLevel = minLevel;
            this.runeSet = runeSet;
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref runeSet);
            dst.Add(ref minLevel);
            return true;
        }
    }

}
