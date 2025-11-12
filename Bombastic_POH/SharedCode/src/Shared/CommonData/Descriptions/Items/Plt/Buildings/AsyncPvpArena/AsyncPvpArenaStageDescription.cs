using Serializer.BinarySerializer;

namespace Shared.CommonData.Plt
{
    public class AsyncPvpArenaStageDescription : StageDescription
    {    
        public override bool Serialize(IBinarySerializer dst)
        {    
            return base.Serialize(dst);
        }
    }
}
