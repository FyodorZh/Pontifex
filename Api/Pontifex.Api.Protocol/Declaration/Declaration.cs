namespace Pontifex.Api
{
    public abstract class Declaration : IDeclaration
    {
        private string _name = "";

        protected abstract void Prepare(bool isServerMode, IPipeAllocator pipeAllocator);

        public string Name => _name;

        public abstract void Stop();

        void IDeclaration.Prepare(bool isServerMode, IPipeAllocator pipeAllocator)
        {
            Prepare(isServerMode, pipeAllocator);
        }

        void IDeclaration.SetName(string name)
        {
            _name = name;
        }
    }
}