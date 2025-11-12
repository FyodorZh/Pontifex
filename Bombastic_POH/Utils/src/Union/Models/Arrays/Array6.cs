namespace Shared.Union
{
    public struct Array6<T> : IArray<T>
    {
        private T mBytes0;
        private T mBytes1;
        private T mBytes2;
        private T mBytes3;
        private T mBytes4;
        private T mBytes5;
        private byte mCount;

        public int Capacity
        {
            get
            {
                return 6;
            }
        }

        public int Length
        {
            get
            {
                int len = mCount - 1;
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
                mCount = (byte)(value + 1);
            }
        }

        public bool IsNull
        {
            get
            {
                return mCount == 0;
            }
        }

        public void SetNull()
        {
            mCount = 0;
        }

        public T this[int id]
        {
            get
            {
                if (id < 0 || id >= Length)
                {
                    throw new System.IndexOutOfRangeException();
                }

                switch (id)
                {
                    case 0:
                        return mBytes0;
                    case 1:
                        return mBytes1;
                    case 2:
                        return mBytes2;
                    case 3:
                        return mBytes3;
                    case 4:
                        return mBytes4;
                    case 5:
                        return mBytes5;
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

                switch (id)
                {
                    case 0:
                        mBytes0 = value;
                        break;
                    case 1:
                        mBytes1 = value;
                        break;
                    case 2:
                        mBytes2 = value;
                        break;
                    case 3:
                        mBytes3 = value;
                        break;
                    case 4:
                        mBytes4 = value;
                        break;
                    case 5:
                        mBytes5 = value;
                        break;
                    default:
                        throw new System.IndexOutOfRangeException();
                }
            }
        }

        public bool Add(T data)
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
    }
}
