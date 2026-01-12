using Archivarius;
using Pontifex.Utils;

namespace Pontifex.Api.Protocol
{
    internal struct ReceivedMessage // TODO: remove this struct
    {
        public readonly UnionDataList? Buffer;
        public readonly ISerializer? Reader;

        public ReceivedMessage(UnionDataList buffer)
        {
            Buffer = buffer;
            Reader = null;
        }

        public ReceivedMessage(ISerializer reader)
        {
            Reader = reader;
            Buffer = null;
        }
    }

    internal interface IDataModelSender
    {
        bool IsServerMode { get; }
        SendResult Send(ushort declId, UnionDataList data);
        SendResult Send<TDataStruct>(ushort declId, TDataStruct data) where TDataStruct : IDataStruct;
    }
}