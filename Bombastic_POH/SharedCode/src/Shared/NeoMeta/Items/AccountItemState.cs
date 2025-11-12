namespace Shared.NeoMeta.Items
{
    public static class AccountItemState
    {
        public const int None = 0;
        public const int LevelingUp = 1 << 0;
        public const int WaitingForCollectLevelUp = 1 << 1;
    }
}
