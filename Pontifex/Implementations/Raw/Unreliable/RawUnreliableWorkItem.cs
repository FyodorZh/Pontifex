using Actuarius.Memory;
using Pontifex.Utils;

namespace Pontifex.Raw.Unreliable
{
    internal enum RawUnreliableWorkKind
    {
        StartClientEndpoint,
        DeliverClient,
        ProcessServer,
        TeardownEndpoint,
        TeardownAll,
        Stop
    }

    /// <summary>
    /// Allocation-free unit of serialized work for the RawUnreliable
    /// transports. A kind selects the operation; the payload fields carry the
    /// arguments. Posting a work item never allocates: the payload is a struct
    /// holding references and the <see cref="UnionDataList"/> message by value.
    /// </summary>
    internal readonly struct RawUnreliableWorkItem
    {
        public readonly RawUnreliableWorkKind Kind;
        public readonly RawUnreliableEndpoint? Endpoint;
        public readonly IEndPoint? Source;
        public readonly UnionDataList? Message;
        public readonly StopReason? Reason;

        private RawUnreliableWorkItem(RawUnreliableWorkKind kind, RawUnreliableEndpoint? endpoint, IEndPoint? source, UnionDataList? message, StopReason? reason)
        {
            Kind = kind;
            Endpoint = endpoint;
            Source = source;
            Message = message;
            Reason = reason;
        }

        public static RawUnreliableWorkItem StartClientEndpoint(RawUnreliableEndpoint endpoint) =>
            new(RawUnreliableWorkKind.StartClientEndpoint, endpoint, null, default, null);

        public static RawUnreliableWorkItem DeliverClient(UnionDataList message) =>
            new(RawUnreliableWorkKind.DeliverClient, null, null, message, null);

        public static RawUnreliableWorkItem ProcessServer(IEndPoint source, UnionDataList message) =>
            new(RawUnreliableWorkKind.ProcessServer, null, source, message, null);

        public static RawUnreliableWorkItem TeardownEndpoint(RawUnreliableEndpoint endpoint, StopReason reason) =>
            new(RawUnreliableWorkKind.TeardownEndpoint, endpoint, null, default, reason);

        public static RawUnreliableWorkItem TeardownAll(StopReason reason) =>
            new(RawUnreliableWorkKind.TeardownAll, null, null, default, reason);

        public static RawUnreliableWorkItem Stop(StopReason reason) =>
            new(RawUnreliableWorkKind.Stop, null, null, default, reason);
    }
}
