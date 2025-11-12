using Serializer.BinarySerializer;
using Serializer.Extensions;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class ContainersPackDescription : DescriptionBase
    {
        public ContainersPackDescription()
        {
        }

        public ContainersPackDescription(int freeItemInterval, short? freeItemId, short oneStoreItem, short manyStoreItem, short[] groupedContainers)
        {
            _freeItemInterval = freeItemInterval;
            _freeItemId = freeItemId;
            _oneStoreItem = oneStoreItem;
            _manyStoreItem = manyStoreItem;
            _groupedContainers = groupedContainers;
        }

        [EditorField]
        private string _name;

        [EditorField(EditorFieldParameter.UnityTexture)]
        private string _icon;

        [EditorField]
        private bool _showDescription;

        [EditorField(EditorFieldParameter.LocalizedString)]
        private string _description;

        [EditorField]
        private bool _freeItemAvailableOnStart;

        [EditorField]
        private int _freeItemInterval;

        [EditorField, EditorLink("Items", "Items")]
        private short? _freeItemId;

        [EditorField, EditorLink("Store", "Store")]
        private short? _oneStoreItem;

        [EditorField, EditorLink("Store", "Store")]
        private short? _manyStoreItem;

        [EditorField, EditorLink("Items", "Items")]
        private short[] _groupedContainers;

        [EditorField, EditorLink("Items", "Items")]
        private short[] _fakeDropItems;

        [EditorField]
        private byte _multiplyDropPercents;

        public string Name
        {
            get { return _name; }
        }

        public string Icon
        {
            get { return _icon; }
        }

        public string Description
        {
            get { return _showDescription ? _description : null; }
        }

        public bool FreeItemAvailableOnStart
        {
            get { return _freeItemAvailableOnStart; }
        }

        public int FreeItemInterval
        {
            get { return _freeItemInterval; }
        }

        public short? FreeItemId
        {
            get { return _freeItemId; }
        }

        public short? OneStoreItem
        {
            get { return _oneStoreItem; }
        }

        public short? ManyStoreItem
        {
            get { return _manyStoreItem; }
        }

        public short[] GroupedContainers
        {
            get { return _groupedContainers; }
        }

        public short[] FakeDropItems
        {
            get { return _fakeDropItems; }
        }

        public byte MultiplyDropPercents
        {
            get { return _multiplyDropPercents; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _name);
            dst.Add(ref _icon);
            dst.Add(ref _showDescription);
            dst.Add(ref _freeItemAvailableOnStart);
            dst.Add(ref _freeItemInterval);
            dst.AddNullable(ref _freeItemId);
            dst.AddNullable(ref _oneStoreItem);
            dst.AddNullable(ref _manyStoreItem);
            dst.Add(ref _groupedContainers);
            dst.Add(ref _fakeDropItems);
            dst.Add(ref _multiplyDropPercents);

            if (_showDescription)
            {
                dst.Add(ref _description);
            }

            return base.Serialize(dst);
        }
    }
}
