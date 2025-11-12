using Serializer.BinarySerializer;

namespace Shared
{
    namespace CommonData
    {
        public class TutorialStage : IDataStruct
        {
            public byte StageId;
            //public LootTableTemplate[] lootTemplate;

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref this.StageId);
                //dst.Add(ref this.lootTemplate);

                return true;
            }
        }
    }
}