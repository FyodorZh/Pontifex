namespace Pontifex.Api
{
    internal interface ISender<in TMessage>
    {
        SendResult Send(TMessage message);
    }
}