using System;
using Operarius;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Test-only clock for the request and response TTLs.
/// </summary>
internal sealed class ManualClock : IDateTimeProvider
{
    public DateTime Now { get; set; }
}
