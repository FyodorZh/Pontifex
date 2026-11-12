using NUnit.Framework;
using Pontifex.TransportFormula;

namespace Pontifex.TransportFormula.Tests.Registry.Harness;

internal static class RegistryAssertions
{
    public static void ThrowsRegistration(
        TestDelegate code,
        string expectedCode,
        string expectedMessage)
    {
        var exception = Assert.Throws<RegistrationException>(code);

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.Error.Code, Is.EqualTo(expectedCode));
        Assert.That(exception.Error.Message, Is.EqualTo(expectedMessage));
        Assert.That(exception.Error.Phase, Is.EqualTo(FactoryPhase.Registration));
        Assert.That(exception.Error.Span, Is.Null);
    }
}
