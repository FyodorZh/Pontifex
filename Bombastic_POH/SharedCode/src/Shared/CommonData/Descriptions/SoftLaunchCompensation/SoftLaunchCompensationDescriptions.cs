using Serializer.Tools;

namespace Shared.CommonData.Plt
{
    public interface ISoftLaunchCompensationDescriptions
    {
        SoftLaunchCompensationBundlesDescription[] BundlesDescriptions { get; }

        SoftLaunchCompensationLevelDescription[] LevelDescriptions { get; }
    }

    public class SoftLaunchCompensationDescriptions : PlatformerDataContainerDescriptions<SoftLaunchCompensationContainer>, ISoftLaunchCompensationDescriptions
    {
        protected override string FileName
        {
            get { return PlatformerFileDataConstants.SOFT_LAUNCH_COMPENSATION; }
        }

        public SoftLaunchCompensationBundlesDescription[] BundlesDescriptions { get; private set; }
        public SoftLaunchCompensationLevelDescription[] LevelDescriptions { get; private set; }

        public override void InitFromContainer(SoftLaunchCompensationContainer container)
        {
            Container = container;
            BundlesDescriptions = Container.BundlesDescriptions;
            LevelDescriptions = Container.LevelDescriptions;
        }
    }
}
