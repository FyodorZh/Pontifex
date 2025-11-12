using Serializer.BinarySerializer;

namespace Serializer.Tools
{
    public class BlobDataDesc : IDataStruct
    {
        public const int ExporterVersion = 1;

        public int DataVersion;
        public DataTypeDesc[] DataDesc;

        public virtual bool Serialize(IBinarySerializer stream)
        {
            if (!stream.isReader)
            {
                DataVersion = ExporterVersion;
            }

            stream.Add(ref DataVersion);

            if (stream.isReader)
            {
                if (DataVersion != ExporterVersion)
                {
                    Log.e("Resources data blob version is incorrect! Blob - {0}, Exporter - {1}", DataVersion, ExporterVersion);
                    return false;
                }
            }

            stream.Add(ref DataDesc);

            return true;
        }

        public void AppendHeaderSize(int headerSize)
        {
            if (headerSize > 0 && DataDesc != null)
            {
                int count = DataDesc.Length;
                for (int i = 0; i < count; i++)
                {
                    DataDesc[i].StartDataPos += headerSize;
                }
            }
        }
    }
}