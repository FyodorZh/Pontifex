using System;
using System.Collections.Generic;
using System.Threading;
using Pontifex.RR.Unreliable;
using Pontifex.Utils;

namespace Pontifex.Tests.Transports;

internal enum UnreliableServerHandlerMode
{
    Accept,
    Reject,
    Throw
}

/// <summary>
/// Server handler that records every presented request and response handle, releases the request,
/// and can accept, reject, or throw.
/// </summary>
internal sealed class RecordingUnreliableServerHandler : IRRUnreliableServerHandler
{
    private readonly object _locker = new object();
    private readonly List<int> _requestValues = new List<int>();
    private readonly List<IEndPoint> _requestSources = new List<IEndPoint>();
    private readonly List<IRRUnreliableResponse> _handles = new List<IRRUnreliableResponse>();

    public UnreliableServerHandlerMode Mode { get; set; } = UnreliableServerHandlerMode.Accept;

    public ManualResetEventSlim Requested { get; } = new ManualResetEventSlim(false);

    public int RequestCount
    {
        get
        {
            lock (_locker)
            {
                return _requestValues.Count;
            }
        }
    }

    public int[] RequestValues
    {
        get
        {
            lock (_locker)
            {
                return _requestValues.ToArray();
            }
        }
    }

    public IEndPoint[] RequestSources
    {
        get
        {
            lock (_locker)
            {
                return _requestSources.ToArray();
            }
        }
    }

    public IRRUnreliableResponse[] Handles
    {
        get
        {
            lock (_locker)
            {
                return _handles.ToArray();
            }
        }
    }

    public IRRUnreliableResponse? LastHandle
    {
        get
        {
            lock (_locker)
            {
                return _handles.Count == 0 ? null : _handles[_handles.Count - 1];
            }
        }
    }

    public bool OnRequested(IEndPoint client, UnionDataList request,
        IRRUnreliableResponse response)
    {
        int value = request.Elements.Count > 0 ? request.Elements[0].Alias.IntValue : int.MinValue;

        lock (_locker)
        {
            _requestValues.Add(value);
            _requestSources.Add(client);
            _handles.Add(response);
        }

        try
        {
            if (Mode == UnreliableServerHandlerMode.Throw)
            {
                throw new InvalidOperationException("server handler failure");
            }

            return Mode != UnreliableServerHandlerMode.Reject;
        }
        finally
        {
            request.Release();
            Requested.Set();
        }
    }
}
