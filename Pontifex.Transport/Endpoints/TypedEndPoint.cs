using System;
using Transport.Abstractions;

namespace Transport.Endpoints
{
    public class TypedEndPoint<TData> : IEndPoint
        where TData : IEquatable<TData>
    {
        private readonly TData _endpoint;

        public TypedEndPoint(TData endPoint)
        {
            _endpoint = endPoint;
        }

        public TData EP => _endpoint;

        public bool Equals(IEndPoint other)
        {
            TypedEndPoint<TData>? endPoint = other as TypedEndPoint<TData>;
            return Equals(endPoint);
        }

        private bool Equals(TypedEndPoint<TData>? other)
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

        public override bool Equals(object? obj)
        {
            return Equals(obj as TypedEndPoint<TData>);
        }

        public override string ToString()
        {
            return _endpoint.ToString();
        }
    }
}