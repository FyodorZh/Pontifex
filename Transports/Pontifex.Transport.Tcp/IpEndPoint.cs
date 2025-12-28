using System.Net;
using Pontifex.Abstractions;

namespace Pontifex.Transports.NetSockets
{
    internal sealed class IpEndPoint : IEndPoint
    {
        private readonly EndPoint? mEndpoint;

        public IpEndPoint(EndPoint? endPoint)
        {
            mEndpoint = endPoint;
        }

        public EndPoint? EP => mEndpoint;

        public bool Equals(IEndPoint other)
        {
            return Equals(other as IpEndPoint);
        }

        private bool Equals(IpEndPoint? other)
        {
            if (!ReferenceEquals(other, null))
            {
                if (!ReferenceEquals(this, other))
                {
                    return mEndpoint?.Equals(other.mEndpoint) ?? (other.mEndpoint == null);
                }
                return true;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return mEndpoint?.GetHashCode() ?? 0;
        }

        public override string ToString()
        {
            return $"[Ip={mEndpoint?.ToString() ?? "null"}]";
        }
    }
}
