using Serializer.BinarySerializer;

namespace Shared.Union
{
    public struct Bytes23 : IArray<byte>, IDataStruct
    {
        private BytesBuffer mBytes0;
        private BytesBuffer mBytes1;
        private BytesBuffer mBytes2;

        public int Capacity
        {
            get
            {
                return 23;
            }
        }

        public int Length
        {
            get
            {
                int len = mBytes0[0] - 1;
                if (len > Capacity)
                    len = Capacity;
                return len;
            }
            set
            {
                if (value > Capacity)
                {
                    throw new System.ArgumentOutOfRangeException();
                }
                mBytes0[0] = (byte)(value + 1);
            }
        }

        public bool IsNull
        {
            get
            {
                return mBytes0[0] == 0;
            }
        }

        public void SetNull()
        {
            mBytes0[0] = 0;
        }

        public byte this[int id]
        {
            get
            {
                if (id < 0 || id >= Length)
                {
                    throw new System.IndexOutOfRangeException();
                }

                id += 1;
                switch (id / 8)
                {
                    case 0:
                        return mBytes0[id % 8];
                    case 1:
                        return mBytes1[id % 8];
                    case 2:
                        return mBytes2[id % 8];
                    default:
                        throw new System.IndexOutOfRangeException();
                }
            }
            set
            {
                if (id < 0 || id >= Length)
                {
                    throw new System.IndexOutOfRangeException();
                }

                id += 1;
                switch (id / 8)
                {
                    case 0:
                        mBytes0[id % 8] = value;
                        break;
                    case 1:
                        mBytes1[id % 8] = value;
                        break;
                    case 2:
                        mBytes2[id % 8] = value;
                        break;
                    default:
                        throw new System.IndexOutOfRangeException();
                }
            }
        }

        public bool Add(byte data)
        {
            int len = Length;
            if (len < Capacity)
            {
                Length = (len < 0) ? 1 : (len + 1);
                this[Length - 1] = data;
                return true;
            }
            return false;
        }

        public bool Serialize(IBinarySerializer dst)
        {
            mBytes0.Serialize(dst);

            int len = Length;
            if (len > 7)
            {
                mBytes1.Serialize(dst);
                if (len > 15)
                {
                    mBytes2.Serialize(dst);
                }
            }
            return true;
        }
    }
}
