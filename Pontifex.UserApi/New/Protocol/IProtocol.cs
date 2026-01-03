namespace Pontifex.UserApi
{
    internal interface IProtocol
    {
        IDeclaration[] Declarations { get; }
        ProtocolInfo GetInfo(IModelsHashDB modelHashes);
    }
}