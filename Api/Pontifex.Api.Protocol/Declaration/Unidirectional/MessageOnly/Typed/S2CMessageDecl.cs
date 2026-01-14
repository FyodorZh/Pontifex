using Archivarius;

namespace Pontifex.Api
{
    public class S2CMessageDecl<TMessage> : MessageDecl<TMessage>
        where TMessage : struct, IDataStruct
    {
        protected override void Prepare(bool isServerMode, IPipeAllocator pipeAllocator)
        {
            if (isServerMode)
            {
                SetPipeIn(pipeAllocator.AllocateModelPipeIn<TMessage>());
            }
            else
            {
                SetPipeOut(pipeAllocator.AllocateModelPipeOut<TMessage>());
            }
        }
    }
}