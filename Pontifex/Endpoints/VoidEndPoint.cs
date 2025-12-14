using Pontifex.Abstractions;

namespace Pontifex.Endpoints
{
    public sealed class VoidEndPoint : IEndPoint
    {
        public static readonly IEndPoint Instance = new VoidEndPoint();

        private VoidEndPoint()
        {
        }

        public bool Equals(IEndPoint other)
        {
            return ReferenceEquals(this, other);
        }

        public override bool Equals(object? other)
        {
            return ReferenceEquals(this, other);
        }

        public override int GetHashCode()
        {
            return 0;
        }

        public override string ToString()
        {
            return "VoidEndPoint";
        }
    }
}
