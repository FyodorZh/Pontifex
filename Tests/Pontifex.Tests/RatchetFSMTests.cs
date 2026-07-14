namespace Pontifex.Utils.FSM;

public sealed class RatchetFSMTests
{
    private static RatchetFSM<int> CreateFsm()
    {
        return new RatchetFSM<int>((a, b) => a.CompareTo(b), 0);
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
        var fsm = CreateFsm();
        bool called = false;
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
        var fsm = CreateFsm();
        fsm.SetState(5);
        bool called = false;
        fsm.SetState(3, (_, _) => { called = true; return true; });
        Assert.That(called, Is.False);
    }

    [Test]
    public void SetState_onStateChanged_CalledOnForwardMove()
    {
        var fsm = CreateFsm();
        int changedTo = -1;
        fsm.SetState(1, null, s => changedTo = s);
        Assert.That(changedTo, Is.EqualTo(1));
    }

    [Test]
    public void SetState_onStateChanged_NotCalledOnBackwardMove()
    {
        var fsm = CreateFsm();
        fsm.SetState(5);
        bool called = false;
        fsm.SetState(3, null, _ => called = true);
        Assert.That(called, Is.False);
    }

    [Test]
    public void SetState_onStateChanged_NotCalledWhenVetoed()
    {
        var fsm = CreateFsm();
        int changedTo = -1;
        fsm.SetState(1, (_, _) => false, s => changedTo = s);
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
        Assert.DoesNotThrow(() => fsm.SetState(1, null, null));
        Assert.That(fsm.State, Is.EqualTo(1));
    }
}
