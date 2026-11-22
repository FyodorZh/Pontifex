using System;
using System.Collections.Generic;

namespace Pontifex.Tests.VirtualDelivery.Harness;

/// <summary>
/// Test-only uniform 32-bit source that returns a fixed script and fails when the script is exhausted.
/// </summary>
internal sealed class ScriptedRandom
{
    private readonly Queue<uint> _values;

    public ScriptedRandom(params uint[] values)
    {
        _values = new Queue<uint>(values);
    }

    public uint Next()
    {
        if (_values.Count == 0)
        {
            throw new InvalidOperationException("The scripted random sequence is exhausted.");
        }

        return _values.Dequeue();
    }
}
