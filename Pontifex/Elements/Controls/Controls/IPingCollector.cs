namespace Pontifex.Abstractions.Controls
{
    public interface IPingCollector : IControl
    {
        bool CollectPing { get; set; }
        bool GetPing(out int minPing, out int maxPing, out int avgPing);
    }
}
