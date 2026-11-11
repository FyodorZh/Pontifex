using System;

namespace Pontifex.Factory
{
    public readonly struct SourceSpan : IEquatable<SourceSpan>
    {
        public SourceSpan(int start, int length)
        {
            Start = start;
            Length = length;
        }

        public int Start { get; }

        public int Length { get; }

        public int End => Start + Length;

        public bool Equals(SourceSpan other)
        {
            return Start == other.Start && Length == other.Length;
        }

        public override bool Equals(object? obj)
        {
            return obj is SourceSpan other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Start, Length);
        }

        public static bool operator ==(SourceSpan left, SourceSpan right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SourceSpan left, SourceSpan right)
        {
            return !left.Equals(right);
        }

        public void Deconstruct(out int start, out int length)
        {
            start = Start;
            length = Length;
        }

        public override string ToString()
        {
            return $"SourceSpan {{ Start = {Start}, Length = {Length} }}";
        }
    }
}
