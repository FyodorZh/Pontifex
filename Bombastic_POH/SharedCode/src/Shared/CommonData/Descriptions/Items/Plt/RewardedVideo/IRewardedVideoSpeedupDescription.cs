namespace Shared.CommonData.Plt
{
    public interface IRewardedVideoSpeedupDescription
    {
        int SpeedupPercent { get; }
        
        System.TimeSpan SpeedupStep { get; }
        
        System.TimeSpan MinSpeedupTime { get; }
        
        System.TimeSpan MaxSpeedupTime { get; }
    }
}