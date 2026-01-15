using Archivarius;

namespace Pontifex.Api
{
    public class S2CMessageDecl<TMessage> : MessageDecl<TMessage>
        where TMessage : struct, IDataStruct
    {
        protected override void Prepare(bool isServerMode, IPipeSystem pipeSystem)
        {
            if (isServerMode)
            {
                SetPipeIn(pipeSystem.AllocateModelPipeIn<TMessage>());
            }
            else
            {
                SetPipeOut(pipeSystem.AllocateModelPipeOut<TMessage>());
            }
        }
    }
}