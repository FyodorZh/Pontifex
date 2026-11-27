using System.Collections.Concurrent;

namespace Pontifex.Tests;

/// <summary>
/// Regression tests for the shared recording logger used by public-stack acceptance tests.
/// </summary>
[TestFixture]
[Category("TransportTestSupport")]
public sealed class TransportRegistryTests
{
    /// <summary>
    /// Recorded error logs fail through an assertion on the test thread, not the logger consumer.
    /// </summary>
    [Test]
    public void AssertNoErrors_AfterErrorLog_ThrowsOnTheTestThread()
    {
        var logger = TransportRegistry.GetLogger(failIfError: true, writeToConsole: false);
        logger.e("recorded error");

        var assertion = Assert.Throws<AssertionException>(() => logger.AssertNoErrors());

        Assert.That(assertion.Message, Does.Contain("recorded error"));
    }

    /// <summary>
    /// Warning logs are recorded but are not promoted to test errors.
    /// </summary>
    [Test]
    public void AssertNoErrors_AfterWarningLog_DoesNotThrow()
    {
        var logger = TransportRegistry.GetLogger(failIfError: true, writeToConsole: false);
        logger.w("recorded warning");

        Assert.DoesNotThrow(() => logger.AssertNoErrors());
    }

    /// <summary>
    /// Error logs can be added to the test's error bag for assertion after teardown.
    /// </summary>
    [Test]
    public void AddErrorsTo_AfterErrorLog_AddsTheRecordedMessage()
    {
        var logger = TransportRegistry.GetLogger(failIfError: true, writeToConsole: false);
        var errors = new ConcurrentBag<string>();
        logger.e("recorded error");

        logger.AddErrorsTo(errors, "logger-test");

        Assert.That(errors, Has.Some.Contains("recorded error"));
    }
}
