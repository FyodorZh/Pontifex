namespace Shared.NeoMeta.Items
{
    public class EquipmentItemStateRaw
    {
        public const int None = 0;
        public const int GradingUp = 1 << 1;
        public const int LevelingUp = 1 << 2;
        public const int WaitingForCollectGradeUp = 1 << 3;
        public const int WaitingForCollectLevelUp = 1 << 4;
    }
}
