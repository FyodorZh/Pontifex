using System.Text;
using Serializer.BinarySerializer;
using Shared.CommonData.Plt;

namespace Shared.NeoMeta.Items
{
    public partial class ContainersBuildingItem : BuildingItem, IWithStage
    {
        private ContainerPackClient[] _containerPackClients;

        public ContainerPackClient[] ContainerPackClients
        {
            get { return _containerPackClients; }
        }

        public ContainersBuildingItem()
        {
        }

        public ContainersBuildingItem(ID<Item> itemId, short descId, short grade, int state, int? upgradeEndTime, bool canSpeedup, short stageId, ContainerPackClient[] containerPackClients)
        : base(itemId, descId, grade, state, upgradeEndTime, canSpeedup)
        {
            StageId = stageId;
            _containerPackClients = containerPackClients;
        }

        public short StageId;

        short IWithStage.StageId
        {
            get { return StageId; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref StageId);
            dst.Add(ref _containerPackClients);

            return base.Serialize(dst);
        }

        public override byte ItemDescType
        {
            get { return ItemType.ContainersBuildingId; }
        }

        public override string ToString()
        {
            if (_containerPackClients == null)
            {
                return base.ToString();
            }

            var stringBuilder = new StringBuilder(base.ToString());
            stringBuilder.AppendFormat(", StageId={0}", StageId.ToString());
            stringBuilder.Append(", ContainerPackClients:");
            for (int i = 0, n = _containerPackClients != null ? _containerPackClients.Length : 0; i < n; i++)
            {
                stringBuilder.Append(" #").Append(i).Append(") ");
                stringBuilder.Append(_containerPackClients[i]);
            }

            return stringBuilder.ToString();
        }
    }
}
