using NUnit.Framework;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness
{
    /// <summary>
    /// Marks a harness self-test fixture. Harness self-tests carry only this category,
    /// so they are excluded from the suite root category and can be run separately with
    /// <c>--filter "TestCategory=LowLevelTcpSocket.Harness"</c>.
    /// </summary>
    internal sealed class LowLevelTcpSocketHarnessSuiteAttribute : CategoryAttribute
    {
        public LowLevelTcpSocketHarnessSuiteAttribute() : base(TestCategories.Harness)
        {
        }
    }
}
