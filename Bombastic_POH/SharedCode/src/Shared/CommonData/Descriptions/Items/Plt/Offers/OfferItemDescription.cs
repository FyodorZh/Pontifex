using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt.Offers
{
    public enum OfferItemActivationType
    {
        ByEvents = 0,
        FromDropItems = 1
    }

    public class PositionAndSize : IDataStruct
    {
        [EditorField]
        public int targetIndex;

        [EditorField]
        public int buttonPosX;

        [EditorField]
        public int buttonPosY;

        [EditorField]
        public int buttonSizeX;

        [EditorField]
        public int buttonSizeY;

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref targetIndex);
            dst.Add(ref buttonPosX);
            dst.Add(ref buttonPosY);
            dst.Add(ref buttonSizeX);
            dst.Add(ref buttonSizeY);

            return true;
        }
    }

    public class InteractablePicture : IDataStruct
    {
        [EditorField(EditorFieldParameter.UnityTexture)]
        public string picture;

        [EditorField]
        public PositionAndSize[] buttons;

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref picture);
            dst.Add(ref buttons);

            return true;
        }
    }

    public class OfferItemDescription : ItemBaseDescription,
        ICanHaveInstances
    {
        [EditorField, EditorLink("Store", "In Apps")]
        private short _inAppDescId;

        [EditorField]
        private int _delay;

        [EditorField]
        private int _expireTime;

        [EditorField]
        private Requirement[] _requirements;

        [EditorField]
        private bool _enabled;

        [EditorField]
        private OfferItemActivationType _activationType;

        [EditorField]
        private InteractablePicture[] _pictures;

        [EditorField("Discount = (fakePrice - realPrice) / fakePrice")]
        private float _discount;

        public override ItemType ItemDescType2
        {
            get { return ItemType.Offer; }
        }

        public short InAppDescId
        {
            get { return _inAppDescId; }
        }

        public System.TimeSpan Delay
        {
            get { return System.TimeSpan.FromSeconds(_delay); }
        }

        public System.TimeSpan ExpireTime
        {
            get { return System.TimeSpan.FromSeconds(_expireTime); }
        }

        public Requirement[] Requirements
        {
            get { return _requirements; }
        }

        public bool Enabled
        {
            get { return _enabled; }
        }

        public OfferItemActivationType ActivationType
        {
            get { return _activationType; }
        }

        public bool HasDelay
        {
            get { return _delay > 0; }
        }

        public bool HasExpiration
        {
            get { return _expireTime > 0; }
        }

        public InteractablePicture[] Pictures
        {
            get { return _pictures; }
        }

        public float Discount
        {
            get { return _discount; }
        }

//        public override bool CanHaveInstances
//        {
//            get { return true; }
//        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _inAppDescId);
            dst.Add(ref _delay);
            dst.Add(ref _expireTime);
            dst.Add(ref _requirements);
            dst.Add(ref _enabled);
            dst.Add(ref _pictures);
            dst.Add(ref _discount);

            var atTmp = (byte)_activationType;
            dst.Add(ref atTmp);

            if (dst.isReader)
            {
                _activationType = (OfferItemActivationType)atTmp;
            }

            return base.Serialize(dst);
        }
    }
}
