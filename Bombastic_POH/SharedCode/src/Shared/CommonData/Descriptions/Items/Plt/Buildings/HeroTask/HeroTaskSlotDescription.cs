using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt.HeroTasks
{
    public class HeroTaskSlotDescription : IDataStruct
    {
        [EditorField(EditorFieldParameter.Unique | EditorFieldParameter.UseAsId)]
        private short _id;

        [EditorField]
        private Requirement[] _requirements;

        public HeroTaskSlotDescription()
        {            
        }

        public HeroTaskSlotDescription(Requirement[] requirements, short id)
        {
            _requirements = requirements;
            _id = id;
        }

        public short Id
        {
            get { return _id; }
        }

        public Requirement[] Requirements
        {
            get { return _requirements; }
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _requirements);
            dst.Add(ref _id);
            return true;
        }
    }
}
