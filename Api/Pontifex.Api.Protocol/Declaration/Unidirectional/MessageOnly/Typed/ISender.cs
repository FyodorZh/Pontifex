namespace Pontifex.Api.Protocol
{
    internal interface ISender<in TMessage>
    {
        void Send(TMessage message);
    }
}