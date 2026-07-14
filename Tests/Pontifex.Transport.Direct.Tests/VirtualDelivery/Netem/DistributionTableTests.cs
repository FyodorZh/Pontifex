using Pontifex.VirtualDelivery.Netem;

namespace Pontifex.Tests.VirtualDelivery.Netem;

[TestFixture]
public class DistributionTableTests
{
    [Test]
    public void Sigma_zero_returns_mu_regardless_of_table()
    {
        var table = new DistributionTable(new short[] { 100, 200, 300 });
        var crng = new CorrelatedRandom(0, 42);

        long result = table.Sample(5000, 0, crng);

        Assert.That(result, Is.EqualTo(5000));
    }

    [Test]
    public void Uniform_fallback__jitter_without_table()
    {
        var crng = new CorrelatedRandom(0, 42);
        long mu = 1000;
        long sigma = 100;

        long result = tableSample(null, mu, sigma, crng);

        long min = mu - sigma;
        long max = mu + sigma;
        Assert.That(result, Is.GreaterThanOrEqualTo(min));
        Assert.That(result, Is.LessThanOrEqualTo(max));
    }

    [Test]
    public void Table_sampling_respects_mu_and_sigma_bounds()
    {
        var table = new DistributionTable(new short[] { 0, 1, -1, 2, -2 });
        var crng = new CorrelatedRandom(0, 42);

        for (int i = 0; i < 100; i++)
        {
            long result = table.Sample(0, 1000, crng);
            Assert.That(result, Is.GreaterThanOrEqualTo(-3000));
            Assert.That(result, Is.LessThanOrEqualTo(3000));
        }
    }

    [Test]
    public void Known_max_correlation_makes_output_constant()
    {
        var table = new DistributionTable(new short[] { 5, 10, -3, 7 });
        var crng = new CorrelatedRandom(uint.MaxValue, 42);

        long first = table.Sample(100, 50, crng);
        for (int i = 0; i < 20; i++)
            Assert.That(table.Sample(100, 50, crng), Is.EqualTo(first));
    }

    [Test]
    public void Table_with_zero_entries_returns_mu()
    {
        var table = new DistributionTable(new short[] { 0, 0, 0 });
        var crng = new CorrelatedRandom(0, 42);

        for (int i = 0; i < 50; i++)
            Assert.That(table.Sample(1000, 100, crng), Is.EqualTo(1000));
    }

    [Test]
    public void Large_table_does_not_throw()
    {
        var data = new short[10000];
        for (int i = 0; i < data.Length; i++)
            data[i] = (short)(i % 100 - 50);

        var table = new DistributionTable(data);
        var crng = new CorrelatedRandom(0, 42);

        Assert.DoesNotThrow(() => table.Sample(0, 1000, crng));
    }

    private static long tableSample(DistributionTable? table, long mu, long sigma, CorrelatedRandom crng)
    {
        if (table != null)
            return table.Sample(mu, sigma, crng);

        if (sigma == 0)
            return mu;

        uint rnd = crng.Next();
        long jitterRange = 2 * sigma;
        return ((long)(rnd % (ulong)jitterRange) + mu) - sigma;
    }
}
