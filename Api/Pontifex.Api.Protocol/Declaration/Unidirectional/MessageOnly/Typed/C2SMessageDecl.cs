using Archivarius;

namespace Pontifex.Api
{
    public class C2SMessageDecl<TMessage> : MessageDecl<TMessage>
        where TMessage : struct, IDataStruct
    {
        protected override void Prepare(bool isServerMode, IPipeSystem pipeSystem)
        {
            if (isServerMode)
            {
                SetPipeOut(pipeSystem.AllocateModelPipeOut<TMessage>());
            }
            else
            {
                SetPipeIn(pipeSystem.AllocateModelPipeIn<TMessage>());
            }
        }
    }
}