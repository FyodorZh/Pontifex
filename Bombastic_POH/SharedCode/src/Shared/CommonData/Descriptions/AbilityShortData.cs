using Serializer.BinarySerializer;

namespace Shared
{
    namespace CommonData
    {
        public interface IAbilityShortData
        {
            int AbilityId { get; }
            string AbilityName { get; }
        }

        public class AbilityShortData : IAbilityShortData, IDataStruct
        {
            public int AbilityId;

            int IAbilityShortData.AbilityId
            {
                get { return AbilityId; }
            }

            public string AbilityName;

            string IAbilityShortData.AbilityName
            {
                get { return AbilityName; }
            }

            public virtual bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref AbilityId);
                dst.Add(ref AbilityName);

                return true;
            }
        }
    }
}
