using System.Collections.Generic;
using Serializer.BinarySerializer;

namespace Shared
{
    namespace CommonData
    {
        public interface IAbilitySetData
        {
            IEnumerable<IAbilitySlotData> Slots { get; }
        }

        public class AbilitySetData : IDataStruct, IAbilitySetData
        {
            public AbilitySlotData[] Slots;

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref Slots);
                return true;
            }

            IEnumerable<IAbilitySlotData> IAbilitySetData.Slots { get { return Slots; } }
        }
    }
}
