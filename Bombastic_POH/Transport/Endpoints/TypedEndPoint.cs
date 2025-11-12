using System;
using Transport.Abstractions;

namespace Transport.Endpoints
{
    public class TypedEndPoint<TData> : IEndPoint
        where TData : IEquatable<TData>
    {
        private readonly TData mEndpoint;

        public TypedEndPoint(TData endPoint)
        {
            mEndpoint = endPoint;
        }

        public TData EP
        {
            get { return mEndpoint; }
        }

        public bool Equals(IEndPoint other)
        {
            TypedEndPoint<TData> endPoint = other as TypedEndPoint<TData>;
            return Equals(endPoint);
        }

        private bool Equals(TypedEndPoint<TData> other)
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

        public override bool Equals(object obj)
        {
            return Equals(obj as TypedEndPoint<TData>);
        }

        public override string ToString()
        {
            return mEndpoint.ToString();
        }
    }
}