namespace Shared.CommonData.Plt
{
    public abstract class ShardItemDescription : ItemBaseDescription,
        ICanBeInPrice,
        IWithCounts
    {
        public int MaxCount
        {
            get { return 0; }
        }
    }
}
