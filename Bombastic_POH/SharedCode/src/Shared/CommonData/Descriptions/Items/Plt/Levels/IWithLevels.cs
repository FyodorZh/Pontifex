namespace Shared.CommonData.Plt
{
    public interface IWithLevels
    {
        bool AutoLevelUp { get; }

        ItemLevel[] Levels { get; }
    }
}
