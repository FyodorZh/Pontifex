using NUnit.Framework;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness
{
    /// <summary>
    /// Marks a fixture as part of the LowLevelTcpSocket suite, so other test suites in the same
    /// project can include or exclude it with <c>--filter "TestCategory=LowLevelTcpSocket"</c>.
    /// </summary>
    internal sealed class LowLevelTcpSocketSuiteAttribute : CategoryAttribute
    {
        public LowLevelTcpSocketSuiteAttribute() : base(TestCategories.LowLevelTcpSocket)
        {
        }
    }
}
