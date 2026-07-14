using Pontifex.VirtualDelivery.Netem;

namespace Pontifex.Tests.VirtualDelivery.Netem;

[TestFixture]
public class CorrelatedRandomTests
{
    [Test]
    public void Seeded_constructor_produces_deterministic_sequence()
    {
        var a = new CorrelatedRandom(0, 42);
        var b = new CorrelatedRandom(0, 42);

        for (int i = 0; i < 100; i++)
            Assert.That(a.Next(), Is.EqualTo(b.Next()));
    }

    [Test]
    public void Different_seeds_produce_different_sequences()
    {
        var a = new CorrelatedRandom(0, 42);
        var b = new CorrelatedRandom(0, 99);

        for (int i = 0; i < 10; i++)
        {
            a.Next();
            b.Next();
        }

        Assert.That(a.Next(), Is.Not.EqualTo(b.Next()));
    }

    [Test]
    public void Max_rho_produces_constant_output()
    {
        var crng = new CorrelatedRandom(uint.MaxValue, 42);
        uint first = crng.Next();

        for (int i = 0; i < 50; i++)
            Assert.That(crng.Next(), Is.EqualTo(first));
    }

    [Test]
    public void High_rho_produces_lower_variance_than_low_rho()
    {
        const int iterations = 1000;

        static double Variance(Func<CorrelatedRandom> factory)
        {
            var crng = factory();
            var values = new double[iterations];
            for (int i = 0; i < iterations; i++)
                values[i] = crng.Next();
            double mean = values.Average();
            return values.Average(v => (v - mean) * (v - mean));
        }

        double lowRhoVariance = Variance(() => new CorrelatedRandom(0, 42));
        double highRhoVariance = Variance(() => new CorrelatedRandom(0xC0000000, 42));

        Assert.That(highRhoVariance, Is.LessThan(lowRhoVariance));
    }

    [Test]
    public void Reset_produces_new_sequence()
    {
        var crng = new CorrelatedRandom(uint.MaxValue, 42);
        uint beforeReset = crng.Next();
        crng.Reset(uint.MaxValue);
        uint afterReset = crng.Next();

        Assert.That(afterReset, Is.Not.EqualTo(beforeReset));
    }

    [Test]
    public void Reset_changes_rho()
    {
        var crng = new CorrelatedRandom(uint.MaxValue, 42);
        uint constantValue = crng.Next();
        Assert.That(crng.Next(), Is.EqualTo(constantValue));

        crng.Reset(0);

        Assert.That(crng.Next(), Is.Not.EqualTo(constantValue));
    }

    [Test]
    public void Rho_zero_output_is_statistically_uniform()
    {
        var crng = new CorrelatedRandom(0, 42);
        var buckets = new int[16];
        const int samples = 100000;

        for (int i = 0; i < samples; i++)
        {
            uint val = crng.Next();
            int bucket = (int)(val >> 28);
            buckets[bucket]++;
        }

        int expectedPerBucket = samples / 16;
        foreach (int count in buckets)
        {
            Assert.That(count, Is.GreaterThanOrEqualTo((int)(expectedPerBucket * 0.85)));
            Assert.That(count, Is.LessThanOrEqualTo((int)(expectedPerBucket * 1.15)));
        }
    }

    [Test]
    public void Next_returns_uint_values()
    {
        var crng = new CorrelatedRandom(0x80000000, 42);

        for (int i = 0; i < 1000; i++)
        {
            uint val = crng.Next();
            Assert.That(val, Is.TypeOf<uint>());
        }
    }
}
