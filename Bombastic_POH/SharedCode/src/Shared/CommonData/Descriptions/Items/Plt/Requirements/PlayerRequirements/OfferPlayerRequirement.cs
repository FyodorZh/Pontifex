using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;
using Shared.CommonData.Plt.Offers;

namespace Shared.CommonData.Plt
{
    public class OfferPlayerRequirement : PlayerRequirement
    {
        [EditorField, EditorLink("Items", "Items")]
        private short _offerItemDescId;

        [EditorField]
        private short _offerTypeId;

        [EditorField]
        private OfferStatus _status;

        public OfferPlayerRequirement()
        {
        }

        public OfferPlayerRequirement(RequirementOperation operation, short offerTypeId, OfferStatus status)
            : base(operation)
        {
            _offerTypeId = offerTypeId;
            _status = status;
        }

        public short OfferItemDescId
        {
            get { return _offerItemDescId; }
        }

        public short OfferTypeId
        {
            get { return _offerTypeId; }
        }

        public OfferStatus Status
        {
            get { return _status; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _offerItemDescId);
            dst.Add(ref _offerTypeId);

            var statusTmp = (byte)_status;
            dst.Add(ref statusTmp);

            if (dst.isReader)
            {
                _status = (OfferStatus)statusTmp;
            }

            return base.Serialize(dst);
        }
    }
}