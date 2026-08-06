using Actuarius.Memory;
using Pontifex.Utils;

namespace Pontifex.Raw.Unreliable.Ack
{
    internal enum RawUnreliableAckWorkKind
    {
        StartClientEndpoint,
        DeliverClient,
        ProcessServer,
        TeardownEndpoint,
        TeardownAll,
        Stop
    }

    /// <summary>
    /// Allocation-free unit of serialized work for the RawUnreliableAck
    /// transports. A kind selects the operation; the payload fields carry the
    /// arguments. Posting a work item never allocates: the payload is a struct
    /// holding references and the <see cref="UnionDataList"/> message by value.
    /// </summary>
    internal readonly struct RawUnreliableAckWorkItem
    {
        public readonly RawUnreliableAckWorkKind Kind;
        public readonly RawUnreliableAckEndpoint? Endpoint;
        public readonly IEndPoint? Source;
        public readonly UnionDataList? Message;
        public readonly StopReason? Reason;

        private RawUnreliableAckWorkItem(RawUnreliableAckWorkKind kind, RawUnreliableAckEndpoint? endpoint, IEndPoint? source, UnionDataList? message, StopReason? reason)
        {
            Kind = kind;
            Endpoint = endpoint;
            Source = source;
            Message = message;
            Reason = reason;
        }

        public static RawUnreliableAckWorkItem StartClientEndpoint(RawUnreliableAckEndpoint endpoint) =>
            new(RawUnreliableAckWorkKind.StartClientEndpoint, endpoint, null, default, null);

        public static RawUnreliableAckWorkItem DeliverClient(UnionDataList message) =>
            new(RawUnreliableAckWorkKind.DeliverClient, null, null, message, null);

        public static RawUnreliableAckWorkItem ProcessServer(IEndPoint source, UnionDataList message) =>
            new(RawUnreliableAckWorkKind.ProcessServer, null, source, message, null);

        public static RawUnreliableAckWorkItem TeardownEndpoint(RawUnreliableAckEndpoint endpoint, StopReason reason) =>
            new(RawUnreliableAckWorkKind.TeardownEndpoint, endpoint, null, default, reason);

        public static RawUnreliableAckWorkItem TeardownAll(StopReason reason) =>
            new(RawUnreliableAckWorkKind.TeardownAll, null, null, default, reason);

        public static RawUnreliableAckWorkItem Stop(StopReason reason) =>
            new(RawUnreliableAckWorkKind.Stop, null, null, default, reason);
    }
}
