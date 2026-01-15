namespace Pontifex.Api
{
    internal interface IDeclaration
    {
        void Prepare(bool isServerMode, IPipeSystem pipeSystem);

        void SetName(string name);
        string Name { get; }
        void Stop();
    }
}