using System;

namespace Pontifex.Api
{
    internal interface IReceiver<out TMessage>
    {
        void SetProcessor(Action<TMessage> processor);
    }
}