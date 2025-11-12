using Serializer.BinarySerializer;

namespace Shared.NeoMeta.Items
{
    public class OpenContainersRequest : IDataStruct
    {
        public OpenContainer[] OpenContainers;

        public OpenContainersRequest()
        {
        }

        public OpenContainersRequest(OpenContainer[] openContainers)
        {
            OpenContainers = openContainers;
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref OpenContainers);

            return true;
        }        
    }
}
