using System;

namespace Transport.Endpoints
{
    public sealed class GuidEndPoint : TypedEndPoint<Guid>
    {
        public GuidEndPoint(Guid endPoint)
            : base(endPoint)
        {
        }
    }
}