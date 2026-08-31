namespace Pontifex.Utils.FSM;

public sealed class SerializedFSMTests
{
    [Test]
    public void WrapsInnerFsm_Correctly()
    {
        var core = new FSM<int, int>(0, s => s);
        core.AddTransition(0, 1);
        core.AddTransition(1, 2);
        var fsm = new SerializedFSM<int>(core);

        fsm.SetState(1);

        Assert.That(fsm.State, Is.EqualTo(1));
        Assert.That(core.State, Is.EqualTo(1));
    }

    [Test]
    public void InitState_IsForwarded()
    {
        var fsm = new SerializedFSM<int>(new FSM<int, int>(5, s => s));
        Assert.That(fsm.InitState, Is.EqualTo(5));
    }

    [Test]
    public void Reset_IsForwarded()
    {
        var core = new FSM<int, int>(0, s => s);
        core.AddTransition(0, 1);
        var fsm = new SerializedFSM<int>(core);

        fsm.SetState(1);
        fsm.Reset();

        Assert.That(fsm.State, Is.EqualTo(0));
    }

    [Test]
    public void SetState_onStateChanging_IsForwarded()
    {
        var core = new FSM<int, int>(0, s => s);
        core.AddTransition(0, 1);
        var fsm = new SerializedFSM<int>(core);

        fsm.SetState(1, (_, _) => false);

        Assert.That(fsm.State, Is.EqualTo(0));
    }

    [Test]
    public void ConcurrentSetState_IsSerialized()
    {
        var fsm = new SerializedFSM<int>(new RatchetFSM<int>((a, b) => a.CompareTo(b), 0));

        var threads = new List<Thread>();
        for (int i = 0; i < 8; i++)
        {
            int start = i * 100;
            threads.Add(new Thread(() =>
            {
                for (int j = 0; j < 100; j++)
                    fsm.SetState(start + j);
            }));
        }

        threads.ForEach(t => t.Start());
        threads.ForEach(t => t.Join());

        Assert.That(fsm.State, Is.EqualTo(799));
    }
}
