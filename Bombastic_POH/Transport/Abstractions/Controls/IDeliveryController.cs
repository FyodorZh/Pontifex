namespace Transport.Abstractions.Controls
{
    public interface IDeliveryController : IControl
    {
        int DeliveredPS { get; }
        int AttemptsPS { get; }
    }

    public interface IDeliveryControllerSink
    {
        void AttemptToDeliver(bool first);
    }
}