using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Reliable.Protocols;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Raw.Reliable.Protocol.Harness
{
    /// <summary>
    /// A recording reliable client handler.
    /// </summary>
    internal sealed class RecordingRawReliableClientHandler : IRawReliableClientHandler
    {
        public IRawReliableEndpoint? Endpoint { get; private set; }

        public int ConnectedCount { get; private set; }

        public int ReceivedCount { get; private set; }

        public StopReason? DisconnectReason { get; private set; }

        public StopReason? StopReason { get; private set; }

        public List<string> Events { get; } = new List<string>();

        public List<List<string>> Received { get; } = new List<List<string>>();

        public void OnConnected(IRawReliableEndpoint endPoint)
        {
            Endpoint = endPoint;
            ConnectedCount++;
            Events.Add("connected");
        }

        public void OnReceived(UnionDataList receivedBuffer)
        {
            ReceivedCount++;
            Received.Add(Payload.Describe(receivedBuffer));
            Events.Add("received");
            receivedBuffer.Release();
        }

        public void OnDisconnected(StopReason reason)
        {
            DisconnectReason = reason;
            Events.Add("disconnected");
        }

        public void OnStopped(StopReason reason)
        {
            StopReason = reason;
            Events.Add("stopped");
        }
    }

    /// <summary>
    /// A recording reliable server session handler.
    /// </summary>
    internal sealed class RecordingRawReliableServerHandler : IRawReliableServerHandler
    {
        public IRawReliableEndpoint? Endpoint { get; private set; }

        public int ConnectedCount { get; private set; }

        public int ReceivedCount { get; private set; }

        public StopReason? DisconnectReason { get; private set; }

        public StopReason? StopReason { get; private set; }

        public List<string> Events { get; } = new List<string>();

        public List<List<string>> Received { get; } = new List<List<string>>();

        public void OnConnected(IRawReliableEndpoint endPoint)
        {
            Endpoint = endPoint;
            ConnectedCount++;
            Events.Add("connected");
        }

        public void OnReceived(UnionDataList receivedBuffer)
        {
            ReceivedCount++;
            Received.Add(Payload.Describe(receivedBuffer));
            Events.Add("received");
            receivedBuffer.Release();
        }

        public void OnDisconnected(StopReason reason)
        {
            DisconnectReason = reason;
            Events.Add("disconnected");
        }

        public void OnStopped(StopReason reason)
        {
            StopReason = reason;
            Events.Add("stopped");
        }
    }

    /// <summary>
    /// A configurable wrapper logic recording every negotiation hook invocation.
    /// </summary>
    internal sealed class RecordingWrapperLogic : RawReliableWrapperLogic
    {
        public RecordingWrapperLogic(ILogger logger, IMemoryRental memoryRental)
            : base(logger, memoryRental)
        {
        }

        public Action<UnionDataList>? Fill { get; set; }

        public bool CheckResult { get; set; } = true;

        public UnionDataList? ResponseTail { get; set; }

        public bool ProcessReceivedResult { get; set; } = true;

        public bool ProcessSentResult { get; set; } = true;

        public int ConnectedCount { get; private set; }

        public int DisconnectedCount { get; private set; }

        public int ReceivedProcessedCount { get; private set; }

        public int SentProcessedCount { get; private set; }

        public List<List<string>> CheckedTails { get; } = new List<List<string>>();

        public List<List<string>> AcceptedTails { get; } = new List<List<string>>();

        public override void OnConnected()
        {
            ConnectedCount++;
        }

        public override void OnDisconnected()
        {
            DisconnectedCount++;
        }

        public override bool ProcessReceivedData(UnionDataList receivedData)
        {
            ReceivedProcessedCount++;
            return ProcessReceivedResult;
        }

        public override bool ProcessSentData(UnionDataList sentData)
        {
            SentProcessedCount++;
            return ProcessSentResult;
        }

        public override void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
        {
        }

        public override void FillNegotiationData(UnionDataList customData)
        {
            Fill?.Invoke(customData);
        }

        public override void OnNegotiationAccepted(UnionDataList customData)
        {
            AcceptedTails.Add(Payload.Describe(customData));
        }

        public override UnionDataList CreateNegotiationResponse()
        {
            var tail = ResponseTail;
            ResponseTail = null;
            return tail ?? base.CreateNegotiationResponse();
        }

        public override bool CheckNegotiationData(UnionDataList customData)
        {
            CheckedTails.Add(Payload.Describe(customData));
            return CheckResult;
        }
    }
}
