namespace Pontifex.Api.Protocol
{
    internal interface IDeclaration
    {
        void Prepare(bool isServerMode, IPipeAllocator pipeAllocator);

        void SetName(string name);
        string Name { get; }
        void Stop();
    }
}