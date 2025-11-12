using Serializer.BinarySerializer;

namespace Shared.CommonData.Plt.HeroTasks
{
    public class HeroTasksDataContainer : IDataStruct
    {
        private HeroTaskDescription[] _heroTaskDescriptions;

        public HeroTasksDataContainer()
        {
        }

        public HeroTasksDataContainer(HeroTaskDescription[] heroTaskDescriptions)
        {
            _heroTaskDescriptions = heroTaskDescriptions;
        }

        public HeroTaskDescription[] HeroTaskDescriptions
        {
            get { return _heroTaskDescriptions; }
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _heroTaskDescriptions);

            return true;
        }
    }
}