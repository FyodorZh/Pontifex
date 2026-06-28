using System.Net;
using Pontifex.Abstractions;

namespace Pontifex.Transports.NetSockets
{
    public sealed class IpEndPoint : IEndPoint
    {
        private readonly EndPoint _endpoint;

        public IpEndPoint(EndPoint endPoint)
        {
            _endpoint = endPoint;
        }

        public EndPoint EP => _endpoint;

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
                    return _endpoint.Equals(other._endpoint);
                }
                return true;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return _endpoint.GetHashCode();
        }

        public override string ToString()
        {
            return $"[Ip={_endpoint}]";
        }
    }
}
