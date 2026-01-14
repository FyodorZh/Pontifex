namespace Pontifex.Api.Protocol
{
    public interface IRequest<out TRequest, in TResponse>
    {
        TRequest Data { get; }
        SendResult Response(TResponse response);
        SendResult Fail(string errorMessage);
    }
}