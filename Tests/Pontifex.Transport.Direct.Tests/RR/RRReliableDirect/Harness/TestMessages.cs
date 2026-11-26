using System;
using Actuarius.Memory;
using Pontifex.Utils;

namespace Pontifex.Tests.RR.RRReliableDirect.Harness;

internal static class TestMessages
{
    public static UnionDataList Int(int value)
    {
        var message = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
        message.PutLast(value);
        return message;
    }

    public static UnionDataList PayloadOfSize(int payloadSize)
    {
        // A one-element list has a one-byte count prefix. The array element costs one type byte,
        // the zig-zag var-int length prefix, and the bytes themselves.
        int arrayLength = payloadSize - 2;
        while (payloadSize - 2 - VarIntSize(arrayLength) != arrayLength)
        {
            arrayLength = payloadSize - 2 - VarIntSize(arrayLength);
        }

        var message = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
        message.PutLast(new UnionData(new MultiRefByteArray(new byte[arrayLength])));

        if (message.GetDataSize() != payloadSize)
        {
            message.Release();
            throw new InvalidOperationException(
                $"Could not build a payload of exactly {payloadSize} bytes.");
        }

        return message;
    }

    private static int VarIntSize(int value)
    {
        uint encoded = unchecked((uint)(value << 1));
        int size = 1;
        while ((encoded >>= 7) != 0)
        {
            size++;
        }

        return size;
    }
}
