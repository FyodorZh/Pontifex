namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness
{
    /// <summary>
    /// NUnit category names used to tag and filter the suite. NUnit categories are flat strings, so
    /// the hierarchy is a naming convention: <see cref="LowLevelTcpSocket"/> is the suite root
    /// (applied to every fixture through <see cref="LowLevelTcpSocketSuiteAttribute"/>) and each
    /// facet nests under it as <c>LowLevelTcpSocket.&lt;facet&gt;</c>, e.g.
    /// <c>--filter "TestCategory=LowLevelTcpSocket.Allocation"</c>. A test carries both the root
    /// (inherited from its fixture) and its facet.
    /// </summary>
    internal static class TestCategories
    {
        /// <summary>Suite root applied to every fixture through <see cref="LowLevelTcpSocketSuiteAttribute"/>.</summary>
        public const string LowLevelTcpSocket = "LowLevelTcpSocket";

        /// <summary>Calibrated allocation budget/baseline probes; Release only (GC counters are unreliable under Debug/JIT noise).</summary>
        public const string Allocation = LowLevelTcpSocket + ".Allocation";

        /// <summary>Longer steady-state throughput/latency/retention probes; Release only.</summary>
        public const string Load = LowLevelTcpSocket + ".Load";
    }
}
