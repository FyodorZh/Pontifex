using System;
using Actuarius.Memory;

namespace Pontifex.Abstractions
{
    public readonly struct MessageId : IEquatable<MessageId>
    {
        public static readonly MessageId Void = new MessageId();

        public readonly uint Id;

        public MessageId(uint id)
        {
            Id = id;
        }

        public bool Equals(MessageId other)
        {
            return Id == other.Id;
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is MessageId id && Equals(id);
        }

        public override int GetHashCode()
        {
            return (int)Id;
        }

        public static bool operator ==(MessageId a, MessageId b)
        {
            return a.Id == b.Id;
        }

        public static bool operator !=(MessageId a, MessageId b)
        {
            return a.Id != b.Id;
        }

        public override string ToString()
        {
            return Id.ToString();
        }
    }

    public class MessageIdSource
    {
        private MessageId mLastId = new MessageId();

        public MessageId GenNext()
        {
            mLastId = new MessageId(mLastId.Id + 1);
            return mLastId;
        }
    }

    public struct Message : IReleasableResource
    {
        public MessageId Id;
        public IMultiRefByteArray? Data;

        public Message(MessageId id, IMultiRefByteArray? data)
        {
            Id = id;
            Data = data;
        }

        public void Release()
        {
            if (Data != null)
            {
                Data.Release();
                Data = null;
            }
        }

        public Message Acquire()
        {
            return new Message(Id, Data != null ? Data.Acquire() : null);
        }
    }
}