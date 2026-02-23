namespace Pontifex.Api
{
    public abstract class Declaration : IDeclaration
    {
        private string _name = "";

        protected abstract void Start(bool isServerMode, IPipeSystem pipeSystem);

        public string Name => _name;

        public abstract void Stop();

        void IDeclaration.Start(bool isServerMode, IPipeSystem pipeSystem)
        {
            Start(isServerMode, pipeSystem);
        }

        void IDeclaration.SetName(string name)
        {
            _name = name;
        }
    }
}