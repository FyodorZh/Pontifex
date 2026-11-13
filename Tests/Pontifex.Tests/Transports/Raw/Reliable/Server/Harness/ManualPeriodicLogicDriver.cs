using System;
using System.Threading.Tasks;
using Operarius;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Hand-driven <see cref="ILogicDriver{TLogicDriverCtl}"/> for the server transport's session ticker.
/// The transport creates it through the <c>LogicDriverFactory</c> seam, so tests can invoke ticks and
/// stop the ticker without a background thread or a timer.
/// </summary>
internal sealed class ManualPeriodicLogicDriver : ILogicDriver<IPeriodicLogicDriverCtl>
{
    private readonly Control _control;

    private ILogic<IPeriodicLogicDriverCtl>? _logic;

    public ManualPeriodicLogicDriver()
    {
        _control = new Control(this);
    }

    public int StartCalls { get; private set; }

    public int StopCalls { get; private set; }

    public int FinishCalls { get; private set; }

    public bool IsRunning { get; private set; }

    public event Action<ILogic<IPeriodicLogicDriverCtl>>? LogicStopped;

    public event Action<Exception>? ErrorStream
    {
        add { }
        remove { }
    }

    public LogicStartResult Start(ILogic<IPeriodicLogicDriverCtl> logic)
    {
        StartCalls++;
        _logic = logic;
        _control.Attach(logic);
        IsRunning = true;

        return logic.LogicStarted(_control)
            ? LogicStartResult.Success
            : LogicStartResult.FailedToStart;
    }

    /// <summary>
    /// Invokes one periodic tick on the logic, even after the driver has been stopped. This lets a
    /// test verify that the transport itself ignores ticks after it stops.
    /// </summary>
    public void Tick()
    {
        ((IPeriodicLogic)_logic!).LogicTick(_control);
    }

    public void Stop()
    {
        _control.Stop();
    }

    public Task Finish()
    {
        FinishCalls++;
        return Task.CompletedTask;
    }

    public Task WaitForFinish()
    {
        return Task.CompletedTask;
    }

    private sealed class Control : IPeriodicLogicDriverCtl
    {
        private readonly ManualPeriodicLogicDriver _owner;
        private ILogic<IPeriodicLogicDriverCtl>? _logic;

        public Control(ManualPeriodicLogicDriver owner)
        {
            _owner = owner;
        }

        public DateTime CurrentTime { get; } = new(2000, 1, 1);

        public TimeSpan Period => TimeSpan.FromMilliseconds(100);

        public void Attach(ILogic<IPeriodicLogicDriverCtl> logic)
        {
            _logic = logic;
        }

        public void Stop()
        {
            _owner.StopCalls++;

            if (!_owner.IsRunning)
            {
                return;
            }

            _owner.IsRunning = false;
            _logic?.LogicStopped();
            _owner.LogicStopped?.Invoke(_logic!);
        }
    }
}
