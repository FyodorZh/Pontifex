using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class ConditionalDropItems : IDataStruct
    {
        public class Condition : IDataStruct
        {
            [EditorField]
            public Requirement[] Requirements;

            [EditorField]
            public DropItems DropItems;

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref Requirements);
                dst.Add(ref DropItems);

                return true;
            }
        }

        [EditorField]
        public Condition[] Conditions;

        [EditorField]
        public DropItems DefaultDropItems;

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref Conditions);
            dst.Add(ref DefaultDropItems);

            return true;
        }
    }
}