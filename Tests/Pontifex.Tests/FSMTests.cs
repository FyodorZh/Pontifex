namespace Pontifex.Utils.FSM;

public sealed class FSMTests
{
    private static FSM<int, int> CreateFsm(StateChangedReaction<int>? onStateChanged = null)
    {
        var fsm = new FSM<int, int>(0, s => s, onStateChanged);
        fsm.AddTransition(0, 1);
        fsm.AddTransition(1, 2);
        return fsm;
    }

    [Test]
    public void InitialState()
    {
        var fsm = new FSM<int, int>(5, s => s);
        Assert.That(fsm.InitState, Is.EqualTo(5));
        Assert.That(fsm.State, Is.EqualTo(5));
    }

    [Test]
    public void ValidTransition_ChangesState()
    {
        var fsm = CreateFsm();
        fsm.SetState(1);
        Assert.That(fsm.State, Is.EqualTo(1));
    }

    [Test]
    public void InvalidTransition_IsIgnored()
    {
        var fsm = CreateFsm();
        fsm.SetState(99);
        Assert.That(fsm.State, Is.EqualTo(0));
    }

    [Test]
    public void SetState_ChainsMultipleTransitions()
    {
        var fsm = CreateFsm();
        fsm.SetState(1);
        fsm.SetState(2);
        Assert.That(fsm.State, Is.EqualTo(2));
    }

    [Test]
    public void Reset_ReturnsToInitState()
    {
        var fsm = CreateFsm();
        fsm.SetState(2);
        fsm.Reset();
        Assert.That(fsm.State, Is.EqualTo(0));
    }

    [Test]
    public void InitState_DoesNotChange()
    {
        var fsm = CreateFsm();
        fsm.SetState(1);
        Assert.That(fsm.InitState, Is.EqualTo(0));
    }

    [Test]
    public void SetState_onStateChanging_AllowsTransition()
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
    public void SetState_onStateChanging_VetoesTransition()
    {
        var fsm = CreateFsm();
        fsm.SetState(1, (_, _) => false);
        Assert.That(fsm.State, Is.EqualTo(0));
    }

    [Test]
    public void SetState_onStateChanging_ReceivesCorrectStates()
    {
        int? oldReceived = null;
        int? newReceived = null;
        var fsm = CreateFsm();
        fsm.SetState(1, (old, next) =>
        {
            oldReceived = old;
            newReceived = next;
            return true;
        });
        Assert.That(oldReceived, Is.EqualTo(0));
        Assert.That(newReceived, Is.EqualTo(1));
    }

    [Test]
    public void Constructor_onStateChanged_CalledAfterTransition()
    {
        int? changedTo = null;
        var fsm = CreateFsm((_, s) => changedTo = s);
        fsm.SetState(1);
        Assert.That(changedTo, Is.EqualTo(1));
    }

    [Test]
    public void Constructor_onStateChanged_NotCalled_WhenVetoed()
    {
        bool changedCalled = false;
        var fsm = CreateFsm((_, _) => changedCalled = true);
        fsm.SetState(1, (_, _) => false);
        Assert.That(changedCalled, Is.False);
    }

    [Test]
    public void SetState_InvalidTransition_DoesNotCallCallbacks()
    {
        bool changingCalled = false;
        bool changedCalled = false;
        var fsm = CreateFsm((_, _) => changedCalled = true);
        fsm.SetState(99, (_, _) => { changingCalled = true; return true; });
        Assert.That(changingCalled, Is.False);
        Assert.That(changedCalled, Is.False);
    }

    [Test]
    public void AddTransition_FromUnknownState_ReturnsFalse()
    {
        var fsm = new FSM<int, int>(0, s => s);
        Assert.That(fsm.AddTransition(999, 1), Is.False);
    }

    [Test]
    public void AddTransition_Duplicate_ThrowsInvalidOperationException()
    {
        var fsm = new FSM<int, int>(0, s => s);
        fsm.AddTransition(0, 1);
        Assert.Throws<InvalidOperationException>(() => fsm.AddTransition(0, 1));
    }

    [Test]
    public void AddTransitions_AllSucceed()
    {
        var fsm = new FSM<int, int>(0, s => s);
        fsm.AddTransition(0, 1);
        fsm.AddTransition(1, 2);
        fsm.AddTransition(2, 3);
        bool result = fsm.AddTransitions([1, 2], 4);
        Assert.That(result, Is.True);
        fsm.SetState(1);
        fsm.SetState(4);
        Assert.That(fsm.State, Is.EqualTo(4));
    }

    [Test]
    public void AddTransitions_PartialFailure_ReturnsFalse()
    {
        var fsm = new FSM<int, int>(0, s => s);
        fsm.AddTransition(0, 1);
        fsm.AddTransition(0, 2);
        bool result = fsm.AddTransitions([1, 999], 3);
        Assert.That(result, Is.False);
    }

    [Test]
    public void SelfTransition_WhenAllowed_ChangesToSelf()
    {
        var fsm = new FSM<int, int>(0, s => s);
        fsm.AddTransition(0, 0);
        fsm.SetState(0);
        Assert.That(fsm.State, Is.EqualTo(0));
    }

    [Test]
    public void StateMapper_NonUniqueValue_TreatedAsSharedNode()
    {
        var fsm = new FSM<int, int>(0, _ => 0);
        fsm.AddTransition(0, 1);
        fsm.SetState(1);
        Assert.That(fsm.State, Is.EqualTo(0));
    }

    [Test]
    public void SetState_NullCallbacks_DoesNotThrow()
    {
        var fsm = CreateFsm();
        Assert.DoesNotThrow(() => fsm.SetState(1));
        Assert.That(fsm.State, Is.EqualTo(1));
    }
}
