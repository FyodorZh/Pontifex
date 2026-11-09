using System;
using Actuarius.Memory;
using Pontifex.Raw.Reliable;
using Pontifex.StopReasons;
using Pontifex.Tests.Endpoints;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Black-box contract tests for <see cref="RawReliableClientTransport{TClientHandler}"/>, exercised
/// through the extended public + protected surface exposed by <see cref="TestRawReliableClientTransport"/>.
/// Covers initialization, the connect/finish state machine, connectivity, endpoint ownership, the
/// terminal notification matrix, and handler exception isolation.
/// </summary>
[Category("Transports")]
public sealed class RawReliableClientTransportTests
{
    private static IMemoryRental Memory => MemoryRental.Shared;

    private static TestRawReliableClientTransport Create(string name = "TestRawReliableClientTransport")
    {
        return new TestRawReliableClientTransport(name);
    }

    private static RecordingRawReliableClientHandler CreateHandler(
        TestRawReliableClientTransport? transport = null)
    {
        return new RecordingRawReliableClientHandler(transport?.EventLog);
    }

    private static UserIntention Reason(string source = "test")
    {
        return new UserIntention(source);
    }

    private static void InitAndConnect(
        TestRawReliableClientTransport transport,
        RecordingRawReliableClientHandler handler,
        Action<StopReason> onStopped)
    {
        Assert.That(transport.Init(handler), Is.True);
        Assert.That(transport.Start(onStopped), Is.True);
        transport.ExposedConnectionFinished(_ => { });
        Assert.That(transport.IsConnected, Is.True);
    }

    #region Init

    [Test]
    public void Init_WhenConstructed_StoresHandlerAndInitializes()
    {
        var transport = Create();
        using var handler = CreateHandler(transport);

        Assert.That(transport.Init(handler), Is.True);

        Assert.That(transport.Handler, Is.SameAs(handler));
        Assert.That(transport.SetupHandlerCalls, Is.EqualTo(1));
        Assert.That(transport.LastSetupHandlerArgument, Is.SameAs(handler));
        Assert.That(transport.IsValid, Is.True);
        Assert.That(transport.IsStarted, Is.False);
        Assert.That(transport.IsConnected, Is.False);
        Assert.That(transport.DestroyTransportCalls, Is.Zero);
    }

    [Test]
    public void Init_WithNullHandler_FailsAndInvalidates()
    {
        var transport = Create();

        Assert.That(transport.Init(null!), Is.False);

        Assert.That(transport.IsValid, Is.False);
        Assert.That(transport.Handler, Is.Null);
        Assert.That(transport.SetupHandlerCalls, Is.EqualTo(1));
        Assert.That(transport.LastSetupHandlerArgument, Is.Null);
        Assert.That(transport.DestroyTransportCalls, Is.Zero);
    }

    [Test]
    public void Init_WhenSetupHandlerReturnsNull_FailsAndInvalidates()
    {
        var transport = Create();
        using var handler = CreateHandler(transport);
        transport.SetupHandlerBehavior = static _ => null;

        Assert.That(transport.Init(handler), Is.False);

        Assert.That(transport.IsValid, Is.False);
        Assert.That(transport.Handler, Is.Null);
        Assert.That(transport.SetupHandlerCalls, Is.EqualTo(1));
        Assert.That(transport.LastSetupHandlerArgument, Is.SameAs(handler));
        Assert.That(handler.StoppedCount, Is.Zero);
        Assert.That(transport.DestroyTransportCalls, Is.Zero);
    }

    [Test]
    public void Init_WhenSetupHandlerTransforms_StoresTransformedHandler()
    {
        var transport = Create();
        using var original = CreateHandler(transport);
        using var transformed = CreateHandler(transport);
        transport.SetupHandlerBehavior = _ => transformed;

        Assert.That(transport.Init(original), Is.True);

        Assert.That(transport.Handler, Is.SameAs(transformed));
        Assert.That(transport.SetupHandlerCalls, Is.EqualTo(1));
        Assert.That(transport.LastSetupHandlerArgument, Is.SameAs(original));
    }

    [Test]
    public void Init_CalledTwice_FailsAndKeepsFirstHandler()
    {
        var transport = Create();
        using var first = CreateHandler(transport);
        using var second = CreateHandler(transport);

        Assert.That(transport.Init(first), Is.True);
        Assert.That(transport.Init(second), Is.False);

        Assert.That(transport.Handler, Is.SameAs(first));
        Assert.That(transport.SetupHandlerCalls, Is.EqualTo(2));
        Assert.That(transport.LastSetupHandlerArgument, Is.SameAs(second));
        Assert.That(transport.IsValid, Is.False);
        Assert.That(transport.DestroyTransportCalls, Is.Zero);
        Assert.That(first.StoppedCount, Is.Zero);
        Assert.That(second.StoppedCount, Is.Zero);
    }

    [Test]
    public void Init_AfterStartWithoutInit_FailsWithoutCallingSetupHandler()
    {
        var transport = Create();
        using var handler = CreateHandler(transport);
        Assert.That(transport.Start(_ => { }), Is.False);

        Assert.That(transport.Init(handler), Is.False);

        Assert.That(transport.SetupHandlerCalls, Is.Zero);
        Assert.That(transport.Handler, Is.Null);
        Assert.That(transport.BeginConnectCalls, Is.Zero);
        Assert.That(handler.StoppedCount, Is.Zero);
    }

