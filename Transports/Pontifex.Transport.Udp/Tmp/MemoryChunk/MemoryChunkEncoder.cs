// using Actuarius.Memory;
// using Pontifex.Abstractions;
//
// namespace Pontifex.Transports.Udp
// {
//     internal class MemoryChunkEncoder
//     {
//         private readonly byte[] _data;
//         private readonly int _capacity;
//
//         private int _bufferSize;
//
//         public MemoryChunkEncoder(int capacity)
//         {
//             _capacity = capacity;
//             _data = new byte[_capacity];
//         }
//
//         public bool TryPush(MessageId id, IReadOnlyBytes data)
//         {
//             int dataLen = data.Count;
//             int blockDataLen = dataLen + 4;
//             if (_bufferSize + blockDataLen + 2 <= _capacity)
//             {
//                 _data[_bufferSize + 0] = (byte)((blockDataLen >> 0) & 0xFF);
//                 _data[_bufferSize + 1] = (byte)((blockDataLen >> 8) & 0xFF);
//                 _bufferSize += 2;
//
//                 uint _id = id.Id;
//
//                 _data[_bufferSize + 0] = (byte)((_id >> 0) & 0xFF);
//                 _data[_bufferSize + 1] = (byte)((_id >> 8) & 0xFF);
//                 _data[_bufferSize + 2] = (byte)((_id >> 16) & 0xFF);
//                 _data[_bufferSize + 3] = (byte)((_id >> 24) & 0xFF);
//                 _bufferSize += 4;
//
//                 data.CopyTo(_data, _bufferSize, 0, dataLen);
//                 _bufferSize += dataLen;
//                 return true;
//             }
//             return false;
//         }
//
//         public void Clear()
//         {
//             _bufferSize = 0;
//         }
//
//         public bool IsEmpty => _bufferSize == 0;
//
//         public void ShowDataUnsafe(out byte[] data, out int offset, out int count)
//         {
//             data = _data;
//             offset = 0;
//             count = _bufferSize;
//         }
//
//         public override string ToString()
//         {
//             return $"[{_bufferSize} bytes]";
//         }
//     }
// }
