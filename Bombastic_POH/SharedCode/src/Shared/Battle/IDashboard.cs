namespace Shared.Battle
{
    public interface IDashboard
    {
        bool IsActive { get; }
        void Clear();
        void SetParam(string name, int value);
        void IncParam(string name, int delta);
        void AvgParam(string name, double value);
        void TrueAvgParam(string name, double value, double weight);
        void MinMaxParam(string name, int value);
        void MinMaxParam(string name, double value);
        EventHistoryCollector Event(string name);
    }

    public interface IDashboardForStatistics : IDashboard
    {
        bool GetMinMaxParam(string name, out int min, out int max);
        double GetDoubleParam(string name);
        int GetIntParam(string name);
    }

    public abstract class EventHistoryCollector
    {
        public abstract float TimeSliceEnd { get; set; }

        public abstract float TimeSliceLength { get; set; }

        public void AddEventNow()
        {
            AddEventNow(1);
        }

        public void AddEvent(float eventTime)
        {
            AddEvent(eventTime, 1);
        }

        public abstract void AddEventNow(int eventsNumber);
        public abstract void AddEvent(float eventTime, int eventsNumber);
    }

    public sealed class VoidEventHistoryCollector : EventHistoryCollector
    {
        public static readonly VoidEventHistoryCollector Instance = new VoidEventHistoryCollector();

        public override float TimeSliceEnd { get; set; }

        public override float TimeSliceLength { get; set; }

        public override void AddEventNow(int eventsNumber)
        {
            // DO NOTHING
        }

        public override void AddEvent(float eventTime, int eventsNumber)
        {
            // DO NOTHING
        }
    }
}