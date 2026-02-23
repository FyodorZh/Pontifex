using Actuarius.Memory;
using Pontifex.Api;
using Scriba;
using TransportAnalyzer.TestLogic;

namespace Pontifex.Test
{
    public static class ApiFactory
    {
        public static ApiRoot Construct(string name, bool client, IMemoryRental memoryRental, ILogger logger)
        {
            switch (name)
            {
                case "silent":
                    return new SilentApi();
                case "brute":
                    return client ? new AckRawProtocol_Client(memoryRental, logger) : new AckRawProtocol_Server(memoryRental, logger);
                case "big":
                    return client ? new BigApiClient(1024 * 1024, logger) : new BigApiServer(logger);
                default:
                    throw new Exception();
            }
        }
    }
}