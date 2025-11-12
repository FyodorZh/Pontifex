namespace Shared.Buffer
{
    public enum BufferElementType : byte // 4bit
    {
        Unknown,
        Bool,
        Byte,
        UInt16,
        Int32,
        Int64,
        Single,
        Double,
        Buffer,
        Array,
        AbstractArray, // Десериализуется как обычный Array
    }
}
