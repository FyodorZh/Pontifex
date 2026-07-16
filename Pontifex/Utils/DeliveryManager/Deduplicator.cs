using Actuarius.Collections;
using Pontifex.Utils;

namespace Pontifex.DeliveryManager
{
    internal class Deduplicator
    {
        public enum Result
        {
            New,
            Duplicate,
            Overflow
        }

        private readonly CycleQueue<bool> _queue;
        private uint _from;
        private uint _till;
        private ushort _nextPacketId = 1;

        public Deduplicator(int capacity)
        {
            _queue = new CycleQueue<bool>(capacity, false);
            _from = 1;
            _till = 0;
        }

        // ── Outgoing marking ──

        public void MarkAckList(UnionDataList data)
        {
            data.PutFirst(new UnionData((ushort)0));
        }

        public void MarkUserMessage(UnionDataList data)
        {
            data.PutFirst(new UnionData(_nextPacketId));
            _nextPacketId = _nextPacketId == ushort.MaxValue ? (ushort)1 : (ushort)(_nextPacketId + 1);
        }

        // ── Incoming check ──

        public Result Check(UnionDataList data, out bool isUserMessage)
        {
            isUserMessage = false;
            
            if (!data.TryPopFirst(out ushort packetId))
                return Result.Overflow;

            if (packetId == 0)
                return Result.New;

            isUserMessage = true;
            return Received(packetId);
        }

        public Result Received(uint id)
        {
            if (_queue.Count == 0 || id > _till)
            {
                for (uint i = _till + 1; i <= id; ++i)
                {
                    if (!_queue.Put(i == id))
                    {
                        _till = i - 1;
                        return Result.Overflow;
                    }
                }
                _till = id;

                Trim();
                return Result.New;
            }

            if (id < _from)
            {
                return Result.Duplicate;
            }

            int pos = (int)(id - _from);
            if (_queue[pos])
            {
                return Result.Duplicate;
            }

            _queue[pos] = true;
            Trim();

            return Result.New;
        }

        private void Trim()
        {
            while (_till - _from > 0 && _queue[0])
            {
                _queue.TryPop(out _);
                _from += 1;
            }
        }
    }
}
