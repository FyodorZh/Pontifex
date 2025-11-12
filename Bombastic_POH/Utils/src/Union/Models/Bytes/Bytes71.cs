using Serializer.BinarySerializer;

namespace Shared.Union
{
    public struct Bytes71 : IArray<byte>, IDataStruct
    {
        private BytesBuffer mBytes0;
        private BytesBuffer mBytes1;
        private BytesBuffer mBytes2;
        private BytesBuffer mBytes3;
        private BytesBuffer mBytes4;
        private BytesBuffer mBytes5;
        private BytesBuffer mBytes6;
        private BytesBuffer mBytes7;
        private BytesBuffer mBytes8;
        private BytesBuffer mBytes9;
        
        public int Capacity
        {
            get
            {
                return 10 * 8 - 1;
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
                    case 3:
                        return mBytes3[id % 8];
                    case 4:
                        return mBytes4[id % 8];
                    case 5:
                        return mBytes5[id % 8];
                    case 6:
                        return mBytes6[id % 8];
                    case 7:
                        return mBytes7[id % 8];
                    case 8:
                        return mBytes8[id % 8];
                    case 9:
                        return mBytes9[id % 8];
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
                    case 3:
                        mBytes3[id % 8] = value;
                        break;
                    case 4:
                        mBytes4[id % 8] = value;
                        break;
                    case 5:
                        mBytes5[id % 8] = value;
                        break;
                    case 6:
                        mBytes6[id % 8] = value;
                        break;
                    case 7:
                        mBytes7[id % 8] = value;
                        break;
                    case 8:
                        mBytes8[id % 8] = value;
                        break;
                    case 9:
                        mBytes9[id % 8] = value;
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
            if (len >= 1 * 8)
            {
                mBytes1.Serialize(dst);
                if (len >= 2 * 8)
                {
                    mBytes2.Serialize(dst);
                    if (len >= 3 * 8)
                    {
                        mBytes3.Serialize(dst);
                        if (len >= 4 * 8)
                        {
                            mBytes4.Serialize(dst);
                            if (len >= 5 * 8)
                            {
                                mBytes5.Serialize(dst);
                                if (len >= 6 * 8)
                                {
                                    mBytes6.Serialize(dst);
                                    if (len >= 7 * 8)
                                    {
                                        mBytes7.Serialize(dst);
                                        if (len >= 8 * 8)
                                        {
                                            mBytes8.Serialize(dst);
                                            if (len >= 9 * 8)
                                            {
                                                mBytes9.Serialize(dst);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return true;
        }
    }
}
