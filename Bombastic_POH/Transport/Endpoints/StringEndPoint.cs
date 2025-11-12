namespace Transport.Endpoints
{
    public sealed class StringEndPoint : TypedEndPoint<string>
    {
        public StringEndPoint(string endPoint)
            : base(endPoint)
        {
        }
    }
}