    [Test]
    public void Init_WhenConnecting_FailsAndStopsWithHandlerNotification()
    {
        var transport = Create();
        using var handler = CreateHandler(transport);
        Assert.That(transport.Init(handler), Is.True);
        StopReason? callbackReason = null;
        Assert.That(transport.Start(r =>
        {
            callbackReason = r;
            transport.EventLog.Add("startCallback");
        }), Is.True);

        Assert.That(transport.Init(handler), Is.False);

        Assert.That(transport.IsValid, Is.False);
        Assert.That(transport.Handler, Is.SameAs(handler));
        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.DisconnectedCount, Is.Zero);
        Assert.That(handler.StoppedReason, Is.TypeOf<TextFail>());
        var reason = (TextFail)handler.StoppedReason!;
        Assert.That(reason.Source, Is.EqualTo(transport.Name));
        Assert.That(reason.Text, Does.Contain("Wrong transport state"));
        Assert.That(callbackReason, Is.SameAs(handler.StoppedReason));
        Assert.That(transport.DestroyTransportCalls, Is.EqualTo(1));
        Assert.That(transport.ExposedAttachedEndpoint, Is.Null);
        Assert.That(transport.EventLog, Is.EqualTo(new[]
        {
            "beginConnect", "readyToConnect", "handlerStopped", "destroyTransport", "startCallback"
        }));
    }

    [Test]
    public void Init_WhenConnected_FailsAndNotifiesDisconnectThenStop()
    {
        var transport = Create();
        using var handler = CreateHandler(transport);
        StopReason? callbackReason = null;
        InitAndConnect(transport, handler, r =>
        {
            callbackReason = r;
            transport.EventLog.Add("startCallback");
        });

        Assert.That(transport.Init(handler), Is.False);

        Assert.That(transport.IsValid, Is.False);
        Assert.That(handler.DisconnectedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        var reason = (TextFail)handler.StoppedReason!;
        Assert.That(reason.Source, Is.EqualTo(transport.Name));
        Assert.That(reason.Text, Does.Contain("Wrong transport state"));
        Assert.That(callbackReason, Is.SameAs(handler.StoppedReason));
        Assert.That(callbackReason, Is.SameAs(handler.DisconnectedReason));
        Assert.That(transport.DestroyTransportCalls, Is.EqualTo(1));
        Assert.That(transport.EventLog, Is.EqualTo(new[]
        {
            "beginConnect", "readyToConnect", "handlerDisconnected", "handlerStopped", "destroyTransport",
            "startCallback"
        }));
    }

    [Test]
    public void Init_AfterCompletedStop_FailsWithoutAdditionalNotification()
    {
        var transport = Create();
        using var first = CreateHandler(transport);
        using var second = CreateHandler(transport);
        Assert.That(transport.Init(first), Is.True);
        Assert.That(transport.Start(_ => { }), Is.True);
        var reason = Reason();
        Assert.That(transport.Stop(reason), Is.True);
        Assert.That(transport.DestroyTransportCalls, Is.EqualTo(1));
        Assert.That(first.StoppedCount, Is.EqualTo(1));

        Assert.That(transport.Init(second), Is.False);

        Assert.That(transport.IsValid, Is.False);
        Assert.That(transport.SetupHandlerCalls, Is.EqualTo(2));
        Assert.That(transport.LastSetupHandlerArgument, Is.SameAs(second));
        Assert.That(transport.Handler, Is.SameAs(first));
        Assert.That(transport.DestroyTransportCalls, Is.EqualTo(1));
        Assert.That(first.StoppedCount, Is.EqualTo(1));
        Assert.That(second.StoppedCount, Is.Zero);
    }

    #endregion

    #region Start

    [Test]
    public void Start_WithoutInit_FailsWithoutTransportHooksOrHandlerNotifications()
    {
        var transport = Create();
        using var handler = CreateHandler(transport);
        bool callbackCalled = false;
        StopReason? callbackReason = null;

        Assert.That(transport.Start(r =>
        {
            callbackCalled = true;
            callbackReason = r;
        }), Is.False);

        Assert.That(transport.BeginConnectCalls, Is.Zero);
        Assert.That(transport.OnReadyToConnectCalls, Is.Zero);
        Assert.That(transport.DestroyTransportCalls, Is.Zero);
        Assert.That(transport.IsValid, Is.False);
        Assert.That(handler.StoppedCount, Is.Zero);
        Assert.That(handler.DisconnectedCount, Is.Zero);
        Assert.That(transport.EventLog, Is.Empty);
        // The start attempt itself fails while the base transport is started, so its own stop
        // callback still observes the failure; no handler or transport hook is involved.
        Assert.That(callbackCalled, Is.True);
        Assert.That(callbackReason, Is.TypeOf<TextFail>());
    }

    [Test]
    public void Start_WhenInitialized_BeginsConnectThenReady()
    {
        var transport = Create();
        using var handler = CreateHandler(transport);
        Assert.That(transport.Init(handler), Is.True);
        bool callbackCalled = false;

        Assert.That(transport.Start(_ => callbackCalled = true), Is.True);

        Assert.That(transport.BeginConnectCalls, Is.EqualTo(1));
        Assert.That(transport.OnReadyToConnectCalls, Is.EqualTo(1));
        Assert.That(transport.DestroyTransportCalls, Is.Zero);
        Assert.That(transport.IsValid, Is.True);
        Assert.That(transport.IsConnected, Is.False);
        Assert.That(handler.StoppedCount, Is.Zero);
        Assert.That(callbackCalled, Is.False);
        Assert.That(transport.EventLog, Is.EqualTo(new[] { "beginConnect", "readyToConnect" }));
    }

    [Test]
    public void Start_WhenBeginConnectReturnsFalse_DestroysAndNotifiesStopped()
    {
        var transport = Create();
        using var handler = CreateHandler(transport);
        Assert.That(transport.Init(handler), Is.True);
        transport.BeginConnectBehavior = static () => false;
        StopReason? callbackReason = null;

        Assert.That(transport.Start(r =>
        {
            callbackReason = r;
            transport.EventLog.Add("startCallback");
        }), Is.False);

        Assert.That(transport.BeginConnectCalls, Is.EqualTo(1));
        Assert.That(transport.OnReadyToConnectCalls, Is.Zero);
        Assert.That(transport.DestroyTransportCalls, Is.EqualTo(1));
        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.DisconnectedCount, Is.Zero);
        Assert.That(handler.StoppedReason, Is.TypeOf<TextFail>());
        Assert.That(callbackReason, Is.SameAs(handler.StoppedReason));
        Assert.That(transport.IsValid, Is.False);
        Assert.That(transport.IsConnected, Is.False);
        Assert.That(transport.EventLog, Is.EqualTo(new[]
        {
            "beginConnect", "destroyTransport", "handlerStopped", "startCallback"
        }));
    }

    [Test]
    public void Start_WhenBeginConnectReturnsFalseAndOnStoppedThrows_StillDestroys()
    {
        var transport = Create();
        using var handler = CreateHandler(transport);
        var exception = new InvalidOperationException("stopped failure");
        handler.StoppedException = exception;
        Assert.That(transport.Init(handler), Is.True);
        transport.BeginConnectBehavior = static () => false;
        StopReason? callbackReason = null;

        Assert.That(transport.Start(r =>
        {
            callbackReason = r;
            transport.EventLog.Add("startCallback");
        }), Is.False);

        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.ThrownStoppedException, Is.SameAs(exception));
        Assert.That(callbackReason, Is.SameAs(handler.StoppedReason));
        Assert.That(transport.DestroyTransportCalls, Is.EqualTo(1));
        Assert.That(transport.LastDestroyReason, Is.SameAs(handler.StoppedReason));
        Assert.That(transport.ExposedAttachedEndpoint, Is.Null);
        Assert.That(transport.IsValid, Is.False);
        Assert.That(transport.EventLog, Is.EqualTo(new[]
        {
            "beginConnect", "destroyTransport", "handlerStopped", "startCallback"
        }));
    }

    [Test]
    public void Start_WhenBeginConnectReturnsFalseAndDestroyThrows_StillDetaches()
    {
        var transport = Create();
        using var handler = CreateHandler(transport);
        using var endpointHandler = CreateHandler();
        using var endpoint = new TestRawReliableClientEndpoint(endpointHandler);
        Assert.That(transport.Init(handler), Is.True);
        transport.ExposedAttachEndpoint(endpoint);
        transport.BeginConnectBehavior = static () => false;
        transport.DestroyException = new InvalidOperationException("destroy failure");
        bool callbackCalled = false;
        bool started = true;

        Assert.DoesNotThrow(() => started = transport.Start(_ => callbackCalled = true));

        Assert.That(started, Is.False);
        Assert.That(callbackCalled, Is.True);
        Assert.That(transport.DestroyExceptionThrown, Is.True);
        Assert.That(transport.DestroyTransportCalls, Is.EqualTo(1));
        Assert.That(transport.ExposedAttachedEndpoint, Is.Null);
        Assert.That(transport.IsValid, Is.False);
    }

    [Test]
    public void Start_WhenBeginConnectStopsTransport_ReturnsFalseWithoutOnReady()
    {
        var transport = Create();
        using var handler = CreateHandler(transport);
        Assert.That(transport.Init(handler), Is.True);
        var reason = Reason();
        transport.BeginConnectBehavior = () =>
        {
            transport.Stop(reason);
            return true;
        };
        StopReason? callbackReason = null;

        Assert.That(transport.Start(r =>
        {
            callbackReason = r;
            transport.EventLog.Add("startCallback");
        }), Is.False);

        Assert.That(transport.BeginConnectCalls, Is.EqualTo(1));
        Assert.That(transport.OnReadyToConnectCalls, Is.Zero);
        Assert.That(transport.DestroyTransportCalls, Is.EqualTo(1));
        Assert.That(transport.LastDestroyReason, Is.SameAs(reason));
        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.SameAs(reason));
        Assert.That(callbackReason, Is.SameAs(reason));
        Assert.That(transport.IsValid, Is.True);
        Assert.That(transport.IsConnected, Is.False);
        Assert.That(transport.EventLog, Is.EqualTo(new[]
        {
            "beginConnect", "destroyTransport", "handlerStopped", "startCallback"
        }));
    }

    #endregion

    #region Connection completion and failure

    [Test]
    public void ConnectionFinished_WhenConnecting_InvokesActionWithHandlerAndConnects()
    {
        var transport = Create();
        using var handler = CreateHandler(transport);
        Assert.That(transport.Init(handler), Is.True);
        Assert.That(transport.Start(_ => { }), Is.True);
        IRawReliableClientHandler? actionHandler = null;
        bool connectedInsideAction = true;

        transport.ExposedConnectionFinished(h =>
        {
            actionHandler = h;
            connectedInsideAction = transport.IsConnected;
            transport.EventLog.Add("connectionFinished");
        });

        Assert.That(actionHandler, Is.SameAs(handler));
        Assert.That(connectedInsideAction, Is.False);
        Assert.That(transport.IsConnected, Is.True);
        Assert.That(transport.DestroyTransportCalls, Is.Zero);
        Assert.That(handler.DisconnectedCount, Is.Zero);
        Assert.That(handler.StoppedCount, Is.Zero);
        Assert.That(transport.EventLog, Is.EqualTo(new[]
        {
            "beginConnect", "readyToConnect", "connectionFinished"
        }));
    }

    [Test]
    public void ConnectionFinished_WhenAlreadyConnected_StopsWithTextFailWithoutInvokingAction()
    {
        var transport = Create();
        using var handler = CreateHandler(transport);
        StopReason? callbackReason = null;
        InitAndConnect(transport, handler, r =>
        {
            callbackReason = r;
            transport.EventLog.Add("startCallback");
        });
        bool actionCalled = false;

        transport.ExposedConnectionFinished(_ => actionCalled = true);

        Assert.That(actionCalled, Is.False);
        Assert.That(handler.DisconnectedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.TypeOf<TextFail>());
        var reason = (TextFail)handler.StoppedReason!;
        Assert.That(reason.Source, Is.EqualTo(transport.Name));
        Assert.That(reason.Text, Does.Contain("Connected"));
        Assert.That(reason.Text, Does.Contain("Connecting"));
        Assert.That(callbackReason, Is.SameAs(handler.StoppedReason));
        Assert.That(transport.DestroyTransportCalls, Is.EqualTo(1));
        Assert.That(transport.IsValid, Is.True);
        Assert.That(transport.IsConnected, Is.False);
        Assert.That(transport.EventLog, Is.EqualTo(new[]
        {
            "beginConnect", "readyToConnect", "handlerDisconnected", "handlerStopped", "destroyTransport",
            "startCallback"
        }));
    }

    [Test]
    public void ConnectionFinished_WhenInitialized_IsIgnoredAndStartStillWorks()
    {
        var transport = Create();
        using var handler = CreateHandler(transport);
        Assert.That(transport.Init(handler), Is.True);
        bool actionCalled = false;

        transport.ExposedConnectionFinished(_ => actionCalled = true);

        Assert.That(actionCalled, Is.False);
        Assert.That(transport.DestroyTransportCalls, Is.Zero);
        Assert.That(handler.StoppedCount, Is.Zero);
        Assert.That(transport.IsValid, Is.True);
        Assert.That(transport.IsConnected, Is.False);

        Assert.That(transport.Start(_ => { }), Is.True);
        Assert.That(transport.BeginConnectCalls, Is.EqualTo(1));
        Assert.That(transport.OnReadyToConnectCalls, Is.EqualTo(1));
        Assert.That(transport.IsConnected, Is.False);
    }

    [Test]
    public void ConnectionFinished_WhenActionStopsTransport_DoesNotResurrectConnected()
    {
        var transport = Create();
        using var handler = CreateHandler(transport);
        Assert.That(transport.Init(handler), Is.True);
        var reason = Reason();
        StopReason? callbackReason = null;
        Assert.That(transport.Start(r =>
        {
            callbackReason = r;
            transport.EventLog.Add("startCallback");
        }), Is.True);

        transport.ExposedConnectionFinished(_ =>
        {
            transport.EventLog.Add("connectionFinished");
            transport.Stop(reason);
        });

        Assert.That(transport.IsConnected, Is.False);
        Assert.That(transport.IsStarted, Is.False);
        Assert.That(transport.IsValid, Is.True);
        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.SameAs(reason));
        Assert.That(callbackReason, Is.SameAs(reason));
        Assert.That(transport.DestroyTransportCalls, Is.EqualTo(1));
        Assert.That(transport.LastDestroyReason, Is.SameAs(reason));
        Assert.That(transport.EventLog, Is.EqualTo(new[]
        {
            "beginConnect", "readyToConnect", "connectionFinished", "handlerStopped", "destroyTransport",
            "startCallback"
        }));
    }

    [Test]
    public void ConnectionFinished_WhenActionFailsTransport_DoesNotResurrectConnected()
    {
        var transport = Create();
        using var handler = CreateHandler(transport);
        Assert.That(transport.Init(handler), Is.True);
        var failReason = new TextFail(transport.Name, "action failure");
        StopReason? callbackReason = null;
        Assert.That(transport.Start(r =>
        {
            callbackReason = r;
            transport.EventLog.Add("startCallback");
        }), Is.True);

        transport.ExposedConnectionFinished(_ =>
        {
            transport.EventLog.Add("connectionFinished");
            transport.Fail(failReason);
        });

        Assert.That(transport.IsConnected, Is.False);
        Assert.That(transport.IsStarted, Is.False);
        Assert.That(transport.IsValid, Is.False);
        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.SameAs(failReason));
        Assert.That(callbackReason, Is.SameAs(failReason));
        Assert.That(transport.DestroyTransportCalls, Is.EqualTo(1));
        Assert.That(transport.LastDestroyReason, Is.SameAs(failReason));
        Assert.That(transport.EventLog, Is.EqualTo(new[]
        {
            "beginConnect", "readyToConnect", "connectionFinished", "handlerStopped", "destroyTransport",
            "startCallback"
        }));
    }

    [Test]
    public void ConnectionFinished_WhenActionThrows_FailsTransportWithoutConnecting()
    {
        var transport = Create();
        using var handler = CreateHandler(transport);
        Assert.That(transport.Init(handler), Is.True);
        StopReason? callbackReason = null;
        Assert.That(transport.Start(r =>
        {
            callbackReason = r;
            transport.EventLog.Add("startCallback");
        }), Is.True);
        var exception = new InvalidOperationException("constructor failure");

        Assert.DoesNotThrow(() =>
            transport.ExposedConnectionFinished(_ =>
            {
                transport.EventLog.Add("connectionFinished");
                throw exception;
            }));

        Assert.That(transport.IsConnected, Is.False);
        Assert.That(transport.IsStarted, Is.False);
        Assert.That(transport.IsValid, Is.False);
        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.TypeOf<ExceptionFail>());
        Assert.That(((ExceptionFail)handler.StoppedReason!).Exception, Is.SameAs(exception));
        Assert.That(callbackReason, Is.SameAs(handler.StoppedReason));
        Assert.That(transport.DestroyTransportCalls, Is.EqualTo(1));
        Assert.That(transport.EventLog, Is.EqualTo(new[]
        {
            "beginConnect", "readyToConnect", "connectionFinished", "handlerStopped", "destroyTransport",
            "startCallback"
        }));
    }

    [Test]
    public void ConnectionFinished_WhenActionAttachesConnectedEndpoint_IsConnectedDuringAction()
    {
        var transport = Create();
        using var transportHandler = CreateHandler(transport);
        using var endpointHandler = CreateHandler();
        using var endpoint = new TestRawReliableClientEndpoint(endpointHandler);
        Assert.That(transport.Init(transportHandler), Is.True);
        Assert.That(transport.Start(_ => { }), Is.True);
        endpoint.ConnectAndComplete();
        bool? connectedInsideAction = null;

        transport.ExposedConnectionFinished(_ =>
        {
            transport.ExposedAttachEndpoint(endpoint);
            connectedInsideAction = transport.IsConnected;
            transport.EventLog.Add("connectionFinished");
        });

        Assert.That(connectedInsideAction, Is.True);
        Assert.That(transport.IsConnected, Is.True);
        Assert.That(transport.ExposedAttachedEndpoint, Is.SameAs(endpoint));
        Assert.That(transport.EventLog, Is.EqualTo(new[]
        {
            "beginConnect", "readyToConnect", "connectionFinished"
        }));
    }

    [Test]
    public void ConnectionFinished_WhenConstructed_IsIgnoredAndInitStillWorks()
    {
        var transport = Create();
        using var handler = CreateHandler(transport);
        bool actionCalled = false;

        transport.ExposedConnectionFinished(_ => actionCalled = true);

        Assert.That(actionCalled, Is.False);
        Assert.That(transport.DestroyTransportCalls, Is.Zero);
        Assert.That(transport.IsValid, Is.True);
        Assert.That(transport.Init(handler), Is.True);
        Assert.That(transport.Start(_ => { }), Is.True);
        Assert.That(transport.BeginConnectCalls, Is.EqualTo(1));
        Assert.That(transport.OnReadyToConnectCalls, Is.EqualTo(1));
    }

    [Test]
    public void ConnectionFailed_WhenConstructed_IsIgnoredAndInitStillWorks()
    {
        var transport = Create();
        using var handler = CreateHandler(transport);

        transport.ExposedConnectionFailed();

        Assert.That(transport.DestroyTransportCalls, Is.Zero);
        Assert.That(transport.IsValid, Is.True);
        Assert.That(transport.Init(handler), Is.True);
        Assert.That(transport.Start(_ => { }), Is.True);
        Assert.That(transport.BeginConnectCalls, Is.EqualTo(1));
        Assert.That(transport.OnReadyToConnectCalls, Is.EqualTo(1));
    }

    [Test]
    public void ConnectionFailed_WhenConnecting_FailsAndNotifiesStopped()
    {
        var transport = Create();
        using var handler = CreateHandler(transport);
        Assert.That(transport.Init(handler), Is.True);
        StopReason? callbackReason = null;
        Assert.That(transport.Start(r =>
        {
            callbackReason = r;
            transport.EventLog.Add("startCallback");
        }), Is.True);

        transport.ExposedConnectionFailed();

        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.DisconnectedCount, Is.Zero);
        var reason = (TextFail)handler.StoppedReason!;
        Assert.That(reason.Text, Is.EqualTo("Connection failed"));
        Assert.That(reason.Source, Is.EqualTo(transport.Name));
        Assert.That(callbackReason, Is.SameAs(handler.StoppedReason));
        Assert.That(transport.DestroyTransportCalls, Is.EqualTo(1));
        Assert.That(transport.IsValid, Is.False);
        Assert.That(transport.IsConnected, Is.False);
        Assert.That(transport.EventLog, Is.EqualTo(new[]
        {
            "beginConnect", "readyToConnect", "handlerStopped", "destroyTransport", "startCallback"
        }));
    }

    [Test]
    public void ConnectionFailed_WhenConnected_StopsWithTextFailAndNotifiesDisconnect()
    {
        var transport = Create();
        using var handler = CreateHandler(transport);
        StopReason? callbackReason = null;
        InitAndConnect(transport, handler, r =>
        {
            callbackReason = r;
            transport.EventLog.Add("startCallback");
        });

        transport.ExposedConnectionFailed();

        Assert.That(handler.DisconnectedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        var reason = (TextFail)handler.StoppedReason!;
        Assert.That(reason.Source, Is.EqualTo(transport.Name));
        Assert.That(reason.Text, Does.Contain("Connected"));
        Assert.That(reason.Text, Does.Contain("Connecting"));
        Assert.That(callbackReason, Is.SameAs(handler.StoppedReason));
        Assert.That(callbackReason, Is.SameAs(handler.DisconnectedReason));
        Assert.That(transport.DestroyTransportCalls, Is.EqualTo(1));
        Assert.That(transport.IsValid, Is.True);
        Assert.That(transport.IsConnected, Is.False);
        Assert.That(transport.EventLog, Is.EqualTo(new[]
        {
            "beginConnect", "readyToConnect", "handlerDisconnected", "handlerStopped", "destroyTransport",
            "startCallback"
        }));
    }

    [Test]
    public void ConnectionFailed_WhenInitialized_IsIgnoredAndStartStillWorks()
    {
        var transport = Create();
        using var handler = CreateHandler(transport);
        Assert.That(transport.Init(handler), Is.True);

        transport.ExposedConnectionFailed();

        Assert.That(transport.DestroyTransportCalls, Is.Zero);
        Assert.That(handler.StoppedCount, Is.Zero);
        Assert.That(transport.IsValid, Is.True);
        Assert.That(transport.IsConnected, Is.False);

        Assert.That(transport.Start(_ => { }), Is.True);
        Assert.That(transport.BeginConnectCalls, Is.EqualTo(1));
        Assert.That(transport.OnReadyToConnectCalls, Is.EqualTo(1));
        Assert.That(transport.IsConnected, Is.False);
    }

    #endregion

    #region IsConnected

    [Test]
    public void IsConnected_TracksTransportStateWithoutEndpoint()
    {
        var transport = Create();
        using var handler = CreateHandler(transport);

        Assert.That(transport.IsConnected, Is.False);
        Assert.That(transport.Init(handler), Is.True);
        Assert.That(transport.IsConnected, Is.False);
        Assert.That(transport.Start(_ => { }), Is.True);
        Assert.That(transport.IsConnected, Is.False);
        transport.ExposedConnectionFinished(_ => { });
        Assert.That(transport.IsConnected, Is.True);

        Assert.That(transport.Stop(Reason()), Is.True);
        Assert.That(transport.IsConnected, Is.False);
    }

    [Test]
    public void IsConnected_WithAttachedEndpoint_DelegatesToEndpoint()
    {
        var transport = Create();
        using var transportHandler = CreateHandler(transport);
        using var endpointHandler = CreateHandler();
        using var endpoint = new TestRawReliableClientEndpoint(endpointHandler);
        InitAndConnect(transport, transportHandler, _ => { });
        transport.ExposedAttachEndpoint(endpoint);

        Assert.That(transport.IsConnected, Is.False);

        endpoint.ConnectAndComplete();

        Assert.That(transport.IsConnected, Is.True);
    }

    [Test]
    public void IsConnected_IsFalseDuringOnDisconnected()
    {
        var transport = Create();
        using var handler = CreateHandler(transport);
        handler.IsConnectedProbe = () => transport.IsConnected;
        InitAndConnect(transport, handler, _ => { });

        Assert.That(transport.Stop(Reason()), Is.True);

        Assert.That(handler.DisconnectedCount, Is.EqualTo(1));
        Assert.That(handler.IsConnectedDuringDisconnected, Is.False);
    }

    [Test]
    public void IsConnected_IsFalseDuringOnStopped()
    {
        var transport = Create();
        using var handler = CreateHandler(transport);
        handler.IsConnectedProbe = () => transport.IsConnected;
        InitAndConnect(transport, handler, _ => { });

        Assert.That(transport.Stop(Reason()), Is.True);

        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.IsConnectedDuringStopped, Is.False);
    }

    [Test]
    public void IsConnected_IsFalseInsideDestroyTransport()
    {
        var transport = Create();
        using var handler = CreateHandler(transport);
        InitAndConnect(transport, handler, _ => { });

        Assert.That(transport.Stop(Reason()), Is.True);

        Assert.That(transport.DestroyTransportCalls, Is.EqualTo(1));
        Assert.That(transport.IsConnectedDuringDestroy, Is.False);
    }

    #endregion

    #region Endpoint attach and detach

    [Test]
    public void AttachEndpoint_WithNull_Throws()
    {
        var transport = Create();

        Assert.Throws<ArgumentNullException>(() => transport.ExposedAttachEndpoint(null!));

        Assert.That(transport.ExposedAttachedEndpoint, Is.Null);
    }

    [Test]
    public void AttachEndpoint_WhenAlreadyAttached_ThrowsAndKeepsFirst()
    {
        var transport = Create();
        using var firstHandler = CreateHandler();
        using var secondHandler = CreateHandler();
        using var first = new TestRawReliableClientEndpoint(firstHandler);
        using var second = new TestRawReliableClientEndpoint(secondHandler);
        transport.ExposedAttachEndpoint(first);

        Assert.Throws<InvalidOperationException>(() => transport.ExposedAttachEndpoint(second));

        Assert.That(transport.ExposedAttachedEndpoint, Is.SameAs(first));
    }

    [Test]
    public void AttachEndpoint_EndpointStop_StopsTransportWithSameReason()
    {
        var transport = Create();
        using var transportHandler = CreateHandler(transport);
        using var endpointHandler = CreateHandler();
        using var endpoint = new TestRawReliableClientEndpoint(endpointHandler);
        StopReason? callbackReason = null;
        InitAndConnect(transport, transportHandler, r => callbackReason = r);
        transport.ExposedAttachEndpoint(endpoint);
        endpoint.ConnectAndComplete();
        var reason = Reason();

        endpoint.StopAndComplete(reason, tryBeGentle: false);

        Assert.That(transport.LastDestroyReason, Is.SameAs(reason));
        Assert.That(transport.ExposedAttachedEndpoint, Is.Null);
        Assert.That(transport.DestroyTransportCalls, Is.EqualTo(1));
        Assert.That(transportHandler.DisconnectedCount, Is.Zero);
        Assert.That(transportHandler.StoppedCount, Is.Zero);
        Assert.That(callbackReason, Is.SameAs(reason));
        Assert.That(transport.IsConnected, Is.False);
        Assert.That(endpointHandler.StoppedReason, Is.SameAs(reason));
    }

    [Test]
    public void Stop_WhileConnected_DetachesEndpointAfterDestroy()
    {
        var transport = Create();
        using var transportHandler = CreateHandler(transport);
        using var endpointHandler = CreateHandler();
        using var endpoint = new TestRawReliableClientEndpoint(endpointHandler);
        StopReason? callbackReason = null;
        InitAndConnect(transport, transportHandler, r => callbackReason = r);
        transport.ExposedAttachEndpoint(endpoint);
        var reason = Reason();

        Assert.That(transport.Stop(reason), Is.True);

        Assert.That(transport.DestroyTransportCalls, Is.EqualTo(1));
        Assert.That(transport.LastDestroyReason, Is.SameAs(reason));
        Assert.That(transport.ExposedAttachedEndpoint, Is.Null);
        Assert.That(transportHandler.DisconnectedCount, Is.Zero);
        Assert.That(transportHandler.StoppedCount, Is.Zero);
        Assert.That(callbackReason, Is.SameAs(reason));
    }

    [Test]
    public void DestroyTransport_SeesAttachedEndpointBeforeBaseDetaches()
    {
        var transport = Create();
        using var transportHandler = CreateHandler(transport);
        using var endpointHandler = CreateHandler();
        using var endpoint = new TestRawReliableClientEndpoint(endpointHandler);
        InitAndConnect(transport, transportHandler, _ => { });
        endpoint.ConnectAndComplete();
        transport.ExposedAttachEndpoint(endpoint);
        Assert.That(transport.IsConnected, Is.True);

        Assert.That(transport.Stop(Reason()), Is.True);

        Assert.That(transport.DestroyTransportCalls, Is.EqualTo(1));
        Assert.That(transport.EndpointDuringDestroy, Is.SameAs(endpoint));
        Assert.That(transport.ExposedAttachedEndpoint, Is.Null);
        // The attached endpoint is still the connectivity source until the base detaches it.
        Assert.That(transport.IsConnectedDuringDestroy, Is.True);
        Assert.That(transport.IsConnected, Is.False);
    }

    #endregion

    #region Stop and terminal notifications

    [Test]
    public void Stop_AfterInitBeforeStart_KeepsInitializedStateAndAllowsStart()
    {
        var transport = Create();
        using var handler = CreateHandler(transport);
        Assert.That(transport.Init(handler), Is.True);

        Assert.That(transport.Stop(Reason()), Is.True);

        Assert.That(transport.DestroyTransportCalls, Is.Zero);
        Assert.That(handler.StoppedCount, Is.Zero);
        Assert.That(transport.IsValid, Is.True);
        Assert.That(transport.IsConnected, Is.False);

        bool callbackCalled = false;
        Assert.That(transport.Start(_ => callbackCalled = true), Is.True);

        Assert.That(transport.BeginConnectCalls, Is.EqualTo(1));
        Assert.That(transport.OnReadyToConnectCalls, Is.EqualTo(1));
        Assert.That(callbackCalled, Is.False);
    }

    [Test]
    public void Stop_WhileConnecting_NotifiesStoppedThenDestroys()
    {
        var transport = Create();
        using var handler = CreateHandler(transport);
        Assert.That(transport.Init(handler), Is.True);
        StopReason? callbackReason = null;
        Assert.That(transport.Start(r =>
        {
            callbackReason = r;
            transport.EventLog.Add("startCallback");
        }), Is.True);
        var reason = Reason();

        Assert.That(transport.Stop(reason), Is.True);

        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.DisconnectedCount, Is.Zero);
        Assert.That(handler.StoppedReason, Is.SameAs(reason));
        Assert.That(transport.DestroyTransportCalls, Is.EqualTo(1));
        Assert.That(transport.LastDestroyReason, Is.SameAs(reason));
        Assert.That(callbackReason, Is.SameAs(reason));
        Assert.That(transport.IsValid, Is.True);
        Assert.That(transport.IsConnected, Is.False);
        Assert.That(transport.EventLog, Is.EqualTo(new[]
        {
            "beginConnect", "readyToConnect", "handlerStopped", "destroyTransport", "startCallback"
        }));
    }

    [Test]
    public void Stop_WhileConnected_NotifiesDisconnectedThenStoppedThenDestroys()
    {
        var transport = Create();
        using var handler = CreateHandler(transport);
        StopReason? callbackReason = null;
        InitAndConnect(transport, handler, r =>
        {
            callbackReason = r;
            transport.EventLog.Add("startCallback");
        });
        var reason = Reason();

        Assert.That(transport.Stop(reason), Is.True);

        Assert.That(handler.DisconnectedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.DisconnectedReason, Is.SameAs(reason));
        Assert.That(handler.StoppedReason, Is.SameAs(reason));
        Assert.That(transport.DestroyTransportCalls, Is.EqualTo(1));
        Assert.That(transport.LastDestroyReason, Is.SameAs(reason));
        Assert.That(callbackReason, Is.SameAs(reason));
        Assert.That(transport.IsValid, Is.True);
        Assert.That(transport.IsConnected, Is.False);
        Assert.That(transport.EventLog, Is.EqualTo(new[]
        {
            "beginConnect", "readyToConnect", "handlerDisconnected", "handlerStopped", "destroyTransport",
            "startCallback"
        }));
    }

    [Test]
    public void Stop_WhileConnected_WhenOnDisconnectedThrows_StillCallsOnStoppedAndDestroys()
    {
        var transport = Create();
        using var handler = CreateHandler(transport);
        var exception = new InvalidOperationException("disconnected failure");
        handler.DisconnectedException = exception;
        InitAndConnect(transport, handler, _ => { });
        var reason = Reason();

        Assert.DoesNotThrow(() => transport.Stop(reason));

        Assert.That(handler.DisconnectedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.ThrownDisconnectedException, Is.SameAs(exception));
        Assert.That(handler.ThrownStoppedException, Is.Null);
        Assert.That(transport.DestroyTransportCalls, Is.EqualTo(1));
        Assert.That(transport.LastDestroyReason, Is.SameAs(reason));
        Assert.That(transport.IsConnected, Is.False);
        Assert.That(transport.IsValid, Is.True);
    }

    [Test]
    public void Stop_WhileConnected_WhenOnStoppedThrows_StillDestroys()
    {
        var transport = Create();
        using var handler = CreateHandler(transport);
        var exception = new InvalidOperationException("stopped failure");
        handler.StoppedException = exception;
        InitAndConnect(transport, handler, _ => { });
        var reason = Reason();

        Assert.DoesNotThrow(() => transport.Stop(reason));

        Assert.That(handler.DisconnectedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.ThrownStoppedException, Is.SameAs(exception));
        Assert.That(transport.DestroyTransportCalls, Is.EqualTo(1));
        Assert.That(transport.LastDestroyReason, Is.SameAs(reason));
        Assert.That(transport.IsConnected, Is.False);
        Assert.That(transport.IsValid, Is.True);
    }

    [Test]
    public void Stop_WhileConnected_WhenDestroyThrows_StillDetachesAndNotifiesStartCallback()
    {
        var transport = Create();
        using var transportHandler = CreateHandler(transport);
        using var endpointHandler = CreateHandler();
        using var endpoint = new TestRawReliableClientEndpoint(endpointHandler);
        StopReason? callbackReason = null;
        InitAndConnect(transport, transportHandler, r => callbackReason = r);
        endpoint.ConnectAndComplete();
        transport.ExposedAttachEndpoint(endpoint);
        transport.DestroyException = new InvalidOperationException("destroy failure");
        var reason = Reason();
        bool stopped = false;

        Assert.DoesNotThrow(() => stopped = transport.Stop(reason));

        Assert.That(stopped, Is.True);
        Assert.That(transport.DestroyExceptionThrown, Is.True);
        Assert.That(transport.DestroyTransportCalls, Is.EqualTo(1));
        Assert.That(transport.LastDestroyReason, Is.SameAs(reason));
        Assert.That(transport.ExposedAttachedEndpoint, Is.Null);
        Assert.That(callbackReason, Is.SameAs(reason));
        Assert.That(transport.IsStarted, Is.False);
        Assert.That(transport.IsConnected, Is.False);
        Assert.That(transportHandler.DisconnectedCount, Is.Zero);
        Assert.That(transportHandler.StoppedCount, Is.Zero);
    }

    [Test]
    public void Stop_WhileConnecting_WhenOnStoppedThrows_StillDestroys()
    {
        var transport = Create();
        using var handler = CreateHandler(transport);
        var exception = new InvalidOperationException("stopped failure");
        handler.StoppedException = exception;
        Assert.That(transport.Init(handler), Is.True);
        StopReason? callbackReason = null;
        Assert.That(transport.Start(r => callbackReason = r), Is.True);
        var reason = Reason();

        Assert.DoesNotThrow(() => transport.Stop(reason));

        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.DisconnectedCount, Is.Zero);
        Assert.That(handler.ThrownStoppedException, Is.SameAs(exception));
        Assert.That(transport.DestroyTransportCalls, Is.EqualTo(1));
        Assert.That(transport.LastDestroyReason, Is.SameAs(reason));
        Assert.That(callbackReason, Is.SameAs(reason));
        Assert.That(transport.IsConnected, Is.False);
        Assert.That(transport.IsValid, Is.True);
    }

    [Test]
    public void Stop_WhileConnectedWithAttachedEndpoint_SkipsHandlerNotifications()
    {
        var transport = Create();
        using var transportHandler = CreateHandler(transport);
        using var endpointHandler = CreateHandler();
        using var endpoint = new TestRawReliableClientEndpoint(endpointHandler);
        StopReason? callbackReason = null;
        InitAndConnect(transport, transportHandler, r => callbackReason = r);
        transport.ExposedAttachEndpoint(endpoint);
        var reason = Reason();

        Assert.That(transport.Stop(reason), Is.True);

        Assert.That(transportHandler.DisconnectedCount, Is.Zero);
        Assert.That(transportHandler.StoppedCount, Is.Zero);
        Assert.That(transport.DestroyTransportCalls, Is.EqualTo(1));
        Assert.That(transport.LastDestroyReason, Is.SameAs(reason));
        Assert.That(transport.ExposedAttachedEndpoint, Is.Null);
        Assert.That(callbackReason, Is.SameAs(reason));
    }

    #endregion
}
