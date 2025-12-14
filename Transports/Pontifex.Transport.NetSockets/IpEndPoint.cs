using System.Net;
using Transport.Abstractions;

namespace Transport.Endpoints
{
    internal sealed class IpEndPoint : IEndPoint
    {
        private readonly EndPoint mEndpoint;

        public IpEndPoint(EndPoint endPoint)
        {
            mEndpoint = endPoint;
        }

        public EndPoint EP
        {
            get { return mEndpoint; }
        }

        public bool Equals(IEndPoint other)
        {
            IpEndPoint endPoint = other as IpEndPoint;
            return Equals(endPoint);
        }

        private bool Equals(IpEndPoint other)
        {
            if (!ReferenceEquals(other, null))
            {
                if (!ReferenceEquals(this, other))
                {
                    return mEndpoint.Equals(other.mEndpoint);
                }
                return true;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return (mEndpoint != null ? mEndpoint.GetHashCode() : 0);
        }

        public override string ToString()
        {
            return string.Format("[Ip={0}]", mEndpoint != null ? mEndpoint.ToString() : "null");
        }
    }
}
