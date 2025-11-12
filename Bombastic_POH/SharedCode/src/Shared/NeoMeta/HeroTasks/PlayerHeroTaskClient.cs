using Serializer.BinarySerializer;
using Serializer.Extensions;
using Serializer.Extensions.ID;
using Shared.CommonData.Plt;
using Shared.CommonData.Plt.HeroTasks;

namespace Shared.NeoMeta.HeroTasks
{
//    public struct HeroTaskId
//    {
//        public HeroTaskId(int value)
//        {
//            Value = value;
//        }
//
//        public int Value;
//
//        public bool Equals(HeroTaskId other)
//        {
//            return Value == other.Value;
//        }
//
//        public override bool Equals(object obj)
//        {
//            if (ReferenceEquals(null, obj)) return false;
//            return obj is HeroTaskId && Equals((HeroTaskId) obj);
//        }
//
//        public override int GetHashCode()
//        {
//            return Value;
//        }
//    }

    public interface IHeroTask {}
    
    public partial class PlayerHeroTaskClient : IDataStruct
    {
        private string _tag;
        private string _name;
        private string _bannerImageUrl;
        private long _duration;
        private float _powerRequirement;
        private HeroSlotClient[] _heroTaskSlots;
        private GeneratedDropItems _dropItems;
        private byte _playerHeroTaskState;
        private int? _executingEndDate;

        public PlayerHeroTaskClient()
        {
        }

        public PlayerHeroTaskClient(
            ID<IHeroTask> taskId,
            string tag,
            string name,
            string bannerImageUrl,
            long duration,
            float powerRequirement,
            HeroSlotClient[] heroTaskSlots,
            GeneratedDropItems dropItems,
            PlayerHeroTaskState playerHeroTaskState,
            int? executingEndDate)
        {
            TaskId = taskId;
            _tag = tag;
            _playerHeroTaskState = (byte)playerHeroTaskState;
            _name = name;
            _bannerImageUrl = bannerImageUrl;
            _duration = duration;
            _powerRequirement = powerRequirement;
            _heroTaskSlots = heroTaskSlots;
            _dropItems = dropItems;
            _executingEndDate = executingEndDate;
        }

        public ID<IHeroTask> TaskId;

        public string Tag
        {
            get { return _tag; }
        }

        public string Name
        {
            get { return _name; }
        }

        public string BannerImageUrl
        {
            get { return _bannerImageUrl; }
        }

        public long DurationSeconds
        {
            get { return _duration; }
        }

        public float PowerRequirement
        {
            get { return _powerRequirement; }
        }

        public GeneratedDropItems DropItems
        {
            get { return _dropItems; }
        }

        public HeroSlotClient[] HeroTaskSlots
        {
            get { return _heroTaskSlots; }
        }

        public PlayerHeroTaskState PlayerHeroTaskState
        {
            get { return (PlayerHeroTaskState)_playerHeroTaskState; }
        }

        public long? ExecutingEndDate
        {
            get { return _executingEndDate; }
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.AddId(ref TaskId);
            dst.Add(ref _tag);
            dst.Add(ref _name);
            dst.Add(ref _bannerImageUrl);
            dst.Add(ref _duration);
            dst.Add(ref _powerRequirement);
            dst.Add(ref _heroTaskSlots);
            dst.Add(ref _dropItems);
            dst.Add(ref _playerHeroTaskState);
            dst.AddNullable(ref _executingEndDate);

            return true;
        }
    }
}
