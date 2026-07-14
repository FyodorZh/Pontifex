using Pontifex.VirtualDelivery.Netem;

namespace Pontifex.Tests.VirtualDelivery.Netem;

[TestFixture]
public class LossModelTests
{
    private static uint Pct(double p) => (uint)(p * uint.MaxValue);

    [TestFixture]
    public class RandomLossModelTests
    {
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

        [Test]
        public void Reset_is_noop()
        {
            var model = new RandomLossModel(Pct(0.5), new CorrelatedRandom(0, 42));
            Assert.DoesNotThrow(() => model.Reset());
        }
    }

    [TestFixture]
    public class FourStateLossModelTests
    {
        [Test]
        public void All_zero_transitions_never_drops()
        {
            var p = new FourStateParams(0, 0, 0, 0, 0);
            var model = new FourStateLossModel(p);

            for (int i = 0; i < 500; i++)
                Assert.That(model.ShouldDrop(), Is.False);
        }

        [Test]
        public void All_max_transitions_produce_alternating_pattern()
        {
            var p = new FourStateParams(
                uint.MaxValue, uint.MaxValue, uint.MaxValue,
                uint.MaxValue, uint.MaxValue);
            var model = new FourStateLossModel(p);

            Assert.That(model.ShouldDrop(), Is.True);
            Assert.That(model.ShouldDrop(), Is.False);
            Assert.That(model.ShouldDrop(), Is.True);
            Assert.That(model.ShouldDrop(), Is.False);
            Assert.That(model.ShouldDrop(), Is.True);
            Assert.That(model.ShouldDrop(), Is.False);
        }

        [Test]
        public void Reset_returns_to_initial_state_no_drop()
        {
            var p = new FourStateParams(uint.MaxValue, 0, 0, uint.MaxValue, 0);
            var model = new FourStateLossModel(p);

            model.ShouldDrop();
            model.ShouldDrop();
            model.Reset();

            Assert.That(model.ShouldDrop(), Is.True);
            Assert.That(model.ShouldDrop(), Is.False);
        }

        [Test]
        public void Only_P14_set__drops_when_transitioning_to_LostBurst()
        {
            var p = new FourStateParams(0, 0, 0, uint.MaxValue, 0);
            var model = new FourStateLossModel(p);

            Assert.That(model.ShouldDrop(), Is.True);
            Assert.That(model.ShouldDrop(), Is.False);
            Assert.That(model.ShouldDrop(), Is.True);
            Assert.That(model.ShouldDrop(), Is.False);
        }

        [Test]
        public void Only_P13_set__drops_isolated_in_gap()
        {
            var p = new FourStateParams(uint.MaxValue, 0, 0, 0, 0);
            var model = new FourStateLossModel(p);

            Assert.That(model.ShouldDrop(), Is.True);
            Assert.That(model.ShouldDrop(), Is.True);
            Assert.That(model.ShouldDrop(), Is.True);
        }

        [Test]
        public void Only_P23_set__drops_when_leaving_burst()
        {
            var p = new FourStateParams(
                uint.MaxValue,  // p13: TxGap → LostGap (to enter the chain)
                0,              // p31
                uint.MaxValue,  // p32: LostGap → TxBurst
                uint.MaxValue,  // p14: TxGap → LostBurst (competes with p13)
                uint.MaxValue); // p23: TxBurst → LostGap (drop)
            var model = new FourStateLossModel(p);

            Assert.That(model.ShouldDrop(), Is.True);
            Assert.That(model.ShouldDrop(), Is.False);
            Assert.That(model.ShouldDrop(), Is.True);
            Assert.That(model.ShouldDrop(), Is.False);
        }
    }

    [TestFixture]
    public class GilbertElliotLossModelTests
    {
        [Test]
        public void All_zero_transitions_never_drops()
        {
            var p = new GilbertElliotParams(0, 0, uint.MaxValue, 0);
            var model = new GilbertElliotLossModel(p);

            for (int i = 0; i < 500; i++)
                Assert.That(model.ShouldDrop(), Is.False);
        }

        [Test]
        public void Max_P_and_K1__always_drops_and_oscillates()
        {
            var p = new GilbertElliotParams(uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue);
            var model = new GilbertElliotLossModel(p);

            Assert.That(model.ShouldDrop(), Is.True);
            Assert.That(model.ShouldDrop(), Is.False);
            Assert.That(model.ShouldDrop(), Is.True);
            Assert.That(model.ShouldDrop(), Is.False);
        }

        [Test]
        public void Only_K1_set__drops_in_good_state_and_stays()
        {
            var p = new GilbertElliotParams(0, 0, uint.MaxValue, uint.MaxValue);
            var model = new GilbertElliotLossModel(p);

            Assert.That(model.ShouldDrop(), Is.True);
            Assert.That(model.ShouldDrop(), Is.True);
            Assert.That(model.ShouldDrop(), Is.True);
        }

        [Test]
        public void Only_P_set__moves_to_bad_then_never_recovers()
        {
            var p = new GilbertElliotParams(uint.MaxValue, 0, 0, 0);
            var model = new GilbertElliotLossModel(p);

            model.ShouldDrop();
            model.ShouldDrop();

            Assert.That(model.ShouldDrop(), Is.True);
            Assert.That(model.ShouldDrop(), Is.True);
        }

        [Test]
        public void Reset_returns_to_good_state()
        {
            var p = new GilbertElliotParams(uint.MaxValue, 0, 0, 0);
            var model = new GilbertElliotLossModel(p);

            model.ShouldDrop();
            model.ShouldDrop();

            model.Reset();

            Assert.That(model.ShouldDrop(), Is.False);
        }

        [Test]
        public void Max_H_in_bad_state__never_drops_in_bad()
        {
            var p = new GilbertElliotParams(uint.MaxValue, 0, uint.MaxValue, uint.MaxValue);
            var model = new GilbertElliotLossModel(p);

            Assert.That(model.ShouldDrop(), Is.True);
            Assert.That(model.ShouldDrop(), Is.False);
            Assert.That(model.ShouldDrop(), Is.False);
        }
    }
}
