using Actuarius.Memory;
using Pontifex.Utils;

namespace Pontifex.Raw
{
    internal enum RawWorkKind
    {
        StartClient,
        DeliverClient,
        ProcessServer,
        TeardownEndpoint,
        TeardownAll,
        Stop
    }

    /// <summary>
    /// Allocation-free unit of serialized work for the Raw transports. A kind
    /// selects the operation; the payload fields carry the arguments. Posting a
    /// work item never allocates: the payload is a struct holding references and
    /// the <see cref="UnionDataList"/> message by value.
    /// </summary>
    internal readonly struct RawWorkItem
    {
        public readonly RawWorkKind Kind;
        public readonly RawEndpoint? Endpoint;
        public readonly IEndPoint? Source;
        public readonly UnionDataList? Message;
        public readonly StopReason? Reason;

        private RawWorkItem(RawWorkKind kind, RawEndpoint? endpoint, IEndPoint? source, UnionDataList? message, StopReason? reason)
        {
            Kind = kind;
            Endpoint = endpoint;
            Source = source;
            Message = message;
            Reason = reason;
        }

        public static RawWorkItem StartClient() =>
            new(RawWorkKind.StartClient, null, null, default, null);

        public static RawWorkItem DeliverClient(UnionDataList message) =>
            new(RawWorkKind.DeliverClient, null, null, message, null);

        public static RawWorkItem ProcessServer(IEndPoint source, UnionDataList message) =>
            new(RawWorkKind.ProcessServer, null, source, message, null);

        public static RawWorkItem TeardownEndpoint(RawEndpoint endpoint, StopReason reason) =>
            new(RawWorkKind.TeardownEndpoint, endpoint, null, default, reason);

        public static RawWorkItem TeardownAll(StopReason reason) =>
            new(RawWorkKind.TeardownAll, null, null, default, reason);

        public static RawWorkItem Stop(StopReason reason) =>
            new(RawWorkKind.Stop, null, null, default, reason);
    }
}
