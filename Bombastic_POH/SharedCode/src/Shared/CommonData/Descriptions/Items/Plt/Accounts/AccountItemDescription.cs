using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class AccountItemDescription : ItemBaseDescription
        , IWithLevels
    {
        public AccountItemDescription(bool autoLevelUp, ItemLevel[] levels, short tutorialNotificationMaxLevel, short tutorialNotificationTimeMinutes)
        {
            _autoLevelUp = autoLevelUp;
            _levels = levels;
            _tutorialNotificationMaxLevel = tutorialNotificationMaxLevel;
            _tutorialNotificationTimeMinutes = tutorialNotificationTimeMinutes;
        }

        public AccountItemDescription()
        {            
        }

        [EditorField]
        private bool _autoLevelUp;

        [EditorField]
        private ItemLevel[] _levels;

        [EditorField]
        private short _tutorialNotificationMaxLevel;

        [EditorField]
        private short _tutorialNotificationTimeMinutes;

        public override ItemType ItemDescType2
        {
            get { return ItemType.Account; }
        }

        public bool AutoLevelUp
        {
            get { return _autoLevelUp; }
        }

        public short TutorialNotificationMaxLevel
        {
            get { return _tutorialNotificationMaxLevel; }
        }

        public short TutorialNotificationTimeMinutes
        {
            get { return _tutorialNotificationTimeMinutes; }
        }

        public ItemLevel[] Levels
        {
            get { return _levels; }
            set { _levels = value; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _autoLevelUp);
            dst.Add(ref _levels);
            dst.Add(ref _tutorialNotificationMaxLevel);
            dst.Add(ref _tutorialNotificationTimeMinutes);

            return base.Serialize(dst);
        }
    }
}
