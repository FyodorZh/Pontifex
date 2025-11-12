namespace Shared.CommonData.Plt
{
    public interface IWithLevel
    {
        short Level { get; }
        int? UpgradeEndTime { get; }
        bool IsLevelingUp { get; }
        bool IsWaitingLevelUpCollect { get; }
    }
}
