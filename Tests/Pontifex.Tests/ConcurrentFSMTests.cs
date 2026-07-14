using System.Diagnostics;

namespace Pontifex.Utils.FSM;

public sealed class ConcurrentFSMTests
{
    private static (FSM<int, int> core, ConcurrentFSM<int> fsm) CreatePair()
    {
        var core = new FSM<int, int>(0, s => s);
        core.AddTransition(0, 1);
        core.AddTransition(1, 2);
        core.AddTransition(2, 1);
        var fsm = new ConcurrentFSM<int>(core);
        return (core, fsm);
    }

    private static void SpinUntil(Func<bool> predicate, int timeoutMs = 2000)
    {
        var sw = Stopwatch.StartNew();
        while (!predicate() && sw.ElapsedMilliseconds < timeoutMs)
            Thread.Yield();
        Assert.That(predicate(), Is.True, $"Timed out after {timeoutMs}ms");
    }

    [Test]
    public void InitialState()
    {
        var (_, fsm) = CreatePair();
        Assert.That(fsm.InitState, Is.EqualTo(0));
        Assert.That(fsm.State, Is.EqualTo(0));
    }

    [Test]
    public void SetState_ValidTransition_EventuallyChangesState()
    {
        var (_, fsm) = CreatePair();
        fsm.SetState(1, null, null);
        SpinUntil(() => fsm.State == 1);
        Assert.That(fsm.State, Is.EqualTo(1));
    }

    [Test]
    public void SetState_MultipleCalls_ProcessedInOrder()
    {
        var (_, fsm) = CreatePair();
        fsm.SetState(1, null, null);
        fsm.SetState(2, null, null);
        SpinUntil(() => fsm.State == 2);
        Assert.That(fsm.State, Is.EqualTo(2));
    }

    [Test]
    public void Reset_EventuallyReturnsToInitState()
    {
        var (_, fsm) = CreatePair();
        fsm.SetState(1, null, null);
        SpinUntil(() => fsm.State == 1);

        fsm.Reset();
        SpinUntil(() => fsm.State == 0);
        Assert.That(fsm.State, Is.EqualTo(0));
    }

    [Test]
    public void InvalidTransition_IsIgnored()
    {
        var (_, fsm) = CreatePair();
        fsm.SetState(99, null, null);
        SpinUntil(() => fsm.State == 0, 500);
        Assert.That(fsm.State, Is.EqualTo(0));
    }

    [Test]
    public void SetState_Calls_onStateChanging()
    {
        var (_, fsm) = CreatePair();
        bool called = false;
        fsm.SetState(1, (_, _) => { called = true; return true; }, null);
        SpinUntil(() => fsm.State == 1);
        Assert.That(called, Is.True);
    }

    [Test]
    public void SetState_Calls_onStateChanged()
    {
        var (_, fsm) = CreatePair();
        int changedTo = -1;
        fsm.SetState(1, null, s => changedTo = s);
        SpinUntil(() => fsm.State == 1);
        Assert.That(changedTo, Is.EqualTo(1));
    }

    [Test]
    public void SetState_Veto_QueuedCorrectly()
    {
        var (_, fsm) = CreatePair();
        fsm.SetState(1, (_, _) => false, null);
        SpinUntil(() => fsm.State == 0, 500);
        Assert.That(fsm.State, Is.EqualTo(0));
    }

    [Test]
    public void Release_DoesNotThrow()
    {
        var (_, fsm) = CreatePair();
        Assert.DoesNotThrow(() => fsm.Release());
    }

    [Test]
    public void Release_StopsProcessing()
    {
        var (_, fsm) = CreatePair();
        fsm.Release();
        fsm.SetState(1, null, null);
        Thread.Sleep(50);
        Assert.That(fsm.State, Is.EqualTo(0));
    }

    [Test]
    public void WrapsInnerFsm_Correctly()
    {
        var (core, fsm) = CreatePair();
        fsm.SetState(1, null, null);
        SpinUntil(() => fsm.State == 1);
        Assert.That(core.State, Is.EqualTo(1));
    }
}
