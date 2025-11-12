using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class HeroClassItemRequirement : ItemRequirement
    {
        [EditorField, EditorLink("Items", "Heroes Classes")]
        private short _heroClassDescId;

        public HeroClassItemRequirement()
        {
        }

        public HeroClassItemRequirement(RequirementOperation operation, short heroClassDescId)
            : base(operation)
        {
            _heroClassDescId = heroClassDescId;
        }

        public short HeroClassDescriptionId
        {
            get { return _heroClassDescId; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _heroClassDescId);

            return base.Serialize(dst);
        }
    }
}
