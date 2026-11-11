namespace Pontifex.Factory;

public readonly record struct SourceSpan(int Start, int Length)
{
    public int End => Start + Length;
}
