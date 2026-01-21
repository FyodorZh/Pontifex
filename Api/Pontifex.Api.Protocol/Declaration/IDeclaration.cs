namespace Pontifex.Api
{
    internal interface IDeclaration
    {
        void Start(bool isServerMode, IPipeSystem pipeSystem);

        void SetName(string name);
        string Name { get; }
        void Stop();
    }
}