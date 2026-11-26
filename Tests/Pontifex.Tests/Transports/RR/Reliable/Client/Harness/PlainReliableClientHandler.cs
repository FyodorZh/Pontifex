using Pontifex.RR.Reliable;
using Pontifex.Utils;

namespace Pontifex.Tests.Transports;

/// <summary>
/// A handler that implements the interface without deriving from <see cref="RRRequestHandler"/>.
/// </summary>
internal sealed class PlainReliableClientHandler : IRRReliableRequestHandler
{
    public ushort? RequestId => null;

    public void Cancel()
    {
    }

    public void Response(UnionDataList response)
    {
        response.Release();
    }

    public void Finish(RRReliableCompletionType type)
    {
    }
}
