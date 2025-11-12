namespace Shared.NeoMeta.Items
{
    public class HeroItemState
    {
        public const int None = 0;
        public const int HeroTaskExecuting = 1 << 0;
        public const int GradingUp = 1 << 1;
        public const int LevelingUp = 1 << 2;
        public const int WaitingForCollectGradeUp = 1 << 3;
        public const int WaitingForCollectLevelUp = 1 << 4;
        public const int WaitingForCollectHeroTask = 1 << 5;
        public const int HoldingSlot = 1 << 6;
        public const int Mining = 1 << 7;
        public const int Crafting = 1 << 8;
    }
}