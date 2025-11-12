using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class ConditionContainerItemDescription : BaseContainerItemDescription
    {
        [EditorField] private ContainerCondition[] _containerConditions;
        [EditorField] private bool _showContent;

        public override ItemType ItemDescType2
        {
            get { return ItemType.ConditionContainer; }
        }

        public ContainerCondition[] Conditions
        {
            get { return _containerConditions; }
        }

        public bool ShowContent
        {
            get { return _showContent; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _containerConditions);
            dst.Add(ref _showContent);
            return base.Serialize(dst);
        }
    }
}
