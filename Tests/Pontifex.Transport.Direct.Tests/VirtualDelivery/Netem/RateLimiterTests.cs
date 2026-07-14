using Pontifex.VirtualDelivery.Netem;

namespace Pontifex.Tests.VirtualDelivery.Netem;

[TestFixture]
public class RateLimiterTests
{
    [Test]
    public void IsEnabled_returns_false_when_rate_is_zero()
    {
        var limiter = new RateLimiter(0, 0, 0, 0);
        Assert.That(limiter.IsEnabled, Is.False);
    }

    [Test]
    public void IsEnabled_returns_true_when_rate_is_positive()
    {
        var limiter = new RateLimiter(1000, 0, 0, 0);
        Assert.That(limiter.IsEnabled, Is.True);
    }

    [Test]
    public void PacketTimeNs_scales_linearly_with_packet_size()
    {
        var limiter = new RateLimiter(1000, 0, 0, 0);
        long t1 = limiter.PacketTimeNs(100);
        long t2 = limiter.PacketTimeNs(200);

        Assert.That(t2, Is.EqualTo(t1 * 2));
    }

    [Test]
    public void PacketTimeNs_is_inverse_to_rate()
    {
        var fast = new RateLimiter(2000, 0, 0, 0);
        var slow = new RateLimiter(1000, 0, 0, 0);

        long tFast = fast.PacketTimeNs(100);
        long tSlow = slow.PacketTimeNs(100);

        Assert.That(tFast * 2, Is.EqualTo(tSlow));
    }

    [Test]
    public void Packet_overhead_is_added_to_size()
    {
        var noOverhead = new RateLimiter(1000, 0, 0, 0);
        var withOverhead = new RateLimiter(1000, 20, 0, 0);

        long tBase = noOverhead.PacketTimeNs(100);
        long tOverhead = withOverhead.PacketTimeNs(100);

        Assert.That(tOverhead, Is.GreaterThan(tBase));
    }

    [Test]
    public void Cell_size_rounds_up_packet()
    {
        var limiter = new RateLimiter(1000, 0, 100, 0);
        long t50 = limiter.PacketTimeNs(50);
        long t99 = limiter.PacketTimeNs(99);

        Assert.That(t50, Is.EqualTo(t99));
    }

    [Test]
    public void Cell_overhead_adds_per_cell()
    {
        var noCellOverhead = new RateLimiter(1000, 0, 100, 0);
        var withCellOverhead = new RateLimiter(1000, 0, 100, 10);

        long t1 = noCellOverhead.PacketTimeNs(200);
        long t2 = withCellOverhead.PacketTimeNs(200);

        Assert.That(t2, Is.GreaterThan(t1));
    }

    [Test]
    public void Packet_exactly_filling_cells_calculates_correctly()
    {
        var limiter = new RateLimiter(1_000_000_000, 0, 100, 0);
        long timeNs = limiter.PacketTimeNs(300);

        Assert.That(timeNs, Is.GreaterThan(0));
    }

    [Test]
    public void Rate_one_byte_per_sec_gives_ns_per_byte()
    {
        var limiter = new RateLimiter(1, 0, 0, 0);

        long timeNs = limiter.PacketTimeNs(500);

        Assert.That(timeNs, Is.EqualTo(500_000_000_000));
    }

    [Test]
    public void Empty_packet_has_minimal_time()
    {
        var limiter = new RateLimiter(1_000_000, 0, 0, 0);
        long tEmpty = limiter.PacketTimeNs(0);
        long tOneByte = limiter.PacketTimeNs(1);

        Assert.That(tOneByte, Is.GreaterThan(tEmpty));
    }

    [Test]
    public void Cell_size_zero_does_not_round()
    {
        var limiter = new RateLimiter(1_000_000, 0, 0, 0);
        long t100 = limiter.PacketTimeNs(100);
        long t101 = limiter.PacketTimeNs(101);

        Assert.That(t101, Is.GreaterThan(t100));
    }
}
