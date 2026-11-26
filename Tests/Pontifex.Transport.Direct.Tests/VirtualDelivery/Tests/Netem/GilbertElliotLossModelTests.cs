using Pontifex.Tests.VirtualDelivery.Harness;
using Pontifex.VirtualDelivery.Netem;

namespace Pontifex.Tests.VirtualDelivery;

[TestFixture]
[Category("Direct.VirtualDelivery")]
public sealed class GilbertElliotLossModelTests
{
    [Test]
    public void All_zero_transitions_never_drops()
    {
        var p = new GilbertElliotParams(0, 0, uint.MaxValue, 0);
        var random = new ScriptedRandom(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        var model = new GilbertElliotLossModel(p, random.Next);

        for (int i = 0; i < 5; i++)
            Assert.That(model.ShouldDrop(), Is.False);
    }

    [Test]
    public void Max_p_and_k1_always_drops_and_oscillates()
    {
        var p = new GilbertElliotParams(uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue);
        var random = new ScriptedRandom(1, 1, 1, 1, 1, 1, 1, 1);
        var model = new GilbertElliotLossModel(p, random.Next);

        Assert.That(model.ShouldDrop(), Is.True);
        Assert.That(model.ShouldDrop(), Is.False);
        Assert.That(model.ShouldDrop(), Is.True);
        Assert.That(model.ShouldDrop(), Is.False);
    }

    [Test]
    public void Only_k1_set_drops_in_good_state_and_stays()
    {
        var p = new GilbertElliotParams(0, 0, uint.MaxValue, uint.MaxValue);
        var random = new ScriptedRandom(1, 1, 1, 1, 1, 1);
        var model = new GilbertElliotLossModel(p, random.Next);

        Assert.That(model.ShouldDrop(), Is.True);
        Assert.That(model.ShouldDrop(), Is.True);
        Assert.That(model.ShouldDrop(), Is.True);
    }

    [Test]
    public void Only_p_set_moves_to_bad_then_never_recovers()
    {
        var p = new GilbertElliotParams(uint.MaxValue, 0, 0, 0);
        var random = new ScriptedRandom(1, 1, 1, 1, 1, 1);
        var model = new GilbertElliotLossModel(p, random.Next);

        Assert.That(model.ShouldDrop(), Is.False); // Good → Bad, no loss in Good
        Assert.That(model.ShouldDrop(), Is.True);  // Bad, no recovery
        Assert.That(model.ShouldDrop(), Is.True);  // still Bad
    }

    [Test]
    public void Reset_returns_to_good_state()
    {
        var p = new GilbertElliotParams(uint.MaxValue, 0, 0, 0);
        var random = new ScriptedRandom(1, 1, 1, 1, 1, 1);
        var model = new GilbertElliotLossModel(p, random.Next);

        Assert.That(model.ShouldDrop(), Is.False); // Good → Bad
        Assert.That(model.ShouldDrop(), Is.True);  // Bad

        model.Reset();

        Assert.That(model.ShouldDrop(), Is.False); // Good → Bad again
    }

    [Test]
    public void Max_h_in_bad_state_never_drops_in_bad()
    {
        var p = new GilbertElliotParams(uint.MaxValue, 0, uint.MaxValue, uint.MaxValue);
        var random = new ScriptedRandom(1, 1, 1, 1, 1, 1);
        var model = new GilbertElliotLossModel(p, random.Next);

        Assert.That(model.ShouldDrop(), Is.True);  // Good → Bad, K1 loss
        Assert.That(model.ShouldDrop(), Is.False); // Bad, H never exceeded
        Assert.That(model.ShouldDrop(), Is.False); // still Bad
    }

    [Test]
    public void Mixed_transition_probabilities_select_the_expected_branches()
    {
        var p = new GilbertElliotParams(100, 200, 500, 300);
        var random = new ScriptedRandom(
            50, 250,  // Good: P → Bad, K1 loss
            250, 600, // Bad: no recovery, H exceeded
            150, 250, // Bad: R → Good; this packet still uses H
            150, 350);// Good: stays Good, no K1 loss
        var model = new GilbertElliotLossModel(p, random.Next);

        Assert.That(model.ShouldDrop(), Is.True);  // Good, K1 loss
        Assert.That(model.ShouldDrop(), Is.True);  // Bad, H loss
        Assert.That(model.ShouldDrop(), Is.False); // Bad → Good; this packet still uses H
        Assert.That(model.ShouldDrop(), Is.False); // Good, no K1 loss
    }
}
