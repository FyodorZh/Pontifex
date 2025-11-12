using System.IO;
using Serializer.BinarySerializer;

namespace Serializer.Tools
{
    public abstract class PlatformerDataDescriptions : CommonDataContainer
    {
        private const string DATA_PATH = "Assets/LogicResources/Runtime/Data/";
        private const string SERVER_PATH = "../deploy/server/data/" + DATA_PATH;        

        public string DataPath
        {
            get { return DATA_PATH; }
        }

        public string ServerPath
        {
            get { return SERVER_PATH; }
        }

        public string BlobPath
        {
            get { return DataPath + FileName + ".bytes"; }
        }

        protected abstract string FileName { get; }

        protected override string GetBlobPath(string dataPath)
        {
            return Path.Combine(dataPath, BlobPath);
        }
    }
}
