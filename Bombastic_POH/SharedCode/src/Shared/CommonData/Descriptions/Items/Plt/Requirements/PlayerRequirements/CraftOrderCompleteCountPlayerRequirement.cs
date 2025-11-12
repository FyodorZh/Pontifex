using System;
using System.Collections.Generic;
using System.Text;
using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class CraftOrderCompleteCountPlayerRequirement : PlayerRequirement
    {
        public CraftOrderCompleteCountPlayerRequirement()
        {

        }

        public CraftOrderCompleteCountPlayerRequirement(RequirementOperation operation)
            : base(operation)
        {

        }

        [EditorField]
        public short OrderDescId;

        [EditorField]
        public int Count;

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref OrderDescId);
            dst.Add(ref Count);

            return base.Serialize(dst);
        }
    }
}
