using Shared;

namespace NOT_FINISHED
{
    public interface IResponse
    {
        void OnFinished(ByteArraySegment response);
    }

    public interface IRequest
    {
        ByteArraySegment Data { get; }
        bool Response(ByteArraySegment response);
    }

    public interface IRawProtocolEp
    {
        bool Setup(System.Action<ByteArraySegment> onReceived);
        bool Send(ByteArraySegment data);
    }

    public interface IRawProtocolClientEp : IRawProtocolEp
    {
        bool Request(ByteArraySegment data, IResponse response);
    }

    public interface IRawProtocolServerEp : IRawProtocolEp
    {
        bool Setup(System.Action<IRequest> onRequested);
    }
}
