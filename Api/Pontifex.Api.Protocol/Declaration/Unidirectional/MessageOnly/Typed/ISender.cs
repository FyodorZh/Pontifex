namespace Pontifex.Api.Protocol
{
    internal interface ISender<in TMessage>
    {
        SendResult Send(TMessage message);
    }
}