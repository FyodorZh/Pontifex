using Serializer.BinarySerializer;

namespace Shared
{
    namespace CommonData
    {
        public interface IAbilityLevelData : IAbilityShortData
        {
            short StartLevel { get; }
            short StopLevel { get; }
        }

        public class AbilityLevelData : AbilityShortData, IAbilityLevelData
        {
            public short StartLevel;
            public short StopLevel;

            short IAbilityLevelData.StartLevel { get { return StartLevel; } }
            short IAbilityLevelData.StopLevel { get { return StopLevel; } }

            public override bool Serialize(IBinarySerializer dst)
            {
                base.Serialize(dst);

                dst.Add(ref StartLevel);
                dst.Add(ref StopLevel);

                return true;
            }
        }
    }
}
