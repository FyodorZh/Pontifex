namespace Shared.Pool
{
    public interface IPoolStatistics
    {
        int PeakUsedCount { get; }
        int UsingCount { get; }
        int TotalAllocatedCount { get; }
        int FreeObjectsCount { get; }

        void ClearStatistics();
    }
}
