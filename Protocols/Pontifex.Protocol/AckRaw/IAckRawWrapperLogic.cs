using System;
using System.Collections.Generic;
using Pontifex.Abstractions;
using Pontifex.Utils;

namespace Pontifex.Protocols
{
    public interface IAckRawWrapperLogic
    {
        void OnConnected();
        void OnDisconnected();
        bool ProcessReceivedData(UnionDataList receivedData);
        bool ProcessSentData(UnionDataList sentData);
        void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null);
    }
}