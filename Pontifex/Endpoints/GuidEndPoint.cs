using System;

namespace Pontifex.Endpoints
{
    public sealed class GuidEndPoint : TypedEndPoint<Guid>
    {
        public GuidEndPoint(Guid endPoint)
            : base(endPoint)
        {
        }
    }
}