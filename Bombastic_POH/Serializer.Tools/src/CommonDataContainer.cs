using System.IO;

namespace Serializer.Tools
{
    public abstract class CommonDataContainer
    {
        protected static byte[] GetBytes(string path)
        {
            return File.ReadAllBytes(path);
        }

        protected abstract string GetBlobPath(string dataPath);

        public void Init(string dataPath)
        {
            Init(GetBytes(GetBlobPath(dataPath)));
        }

        public void Init(byte[] bytes)
        {
            if (bytes != null && bytes.Length > 0)
            {
                InitFromRawData(bytes);
            }
        }

        protected abstract void InitFromRawData(byte[] bytes);
    }
}
