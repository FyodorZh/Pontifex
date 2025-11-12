namespace Shared.CommonData.Plt
{
    public interface IRandom
    {
        int Next(int maxValue);

        int Next(int minValue, int maxValue);
    }
}
