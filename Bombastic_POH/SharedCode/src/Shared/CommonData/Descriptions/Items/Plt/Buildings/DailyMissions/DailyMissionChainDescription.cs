using System;
using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt.DailyMissions
{
    public class DailyMissionChainDescription : DescriptionBase
    {
        public DailyMissionChainDescription(
            short id,
            string tag,
            MissionSettings[] missions, 
            int rollWeight,
            short missionTypeDescId,
            PlayerRequirement[] rollRequirements,
            Requirement[] heroRequirements,
            string name,
            string description)
        {
            Id = id;
            Tag = tag;
            _missions = missions;
            _rollWeight = rollWeight;
            _missionTypeDescId = missionTypeDescId;
            _rollRequirements = rollRequirements;
            _heroRequirements = heroRequirements;
            _name = name;
            _description = description;
        }

        public DailyMissionChainDescription()
        {    
        }

        [EditorField]
        private MissionSettings[] _missions;

        [EditorField]
        private int _rollWeight;

        [EditorField, EditorLink("Items", "Daily Mission Types")]
        private short _missionTypeDescId;

        [EditorField]
        private PlayerRequirement[] _rollRequirements;

        [EditorField] private Requirement[] _heroRequirements;

        [EditorField]
        private string _name;

        [EditorField]
        private string _description;

        public string Name { get { return _name; } }

        public string Description { get { return _description; } }

        public MissionSettings[] Missions
        {
            get { return _missions; }
        }

        public int RollWeight
        {
            get { return _rollWeight; }
        }

        public PlayerRequirement[] RollRequirements
        {
            get { return _rollRequirements; }
        }

        public short MissionType
        {
            get { return _missionTypeDescId; }
        }

        public Requirement[] HeroRequirements
        {
            get { return _heroRequirements; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _missions);
            dst.Add(ref _rollWeight);
            dst.Add(ref _rollRequirements);
            dst.Add(ref _missionTypeDescId);
            dst.Add(ref _heroRequirements);
            dst.Add(ref _name);
            dst.Add(ref _description);

            return base.Serialize(dst);
        }

        public class MissionSettings : IDataStruct
        {
            public MissionSettings(FullCompletedNumberSettings[] chainFullCompletedNumberSettings)
            {
                _chainFullCompletedNumberSettings = chainFullCompletedNumberSettings;
            }

            public MissionSettings()
            {                
            }

            [EditorField("Номера прохождения")]
            private FullCompletedNumberSettings[] _chainFullCompletedNumberSettings;

            public FullCompletedNumberSettings[] ChainFullCompletedNumberSettings
            {
                get { return _chainFullCompletedNumberSettings; }
            }

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref _chainFullCompletedNumberSettings);

                return true;
            }

            public class FullCompletedNumberSettings : IDataStruct
            {
                public FullCompletedNumberSettings(int power, DropItems missionCompleteDrop)
                {
                    _power = power;
                    _missionCompleteDrop = missionCompleteDrop;
                }

                public FullCompletedNumberSettings()
                {                    
                }
                
                [EditorField]
                private int _power;

                [EditorField]
                private DropItems _missionCompleteDrop;

                public int Power
                {
                    get { return _power; }
                }

                public DropItems MissionCompleteDrop
                {
                    get { return _missionCompleteDrop; }
                }

                public bool Serialize(IBinarySerializer dst)
                {
                    dst.Add(ref _power);
                    dst.Add(ref _missionCompleteDrop);

                    return true;
                }
            }
        }
    }
}
