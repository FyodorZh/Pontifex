namespace Shared.NeoMeta.Items
{
    public class BuildingItemState
    {
        public const int None = 0;
        public const int GradingUp = 1 << 0;
        public const int WaitingForCollectGradeUp = 1 << 1;
        public const int WorkInProgress = 1 << 2;
    }
}



