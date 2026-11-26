using Pontifex.Tests.VirtualDelivery.Harness;
using Pontifex.VirtualDelivery.Netem;

namespace Pontifex.Tests.VirtualDelivery;

[TestFixture]
[Category("Direct.VirtualDelivery")]
public sealed class FourStateLossModelTests
{
    [Test]
    public void All_zero_transitions_never_drops()
    {
        var p = new FourStateParams(0, 0, 0, 0, 0);
        var random = new ScriptedRandom(0, 1, 2, 3, 4);
        var model = new FourStateLossModel(p, random.Next);

        for (int i = 0; i < 5; i++)
            Assert.That(model.ShouldDrop(), Is.False);
    }

    [Test]
    public void All_max_transitions_produce_alternating_pattern()
    {
        var p = new FourStateParams(
            uint.MaxValue, uint.MaxValue, uint.MaxValue,
            uint.MaxValue, uint.MaxValue);
        var random = new ScriptedRandom(1, 1, 1, 1, 1, 1);
        var model = new FourStateLossModel(p, random.Next);

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
        var random = new ScriptedRandom(1, 1, 1, 1);
        var model = new FourStateLossModel(p, random.Next);

        Assert.That(model.ShouldDrop(), Is.True);
        Assert.That(model.ShouldDrop(), Is.False);

        model.Reset();

        Assert.That(model.ShouldDrop(), Is.True);
        Assert.That(model.ShouldDrop(), Is.False);
    }

    [Test]
    public void Only_p14_set_drops_when_transitioning_to_lost_burst()
    {
        var p = new FourStateParams(0, 0, 0, uint.MaxValue, 0);
        var random = new ScriptedRandom(1, 1, 1, 1);
        var model = new FourStateLossModel(p, random.Next);

        Assert.That(model.ShouldDrop(), Is.True);
        Assert.That(model.ShouldDrop(), Is.False);
        Assert.That(model.ShouldDrop(), Is.True);
        Assert.That(model.ShouldDrop(), Is.False);
    }

    [Test]
    public void Only_p13_set_drops_isolated_in_gap()
    {
        var p = new FourStateParams(uint.MaxValue, 0, 0, 0, 0);
        var random = new ScriptedRandom(1, 1, 1);
        var model = new FourStateLossModel(p, random.Next);

        Assert.That(model.ShouldDrop(), Is.True);
        Assert.That(model.ShouldDrop(), Is.True);
        Assert.That(model.ShouldDrop(), Is.True);
    }

    [Test]
    public void Only_p23_set_drops_when_leaving_burst()
    {
        var p = new FourStateParams(
            uint.MaxValue,  // p13: TxGap → LostGap
            0,              // p31
            uint.MaxValue,  // p32: LostGap → TxBurst
            0,              // p14: never enter LostBurst directly
            uint.MaxValue); // p23: TxBurst → LostGap
        var random = new ScriptedRandom(1, 1, 1, 1, 1);
        var model = new FourStateLossModel(p, random.Next);

        Assert.That(model.ShouldDrop(), Is.True);   // TxGap → LostGap
        Assert.That(model.ShouldDrop(), Is.False);  // LostGap → TxBurst
        Assert.That(model.ShouldDrop(), Is.True);   // TxBurst → LostGap
        Assert.That(model.ShouldDrop(), Is.False);  // LostGap → TxBurst
        Assert.That(model.ShouldDrop(), Is.True);   // TxBurst → LostGap
    }

    [Test]
    public void Mixed_transition_probabilities_select_the_expected_branches()
    {
        var p = new FourStateParams(200, 200, 300, 100, 400);
        var random = new ScriptedRandom(
            350, // TxGap: outside both ranges → stays TxGap
            50,  // TxGap: rnd < p14 → LostBurst
            0,   // LostBurst: always returns to TxGap
            150, // TxGap: p14 < rnd < p14 + p13 → LostGap
            50,  // LostGap: rnd < p32 → TxBurst
            50,  // TxBurst: rnd < p23 → LostGap
            400, // LostGap: p32 < rnd < p32 + p31 → TxGap (recovery)
            150, // TxGap: → LostGap
            50,  // LostGap: → TxBurst
            450, // TxBurst: rnd >= p23 → stays TxBurst
            350);// TxBurst: rnd < p23 → LostGap
        var model = new FourStateLossModel(p, random.Next);

        Assert.That(model.ShouldDrop(), Is.False);  // TxGap, no loss
        Assert.That(model.ShouldDrop(), Is.True);   // LostBurst
        Assert.That(model.ShouldDrop(), Is.False);  // TxGap
        Assert.That(model.ShouldDrop(), Is.True);   // LostGap
        Assert.That(model.ShouldDrop(), Is.False);  // TxBurst
        Assert.That(model.ShouldDrop(), Is.True);   // LostGap
        Assert.That(model.ShouldDrop(), Is.False);  // LostGap → TxGap recovery
        Assert.That(model.ShouldDrop(), Is.True);   // LostGap
        Assert.That(model.ShouldDrop(), Is.False);  // TxBurst
        Assert.That(model.ShouldDrop(), Is.False);  // TxBurst stays
        Assert.That(model.ShouldDrop(), Is.True);   // LostGap
    }
}
