using NUnit.Framework;
using Shared.Utils;

namespace TransportAnalyzer.UTests
{
    [TestFixture]
    class PeriodicLogicManualDriverTest
    {
        private class VoidLogic : IPeriodicLogic
        {
            private readonly bool mIsOK;

            public VoidLogic(bool isOk)
            {
                mIsOK = isOk;
            }

            public bool LogicStarted(ILogicDriverCtl driver)
            {
                return mIsOK;
            }

            public void LogicTick()
            {
            }

            public void LogicStopped()
            {
            }
        }

        private void TestCase(bool brokenLogic, int ticks)
        {
            VoidLogic voidLogic = new VoidLogic(brokenLogic);
            bool hasError = false;
            IPeriodicLogic logic = voidLogic.Test(
                (text) =>
                    Assert.Fail($"brokenLogic={brokenLogic}, lazyInit={false}, ticks={ticks}")
                );

            PeriodicLogicManualDriver driver = new PeriodicLogicManualDriver(Shared.DeltaTime.Zero);
            driver.Start(logic, Log.VoidLogger);
            for (int i = 0; i < ticks; ++i)
            {
                driver.Tick();
            }
            driver.Stop();
            Assert.IsFalse(hasError);
        }

        [Test]
        public void Test()
        {
            for (int ticks = 0; ticks < 10; ++ticks)
            {
                TestCase(false, ticks);
                TestCase(true, ticks);
            }
        }
    }
}
