using Serializer.BinarySerializer;

namespace Shared.CommonData.Plt
{
    public class SoftLaunchCompensationContainer : IDataStruct
    {
        public SoftLaunchCompensationContainer()
        {            
        }

        public SoftLaunchCompensationContainer(
            SoftLaunchCompensationBundlesDescription[] bundlesDescriptions,
            SoftLaunchCompensationLevelDescription[] levelDescriptions)
        {
            BundlesDescriptions = bundlesDescriptions;
            LevelDescriptions = levelDescriptions;
        }

        public SoftLaunchCompensationBundlesDescription[] BundlesDescriptions;
        public SoftLaunchCompensationLevelDescription[] LevelDescriptions;

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref BundlesDescriptions);
            dst.Add(ref LevelDescriptions);

            return true;
        }
    }
}
