using Actuarius.Memory;
using Pontifex;
using Pontifex.Raw.Reliable;
using Pontifex.Utils;

namespace Pontifex.Tests;

/// <summary>
/// Regression tests for high-load server send-result ownership.
/// </summary>
[TestFixture]
[Category("TransportTestSupport")]
public sealed class HighLoadDataTransferSendResultTests
{
    /// <summary>
    /// InvalidMessage leaves the acquired server response reference with the caller for release.
    /// </summary>
    [Test]
    public void SendServerResponse_WithInvalidMessage_ReleasesTheAcquiredReference()
    {
        var response = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
        response.PutLast(new UnionData(1));
        var endpoint = new Endpoint(SendResult.InvalidMessage);

        try
        {
            var result = Pontifex.Raw.Reliable.Tests.HighLoadDataTransfer.SendServerResponse(
                endpoint, response, id: 1);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(SendResult.InvalidMessage));
                Assert.That(response.RefCount.RefCount, Is.EqualTo(1),
                    "only the caller-owned original reference remains");
                Assert.That(endpoint.DisconnectCount, Is.EqualTo(1));
            });
        }
        finally
        {
            response.Release();
        }
    }

    private sealed class Endpoint(SendResult result) : IRawReliableEndpoint
    {
        public IEndPoint? RemoteEndPoint => null;
        public int MessageMaxByteSize => 1024;
        public bool IsConnected => true;
        public int DisconnectCount { get; private set; }

        public SendResult Send(UnionDataList bufferToSend) => result;

        public bool Disconnect(StopReason reason)
        {
            DisconnectCount++;
            return true;
        }

        public void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null) { }
    }
}
