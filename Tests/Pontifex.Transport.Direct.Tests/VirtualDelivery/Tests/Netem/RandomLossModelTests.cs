using Pontifex.VirtualDelivery.Netem;

namespace Pontifex.Tests.VirtualDelivery;

[TestFixture]
[Category("Direct.VirtualDelivery")]
public sealed class RandomLossModelTests
{
    private static uint Pct(double p) => (uint)(p * uint.MaxValue);

    [Test]
    public void Probability_zero_never_drops()
    {
        var model = new RandomLossModel(0, new CorrelatedRandom(0, 42));

        for (int i = 0; i < 1000; i++)
            Assert.That(model.ShouldDrop(), Is.False);
    }

    [Test]
    public void Probability_max_always_drops()
    {
        var model = new RandomLossModel(uint.MaxValue, new CorrelatedRandom(0, 42));

        for (int i = 0; i < 100; i++)
            Assert.That(model.ShouldDrop(), Is.True);
    }

    [Test]
    public void Approximate_loss_rate_matches_configured_probability()
    {
        var model = new RandomLossModel(Pct(0.3), new CorrelatedRandom(0, 42));
        int drops = 0;
        const int samples = 10000;

        for (int i = 0; i < samples; i++)
            if (model.ShouldDrop())
                drops++;

        double rate = (double)drops / samples;
        Assert.That(rate, Is.GreaterThanOrEqualTo(0.25));
        Assert.That(rate, Is.LessThanOrEqualTo(0.35));
    }
}
