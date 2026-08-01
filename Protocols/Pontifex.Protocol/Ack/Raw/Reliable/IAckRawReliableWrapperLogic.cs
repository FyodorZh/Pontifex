using System;
using System.Collections.Generic;
using Pontifex.Utils;

namespace Pontifex.Ack.Raw.Reliable.Protocols
{
    public interface IAckRawReliableWrapperLogic
    {
        void OnConnected();
        void OnDisconnected();
        bool ProcessReceivedData(UnionDataList receivedData);
        bool ProcessSentData(UnionDataList sentData);
        void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null);
    }
}