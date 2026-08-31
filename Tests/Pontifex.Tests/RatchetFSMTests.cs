namespace Pontifex.Utils.FSM;

public sealed class RatchetFSMTests
{
    private static RatchetFSM<int> CreateFsm(StateChangedReaction<int>? onStateChanged = null)
    {
        return new RatchetFSM<int>((a, b) => a.CompareTo(b), 0, onStateChanged);
    }

    [Test]
    public void InitialState()
    {
        var fsm = new RatchetFSM<int>((a, b) => a.CompareTo(b), 5);
        Assert.That(fsm.InitState, Is.EqualTo(5));
        Assert.That(fsm.State, Is.EqualTo(5));
    }

    [Test]
    public void ForwardMove_ChangesState()
    {
        var fsm = CreateFsm();
        fsm.SetState(1);
        Assert.That(fsm.State, Is.EqualTo(1));
    }

    [Test]
    public void BackwardMove_IsIgnored()
    {
        var fsm = CreateFsm();
        fsm.SetState(5);
        fsm.SetState(3);
        Assert.That(fsm.State, Is.EqualTo(5));
    }

    [Test]
    public void SameState_IsIgnored()
    {
        var fsm = CreateFsm();
        fsm.SetState(1);
        fsm.SetState(1);
        Assert.That(fsm.State, Is.EqualTo(1));
    }

    [Test]
    public void Reset_ReturnsToInitState()
    {
        var fsm = CreateFsm();
        fsm.SetState(10);
        fsm.Reset();
        Assert.That(fsm.State, Is.EqualTo(0));
    }

    [Test]
    public void ChainForwardIncrements()
    {
        var fsm = CreateFsm();
        fsm.SetState(1);
        fsm.SetState(2);
        fsm.SetState(3);
        Assert.That(fsm.State, Is.EqualTo(3));
    }

    [Test]
    public void BackwardThenForward_BackwardIgnoredForwardWorks()
    {
        var fsm = CreateFsm();
        fsm.SetState(5);
        fsm.SetState(3);
        fsm.SetState(10);
        fsm.SetState(7);
        Assert.That(fsm.State, Is.EqualTo(10));
    }

    [Test]
    public void SetState_onStateChanging_AllowsForwardMove()
    {
        bool called = false;
        var fsm = CreateFsm();
        fsm.SetState(1, (old, next) =>
        {
            called = true;
            return true;
        });
        Assert.That(called, Is.True);
        Assert.That(fsm.State, Is.EqualTo(1));
    }

    [Test]
    public void SetState_onStateChanging_VetoesForwardMove()
    {
        var fsm = CreateFsm();
        fsm.SetState(1, (_, _) => false);
        Assert.That(fsm.State, Is.EqualTo(0));
    }

    [Test]
    public void SetState_onStateChanging_NotCalledOnBackwardMove()
    {
        bool called = false;
        var fsm = CreateFsm();
        fsm.SetState(5);
        fsm.SetState(3, (_, _) => { called = true; return true; });
        Assert.That(called, Is.False);
    }

    [Test]
    public void Constructor_onStateChanged_CalledOnForwardMove()
    {
        int changedTo = -1;
        var fsm = CreateFsm((_, s) => changedTo = s);
        fsm.SetState(1);
        Assert.That(changedTo, Is.EqualTo(1));
    }

    [Test]
    public void Constructor_onStateChanged_NotCalledOnBackwardMove()
    {
        bool called = false;
        var fsm = CreateFsm((_, _) => called = true);
        fsm.SetState(5);
        called = false;
        fsm.SetState(3);
        Assert.That(called, Is.False);
    }

    [Test]
    public void Constructor_onStateChanged_NotCalledWhenVetoed()
    {
        int changedTo = -1;
        var fsm = CreateFsm((_, s) => changedTo = s);
        fsm.SetState(1, (_, _) => false);
        Assert.That(changedTo, Is.EqualTo(-1));
    }

    [Test]
    public void SetState_NegativeComparator_ReversesDirection()
    {
        var fsm = new RatchetFSM<int>((a, b) => b.CompareTo(a), 10);
        fsm.SetState(5);
        Assert.That(fsm.State, Is.EqualTo(5));
        fsm.SetState(8);
        Assert.That(fsm.State, Is.EqualTo(5));
        fsm.SetState(1);
        Assert.That(fsm.State, Is.EqualTo(1));
    }

    [Test]
    public void SetState_NullCallbacks_DoesNotThrow()
    {
        var fsm = CreateFsm();
        Assert.DoesNotThrow(() => fsm.SetState(1));
        Assert.That(fsm.State, Is.EqualTo(1));
    }
}
