using Pontifex.NoAck.Raw;
using Pontifex.Utils;

namespace Pontifex.NoAck.Raw.Unreliable.Tests;

/// <summary>
/// Factory for one transport implementation's conformance test session.
/// Implement this to run the full NoAckRawUnreliable conformance suite
/// against your transport.
/// </summary>
public interface INoAckRawUnreliableConformanceTestAdapter
{
    /// <summary>Human-readable name for the implementation under test.</summary>
    string ImplementationName { get; }

    /// <summary>Create a new isolated scope that owns transport instances and payload factories.</summary>
    INoAckRawUnreliableConformanceScope CreateScope();
}

/// <summary>
/// Owns one test session: creates isolated client/server pairs and
/// payload factories for a specific transport implementation.
/// </summary>
public interface INoAckRawUnreliableConformanceScope : IDisposable
{
    /// <summary>Create a client transport. When <paramref name="instrumented"/> is <see langword="true"/>,
    /// the transport must expose <see cref="INoAckRawUnreliableConformanceControl"/> via
    /// <see cref="ITransport.GetControls"/>.</summary>
    INoAckRawUnreliableClient CreateClient(bool instrumented);

    /// <summary>Create a server transport. When <paramref name="instrumented"/> is <see langword="true"/>,
    /// the transport must expose <see cref="INoAckRawUnreliableConformanceControl"/> via
    /// <see cref="ITransport.GetControls"/>.</summary>
    INoAckRawUnreliableServer CreateServer(bool instrumented);

    /// <summary>A valid message small enough to always succeed on any transport.</summary>
    UnionDataList CreateSmallValidMessage(ITransport transport);

    /// <summary>A valid message that exactly fills the transport's maximum send size.</summary>
    UnionDataList CreateExactLimitMessage(ITransport transport);

    /// <summary>A message one byte over the transport's maximum send size.</summary>
    UnionDataList CreateOneByteOverLimitMessage(ITransport transport);

    /// <summary>A destination endpoint that belongs to a different server
    /// (used to test <see cref="SendResult.InvalidAddress"/>).</summary>
    IEndPoint CreateForeignServerDestination();

    /// <summary>Additional non-<see cref="SendResult.Ok"/> test cases beyond the built-in ones.</summary>
    IEnumerable<INoAckRawUnreliableAdditionalNonOkCase> CreateAdditionalNonOkCases();
}

/// <summary>
/// An additional test case that asserts a transport returns a specific
/// non-<see cref="SendResult.Ok"/> result for a given send attempt.
/// </summary>
public interface INoAckRawUnreliableAdditionalNonOkCase : IDisposable
{
    /// <summary>Display name for the test case.</summary>
    string Name { get; }

    /// <summary>The <see cref="SendResult"/> that <see cref="Invoke"/> is expected to return.</summary>
    SendResult ExpectedResult { get; }

    /// <summary>The transport to send on.</summary>
    ITransport Transport { get; }

    /// <summary>Execute the send and return the result.</summary>
    SendResult Invoke();
}
