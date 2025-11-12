using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt.GameEvents
{
    public class CoopGameEventDescription : GameEventDescription
    {
        public override ItemType ItemDescType2
        {
            get { return ItemType.CoopGameEvent; }
        }

        public override GameEventType EventType
        {
            get { return GameEventType.Coop; }
        }

        [EditorField] public MapDescription[] MapsData;

        [EditorField] public byte MapChangeHour;

        [EditorField] public BucketDescription[] BucketDescriptions;

        [EditorField] public DropItems WinDrop;
        
        [EditorField] public DropItems LoseDrop;

        [EditorField] public RewardDescription[] MapRewards;
        
        [EditorField, EditorLink("Items", "Items")] 
        public short StarItem;

        [EditorField] public BuyCount[] RevivePrices;

        public class BuyCount : IDataStruct
        {
            public BuyCount()
            {
            }
            
            public BuyCount(int minCount, Price price)
            {
                MinCount = minCount;
                Price = price;
            }

            [EditorField] public int MinCount;

            [EditorField] public Price Price;

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref MinCount);
                dst.Add(ref Price);

                return true;
            }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref MapsData);
            dst.Add(ref MapChangeHour);
            dst.Add(ref BucketDescriptions);
            dst.Add(ref WinDrop);
            dst.Add(ref LoseDrop);
            dst.Add(ref MapRewards);
            dst.Add(ref StarItem);
            dst.Add(ref RevivePrices);
            return base.Serialize(dst);
        }

        public class MapDescription : DescriptionBase
        {
            public MapDescription()
            {
            }
            
            public MapDescription(short mapId, string tag, string mapGuid, string name, string icon, float order)
            {
                Id = mapId;
                Tag = tag;
                MapGuid = mapGuid;
                Name = name;
                Icon = icon;
                Order = order;
            }

            [EditorField]
            public string MapGuid;

            [EditorField(EditorFieldParameter.LocalizedString)]
            public string Name;

            [EditorField(EditorFieldParameter.UnityTexture)]
            public string Icon;

            [EditorField]
            public DropItem[] FakeDropItems;

            [EditorField]
            public float Order;

            public override bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref MapGuid);
                dst.Add(ref Name);
                dst.Add(ref Icon);
                dst.Add(ref FakeDropItems);
                dst.Add(ref Order);
                return base.Serialize(dst);
            }
        }

        public class BucketDescription : DescriptionBase
        {
            public BucketDescription()
            {
            }
            
            public BucketDescription(int fromMaxLevel, int mapPower)
            {
                FromMaxLevel = fromMaxLevel;
                MapPower = mapPower;
            }

            [EditorField] public int FromMaxLevel;

            [EditorField] public int MapPower;

            public override bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref FromMaxLevel);
                dst.Add(ref MapPower);

                return base.Serialize(dst);
            }
        }
        
        public class RewardDescription : DescriptionBase
        {
            public RewardDescription()
            {
            }
            
            public RewardDescription(short id, int minStars, DropItems drop)
            {
                Id = id;
                MinStars = minStars;
                Drop = drop;
            }

            [EditorField] public int MinStars;

            [EditorField] public DropItems Drop;

            [EditorField] public FakeDrop[] FakeDrop;

            public override bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref MinStars);
                dst.Add(ref Drop);
                dst.Add(ref FakeDrop);
                return base.Serialize(dst);
            }
        }
    }
}