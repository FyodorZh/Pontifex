using Serializer.BinarySerializer;
using Serializer.Extensions;
using Serializer.Extensions.ID;

namespace Shared.NeoMeta.Items
{
    public class CollectAndOpenRequest : IDataStruct
    {
        public CollectAndOpenRequest()
        {           
        }

        public CollectAndOpenRequest(CollectInfo? collect, OpenContainer[] containers)
        {
            Collect = collect;
            Containers = containers;
        }

        public CollectInfo? Collect;

        public OpenContainer[] Containers;

        public bool Serialize(IBinarySerializer dst)
        {
            dst.AddNullable(ref Collect);
            dst.Add(ref Containers);

            return true;
        }

       public struct CollectInfo : IDataStruct
       {
           public CollectInfo(ID<Item> containerBuildingId, short[] packIds)
           {
               ContainerBuildingId = containerBuildingId;
               PackIds = packIds;
           }

           public ID<Item> ContainerBuildingId;
           public short[] PackIds;

           public bool Serialize(IBinarySerializer dst)
           {
               dst.AddId(ref ContainerBuildingId);
               dst.Add(ref PackIds);

               return true;
           }
       }
    }
}
