using System;
using Archivarius;

namespace Pontifex.Api.Protocol
{
    public class C2SMessageDecl<TMessage> : MessageDecl<TMessage>
        where TMessage : struct, IDataStruct
    {
        protected override void Prepare(bool isServerMode, IPipeAllocator pipeAllocator)
        {
            if (isServerMode)
            {
                SetPipeOut(pipeAllocator.AllocateModelPipeOut<TMessage>());
            }
            else
            {
                SetPipeIn(pipeAllocator.AllocateModelPipeIn<TMessage>());
            }
        }
    }
}