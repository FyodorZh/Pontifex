namespace Pontifex.Api
{
    public abstract class Declaration : IDeclaration
    {
        private string _name = "";

        protected abstract void Prepare(bool isServerMode, IPipeSystem pipeSystem);

        public string Name => _name;

        public abstract void Stop();

        void IDeclaration.Prepare(bool isServerMode, IPipeSystem pipeSystem)
        {
            Prepare(isServerMode, pipeSystem);
        }

        void IDeclaration.SetName(string name)
        {
            _name = name;
        }
    }
}