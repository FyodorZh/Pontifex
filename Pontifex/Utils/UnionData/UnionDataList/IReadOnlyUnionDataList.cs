using System.Diagnostics.CodeAnalysis;
using Actuarius.Collections;
using Actuarius.Memory;

namespace Pontifex.Utils
{
    public interface IReadOnlyUnionDataList
    {
        IReadOnlyArray<UnionData> Elements { get; }

        bool IsAlive { get; }

        void AddRef();

        void Release();

        UnionDataType PeekFirstType();

        int GetDataSize();

        bool SerializeTo<TByteSink>(ref TByteSink sink)
            where TByteSink : IByteSink;

        bool SerializeTo(IMultiRefByteArray buffer);

        bool Serialize(IPool<IMultiRefByteArray, int> bytesPool, [MaybeNullWhen(false)] out IMultiRefByteArray serializedData);
        UnionDataList Clone(ICollectablePool? pool);
    }
}
