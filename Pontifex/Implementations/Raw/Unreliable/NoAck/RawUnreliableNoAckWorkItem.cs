using Actuarius.Memory;
using Pontifex.Utils;

namespace Pontifex.Raw.Unreliable.NoAck
{
    internal enum RawUnreliableNoAckWorkKind
    {
        StartClientEndpoint,
        DeliverClient,
        ProcessServer,
        TeardownEndpoint,
        TeardownAll,
        Stop
    }

    /// <summary>
    /// Allocation-free unit of serialized work for the RawUnreliableNoAck
    /// transports. A kind selects the operation; the payload fields carry the
    /// arguments. Posting a work item never allocates: the payload is a struct
    /// holding references and the <see cref="UnionDataList"/> message by value.
    /// </summary>
    internal readonly struct RawUnreliableNoAckWorkItem
    {
        public readonly RawUnreliableNoAckWorkKind Kind;
        public readonly RawUnreliableNoAckEndpoint? Endpoint;
        public readonly IEndPoint? Source;
        public readonly UnionDataList? Message;
        public readonly StopReason? Reason;

        private RawUnreliableNoAckWorkItem(RawUnreliableNoAckWorkKind kind, RawUnreliableNoAckEndpoint? endpoint, IEndPoint? source, UnionDataList? message, StopReason? reason)
        {
            Kind = kind;
            Endpoint = endpoint;
            Source = source;
            Message = message;
            Reason = reason;
        }

        public static RawUnreliableNoAckWorkItem StartClientEndpoint(RawUnreliableNoAckEndpoint endpoint) =>
            new(RawUnreliableNoAckWorkKind.StartClientEndpoint, endpoint, null, default, null);

        public static RawUnreliableNoAckWorkItem DeliverClient(UnionDataList message) =>
            new(RawUnreliableNoAckWorkKind.DeliverClient, null, null, message, null);

        public static RawUnreliableNoAckWorkItem ProcessServer(IEndPoint source, UnionDataList message) =>
            new(RawUnreliableNoAckWorkKind.ProcessServer, null, source, message, null);

        public static RawUnreliableNoAckWorkItem TeardownEndpoint(RawUnreliableNoAckEndpoint endpoint, StopReason reason) =>
            new(RawUnreliableNoAckWorkKind.TeardownEndpoint, endpoint, null, default, reason);

        public static RawUnreliableNoAckWorkItem TeardownAll(StopReason reason) =>
            new(RawUnreliableNoAckWorkKind.TeardownAll, null, null, default, reason);

        public static RawUnreliableNoAckWorkItem Stop(StopReason reason) =>
            new(RawUnreliableNoAckWorkKind.Stop, null, null, default, reason);
    }
}
