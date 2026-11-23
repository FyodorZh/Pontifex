using System;
using Operarius;

namespace Pontifex.Tests.RR.RRUnreliableDirect.Harness;

/// <summary>
/// Test-only clock for the request and response TTLs.
/// </summary>
internal sealed class ManualClock : IDateTimeProvider
{
    public DateTime Now { get; set; }
}
