namespace Transport.Endpoints
{
    public sealed class LongEndPoint : TypedEndPoint<long>
    {
        public LongEndPoint(long endPoint)
            : base(endPoint)
        {
        }
    }
}