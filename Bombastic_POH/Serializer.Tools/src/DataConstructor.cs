using Serializer.BinarySerializer;
using Serializer.Factory;
using Shared;

namespace Serializer.Tools
{
    public abstract class DataConstructor : System.IDisposable
    {
        private DataTypeDesc[] mDataDesc = null;
        private ByteArray mData;

        private bool mInited = false;
        public bool IsInited { get { return mInited; } }

        protected bool SetBlobData<T, F>(byte[] data, ref T dataInfo, bool useResourceFactory)
            where T : BlobDataDesc, new()
            where F : IDataStructFactory, new()
        {
            if (data == null)
            {
                return false;
            }
            mData = UnpackData(data);

            var reader = useResourceFactory ?
                DataStreamReader.ThreadInstance<F>(mData.AddRef()) :
                DataStreamReader.ThreadInstance(mData.AddRef());

            dataInfo = new T();
            bool bRead = dataInfo.Serialize(reader);

            mInited = bRead || !reader.IsEndOfBuffer;
            if (!mInited)
            {
                dataInfo = default(T);
                Log.e("Wrong data. Read = {0}.", mInited, reader.Reader.BufferSize, reader.Reader.BufferPos);
            }

            reader.Reader.Reset();

            mDataDesc = dataInfo.DataDesc;
            return mInited;
        }

        private static ByteArray UnpackData(byte[] data)
        {
            if (data.Length >= 2 && data[0] == 0x78 && data[1] == 0xDA)
            {
                System.IO.MemoryStream ms = new System.IO.MemoryStream();
                Ionic.Zlib.ZlibStream zOut = new Ionic.Zlib.ZlibStream(ms, Ionic.Zlib.CompressionMode.Decompress, true);
                zOut.Write(data, 0, data.Length);
                zOut.Flush();
                data = ms.ToArray();
            }
            return ByteArray.AssumeControl(data);
        }

        private IBinarySerializer GetDataStream(DataTypeDesc[] data, int staticDataId, int dataId)
        {
            if (!mInited || data == null || dataId <= 0)
            {
                return null;
            }

            dataId -= 1;

            foreach (DataTypeDesc val in data)
            {
                if (val.DataTypeId == staticDataId)
                {
                    if (val.refDataPos == null
                        || val.refDataPos.Length <= dataId)
                    {
                        return null;
                    }
                    DataRecord dataRec = val.refDataPos[dataId];
                    int dataOffset = val.StartDataPos;

                    if ((dataRec.pos + dataRec.size) > val.DataSize)
                    {
                        // Данные выходят за границы
                        return null;
                    }

                    int globalOffset = dataOffset + dataRec.pos;
                    return DataStreamReader.ThreadInstance(mData.AddRef(), globalOffset);
                }
            }

            return null;
        }

        public int GetDataStreamRecords(DataTypeDesc[] data, int staticDataId)
        {
            if (mInited && data != null)
            {
                foreach (DataTypeDesc val in data)
                {
                    if (val.DataTypeId == staticDataId)
                    {
                        if (val.refDataPos == null)
                        {
                            return 0;
                        }

                        return val.DataCount;
                    }
                }
            }
            return 0;
        }

        public IBinarySerializer GetDataStream(int staticDataId, int dataId)
        {
            return GetDataStream(mDataDesc, staticDataId, dataId);
        }

        public void Dispose()
        {
            mData.Dispose();
            mData = null;
            mInited = false;
        }
    }
}