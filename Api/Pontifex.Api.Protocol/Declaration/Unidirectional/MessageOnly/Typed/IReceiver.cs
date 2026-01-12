using System;

namespace Pontifex.Api.Protocol
{
    internal interface IReceiver<out TMessage>
    {
        void SetProcessor(Action<TMessage> processor);
    }
}