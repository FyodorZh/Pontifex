namespace Serializer.BinarySerializer
{
    public class ManagedWriterVerifier : IManagedWriterVerifier
    {
        private readonly byte[] mVerifyArray;

        public int DataLength { get { return mVerifyArray == null ? 0 : mVerifyArray.Length; } }

        protected ManagedWriterVerifier()
        {
        }

        public ManagedWriterVerifier(byte[] verifyArray)
            : this()
        {
            mVerifyArray = verifyArray;
        }

        protected virtual void LogAssert(string error, params object[] args)
        {
            Log.e(error, args);
        }

        public bool Check(byte v, int pos)
        {
            byte vValue = mVerifyArray[pos];

            bool bRes = vValue == v;

            if (!bRes)
            {
                LogAssert("Verify Error: wrong byte value");
            }
            return bRes;
        }

        public bool Check(char v, int pos)
        {
            int cPos = pos;
            char vValue = EndianIndependentSerializer.CharReader.Read(mVerifyArray, ref cPos);

            bool bRes = vValue == v;
            if (!bRes)
            {
                LogAssert("Verify Error: wrong char value");
            }
            return bRes;
        }

        public bool Check(short v, int pos)
        {
            int cPos = pos;
            short vValue = EndianIndependentSerializer.ShortReader.Read(mVerifyArray, ref cPos);

            bool bRes = vValue == v;
            if (!bRes)
            {
                LogAssert("Verify Error: wrong short value");
            }
            return bRes;
        }

        public bool Check(int v, int pos)
        {
            int cPos = pos;
            int vValue = EndianIndependentSerializer.IntReader.Read(mVerifyArray, ref cPos);

            bool bRes = vValue == v;
            if (!bRes)
            {
                LogAssert("Verify Error: wrong int value");
            }
            return bRes;
        }

        public bool Check(long v, int pos)
        {
            int cPos = pos;
            long vValue = EndianIndependentSerializer.LongReader.Read(mVerifyArray, ref cPos);

            bool bRes = vValue == v;
            if (!bRes)
            {
                LogAssert("Verify Error: wrong long value");
            }
            return bRes;
        }

        public bool Check(float v, int pos)
        {
            int cPos = pos;
            float vValue = EndianIndependentSerializer.FloatReader.Read(mVerifyArray, ref cPos);

            bool bRes = vValue == v;
            if (!bRes)
            {
                LogAssert("Verify Error: wrong float value");
            }
            return bRes;
        }

        public bool Check(double v, int pos)
        {
            int cPos = pos;
            double vValue = EndianIndependentSerializer.DoubleReader.Read(mVerifyArray, ref cPos);

            bool bRes = vValue == v;
            if (!bRes)
            {
                LogAssert("Verify Error: wrong double value");
            }
            return bRes;
        }

        public bool Check(byte[] v, int pos)
        {
            int count = v.Length;
            for (int i = 0; i < count; i++)
            {
                if (!Check(v[i], pos + i))
                {
                    LogAssert("Verify Error: wrong byte[{0}] value", i);
                    return false;
                }
            }

            return true;
        }

        public bool Check(byte[] v, int from, int count, int pos)
        {
            for (int i = 0; i < count; i++)
            {
                if (!Check(v[from + i], pos + i))
                {
                    LogAssert("Verify Error: wrong byte[{0}] value", from + i);
                    return false;
                }
            }

            return true;
        }
    }
}
