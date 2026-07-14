using Actuarius.Memory;
using Pontifex.Factory;
using Scriba;

namespace Pontifex.Tests;

public class TransportFactory
{
    private readonly IMemoryRental _memory;
    private readonly ILogger _logger;

    private readonly IDescription _serverDesc;
    private readonly IDescription _clientDesc;

    public TransportFactory(IDescription serverDesc, IDescription clientDesc, bool failIfError = true)
    {
        _serverDesc = serverDesc;
        _clientDesc = clientDesc;
        _memory = TransportRegistry.Memory;
        _logger = TransportRegistry.GetLogger(failIfError);
    }

    public ITransport BuildServer()
    {
        return TransportRegistry.Builder.BuildServer(_serverDesc, _memory, _logger);
    }

    public ITransport BuildClient()
    {
        return TransportRegistry.Builder.BuildClient(_clientDesc, _memory, _logger);
    }

    public override string ToString()
    {
        return _serverDesc.ToString() ?? "";
    }
}
