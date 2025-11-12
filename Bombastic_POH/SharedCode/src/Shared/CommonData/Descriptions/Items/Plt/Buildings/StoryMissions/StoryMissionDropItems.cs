using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt.StoryMissions
{
    public class StoryMissionDropItems : IDataStruct
    {
        [EditorField]
        private DropItems _dropItems1Star;

        [EditorField]
        private DropItems _dropItems2Star;

        [EditorField]
        private DropItems _dropItems3Star;

        public StoryMissionDropItems()
        {
        }

        public StoryMissionDropItems(DropItems dropItems1Star, DropItems dropItems2Star, DropItems dropItems3Star)
        {
            _dropItems1Star = dropItems1Star;
            _dropItems2Star = dropItems2Star;
            _dropItems3Star = dropItems3Star;
        }

        public DropItems DropItems1Star
        {
            get { return _dropItems1Star; }
        }

        public DropItems DropItems2Star
        {
            get { return _dropItems2Star; }
        }

        public DropItems DropItems3Star
        {
            get { return _dropItems3Star; }
        }

        public DropItems GetDropItemsByStar(byte star)
        {
            switch (star)
            {
                case 1: return _dropItems1Star;
                case 2: return _dropItems2Star;
                case 3: return _dropItems3Star;
                default: return null;
            }
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _dropItems1Star);
            dst.Add(ref _dropItems2Star);
            dst.Add(ref _dropItems3Star);

            return true;
        }
    }
}
