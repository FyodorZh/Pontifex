namespace Pontifex.Api.Protocol
{
    public interface IRequest<out TRequest, in TResponse>
    {
        TRequest Data { get; }
        void Response(TResponse response);
        void Fail(string errorMessage);
    }
}