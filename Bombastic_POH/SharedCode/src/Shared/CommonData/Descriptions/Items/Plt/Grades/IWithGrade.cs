namespace Shared.CommonData.Plt
{
    public interface IWithGrade
    {
        short Grade { get; }
        int? UpgradeEndTime { get; }
        bool IsGradingUp { get; }
        bool IsWaitingGradeUpCollect { get; }
    }
}
