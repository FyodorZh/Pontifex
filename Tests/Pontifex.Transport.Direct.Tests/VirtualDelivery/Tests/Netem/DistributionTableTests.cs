using Pontifex.VirtualDelivery.Netem;

namespace Pontifex.Tests.VirtualDelivery;

[TestFixture]
[Category("Direct.VirtualDelivery")]
public sealed class DistributionTableTests
{
    private const long Scale = 8192;

    [Test]
    public void Sigma_zero_returns_mu_regardless_of_table()
    {
        var table = new DistributionTable(new short[] { 100, 200, 300 });
        var crng = new CorrelatedRandom(0, 42);

        long result = table.Sample(5000, 0, crng);

        Assert.That(result, Is.EqualTo(5000));
    }

    [Test]
    public void Table_sampling_uses_the_table_entries_and_stays_in_bounds()
    {
        var table = new DistributionTable(new short[] { -4096, -2048, 0, 2048, 4096 });
        var crng = new CorrelatedRandom(0, 42);
        const long sigma = Scale * 4;

        var values = new HashSet<long>();
        for (int i = 0; i < 100; i++)
        {
            long result = table.Sample(0, sigma, crng);
            values.Add(result);
            Assert.That(result, Is.GreaterThanOrEqualTo(-sigma));
            Assert.That(result, Is.LessThanOrEqualTo(sigma));
        }

        Assert.That(values.Count, Is.GreaterThan(1),
            "The table entries must influence the sample.");
    }

    [Test]
    public void Known_max_correlation_makes_output_constant()
    {
        var table = new DistributionTable(new short[] { 2048, -2048, 4096, -4096 });
        var crng = new CorrelatedRandom(uint.MaxValue, 42);
        const long mu = 100;
        const long sigma = Scale * 4;

        long first = table.Sample(mu, sigma, crng);
        for (int i = 0; i < 20; i++)
            Assert.That(table.Sample(mu, sigma, crng), Is.EqualTo(first));

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.Not.EqualTo(mu),
                "The table entry must offset the constant sample.");
            Assert.That(first, Is.GreaterThanOrEqualTo(mu - sigma));
            Assert.That(first, Is.LessThanOrEqualTo(mu + sigma));
        });
    }

    [Test]
    public void Table_of_zeros_returns_mu()
    {
        var table = new DistributionTable(new short[] { 0, 0, 0 });
        var crng = new CorrelatedRandom(0, 42);

        for (int i = 0; i < 50; i++)
            Assert.That(table.Sample(1000, Scale * 4, crng), Is.EqualTo(1000));
    }

    [Test]
    public void Large_table_stays_in_bounds()
    {
        var data = new short[10000];
        for (int i = 0; i < data.Length; i++)
            data[i] = (short)(i % 100 - 50);

        var table = new DistributionTable(data);
        var crng = new CorrelatedRandom(0, 42);
        const long sigma = Scale * 4;

        long result = table.Sample(0, sigma, crng);

        Assert.That(result, Is.InRange(-sigma, sigma));
    }

    [Test]
    public void Constructor_with_null_table_throws()
    {
        Assert.Throws<ArgumentNullException>(() => new DistributionTable(null!));
    }

    [Test]
    public void Constructor_with_empty_table_throws()
    {
        Assert.Throws<ArgumentException>(() => new DistributionTable(Array.Empty<short>()));
    }
}
