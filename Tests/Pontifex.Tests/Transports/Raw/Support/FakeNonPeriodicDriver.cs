using Operarius;

namespace Pontifex.Tests.Transports.Raw.Support;

/// <summary>
/// Deterministic single-logic <see cref="INonPeriodicLogicDriverCtl"/> test double.
/// Mirrors the Operarius non-periodic driver contract that RawEndpoint relies on:
/// <list type="bullet">
/// <item><see cref="Start"/> invokes <c>LogicStarted</c>; a false return (or a throw)
/// is treated as a failed start and <c>LogicStopped</c> is invoked, exactly like the
/// real driver.</item>
/// <item><see cref="RequestInvocation"/> records a pending invocation (coalesced) and
/// grants no tick by itself in manual mode; the test grants ticks deterministically via
/// <see cref="DrainPending"/> / <see cref="TickOnce"/>.</item>
/// <item><see cref="Stop"/> only records the request; the test simulates the driver
/// actually stopping the logic with <see cref="SimulateLogicStopped"/>.</item>
/// </list>
/// </summary>
public sealed class FakeNonPeriodicDriver : INonPeriodicLogicDriverCtl
{
    private readonly bool _autoDrain;
    private INonPeriodicLogic? _logic;
    private int _running;
    private int _pending;
    private int _requestCount;
    private int _tickCount;
    private int _stopRequestCount;

    public FakeNonPeriodicDriver(bool autoDrain = false)
    {
        _autoDrain = autoDrain;
    }

    public DateTime CurrentTime { get; set; } = DateTime.UtcNow;

    public INonPeriodicLogic? Logic => _logic;

    public bool IsRunning => Volatile.Read(ref _running) == 1;

    public bool HasPendingInvocation => Volatile.Read(ref _pending) != 0;

    public int RequestCount => Volatile.Read(ref _requestCount);

    public int TickCount => Volatile.Read(ref _tickCount);

    public int StopRequestCount => Volatile.Read(ref _stopRequestCount);

    public bool Start(INonPeriodicLogic logic)
    {
        ArgumentNullException.ThrowIfNull(logic);

        if (Interlocked.Exchange(ref _running, 1) != 0)
        {
            return false;
        }

        _logic = logic;
        try
        {
            if (!logic.LogicStarted(this))
            {
                logic.LogicStopped();
                Interlocked.Exchange(ref _running, 0);
                _logic = null;
                return false;
            }

            return true;
        }
        catch
        {
            Interlocked.Exchange(ref _running, 0);
            try
            {
                logic.LogicStopped();
            }
            catch
            {
                // The driver does not let a failing LogicStopped mask the original failure.
            }

            _logic = null;
            throw;
        }
    }

    public void RequestInvocation()
    {
        if (!IsRunning)
        {
            return;
        }

        Interlocked.Increment(ref _requestCount);
        Volatile.Write(ref _pending, 1);
        if (_autoDrain)
        {
            DrainPending();
        }
    }

    /// <summary>Consumes one pending invocation grant, if any, and runs one LogicTick.</summary>
    public bool DrainPending()
    {
        if (!IsRunning)
        {
            return false;
        }

        if (Interlocked.Exchange(ref _pending, 0) == 0)
        {
            return false;
        }

        return TickOnce();
    }

    /// <summary>Runs one LogicTick regardless of pending grants (used for the graceful-stop flush).</summary>
    public bool TickOnce()
    {
        if (!IsRunning)
        {
            return false;
        }

        var logic = _logic;
        if (logic == null)
        {
            return false;
        }

        Interlocked.Increment(ref _tickCount);
        logic.LogicTick(this);
        return true;
    }

    public void Stop()
    {
        Interlocked.Increment(ref _stopRequestCount);
    }

    /// <summary>Simulates the driver terminating the logic (calls ILogic.LogicStopped once).</summary>
    public void SimulateLogicStopped()
    {
        var logic = _logic;
        if (logic == null)
        {
            return;
        }

        Interlocked.Exchange(ref _running, 0);
        logic.LogicStopped();
    }
}
