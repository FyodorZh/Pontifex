namespace Shared
{
    public interface IOwner<TObject> : IMultiRef
    {
        TObject Value { get; }
    }
}