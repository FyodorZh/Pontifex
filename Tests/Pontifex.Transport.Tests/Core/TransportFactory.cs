using Actuarius.Memory;
using Pontifex.Factory.Compilation;
using Pontifex.Factory.Registration;
using Scriba;

namespace Pontifex.Tests;

public class TransportFactory
{
    private readonly string _serverFormula;
    private readonly string _clientFormula;
    private readonly ITransportCompiler _serverCompiler;
    private readonly ITransportCompiler _clientCompiler;

    public TransportFactory(
        string serverFormula,
        string clientFormula,
        bool failIfError = true)
    {
        _serverFormula = serverFormula;
        _clientFormula = clientFormula;

        var memory = TransportRegistry.Memory;
        var logger = TransportRegistry.GetLogger(failIfError);

        _serverCompiler = TransportRegistry.Factory.CreateCompiler(
            TransportRole.Server,
            logger,
            memory);
        _clientCompiler = TransportRegistry.Factory.CreateCompiler(
            TransportRole.Client,
            logger,
            memory);
    }

    public ITransport BuildServer()
    {
        return TransportRegistry.Factory.Compile(_serverFormula, _serverCompiler);
    }

    public ITransport BuildClient()
    {
        return TransportRegistry.Factory.Compile(_clientFormula, _clientCompiler);
    }

    public override string ToString()
    {
        return _serverFormula;
    }
}
