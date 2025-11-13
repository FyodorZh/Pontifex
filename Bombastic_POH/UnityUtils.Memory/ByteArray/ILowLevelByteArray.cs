namespace Shared
{
    public interface ILowLevelByteArray: IByteArray
    {
        byte[] Array { get; }
        int Offset { get; }
        byte this[int id] { get; }
    }
}