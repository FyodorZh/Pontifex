using NUnit.Framework;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness
{
    /// <summary>
    /// Marks a non-harness fixture as part of the LowLevelTcpSocket suite, so other test suites in
    /// the same project can include or exclude it with
    /// <c>--filter "TestCategory=LowLevelTcpSocket"</c>. Harness self-tests use
    /// <see cref="LowLevelTcpSocketHarnessSuiteAttribute"/> instead.
    /// </summary>
    internal sealed class LowLevelTcpSocketSuiteAttribute : CategoryAttribute
    {
        public LowLevelTcpSocketSuiteAttribute() : base(TestCategories.LowLevelTcpSocket)
        {
        }
    }
}
