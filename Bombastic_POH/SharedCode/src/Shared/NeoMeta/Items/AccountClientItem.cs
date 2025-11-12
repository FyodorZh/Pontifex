using System.Collections.Generic;
using Serializer.BinarySerializer;
using Serializer.Extensions;
using Shared.CommonData.Plt;

namespace Shared.NeoMeta.Items
{
    public partial class AccountClientItem : Item
        , IWithLevel
    {
        public AccountClientItem()
        {
        }

        public AccountClientItem(ID<Item> itemId, short descId, int state, short level, int? upgradeEndTime, string name, bool tutorialCompleted, int renamesCount, short[] completedTutorialStages)
            : base(itemId, descId)
        {
            State = state;
            Level = level;
            UpgradeEndTime = upgradeEndTime;
            Name = name;
            TutorialCompleted = tutorialCompleted;
            RenamesCount = renamesCount;
            CompletedTutorialStages = completedTutorialStages;
        }

        public override byte ItemDescType
        {
            get { return ItemType.AccountId; }
        }

        public int State;

        public short Level;

        public int? UpgradeEndTime;

        public string Name;

        public bool TutorialCompleted;

        public int RenamesCount;


        public short[] CompletedTutorialStages;

        short IWithLevel.Level
        {
            get { return Level; }
        }

        int? IWithLevel.UpgradeEndTime
        {
            get { return UpgradeEndTime; }
        }

        public bool IsLevelingUp
        {
            get { return State.HasState(HeroItemState.LevelingUp); }
        }

        public bool IsWaitingLevelUpCollect
        {
            get { return State.HasState(HeroItemState.WaitingForCollectLevelUp); }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref State);
            dst.Add(ref Level);
            dst.AddNullable(ref UpgradeEndTime);
            dst.Add(ref Name);
            dst.Add(ref TutorialCompleted);
            dst.Add(ref RenamesCount);
            dst.Add(ref CompletedTutorialStages);

            return base.Serialize(dst);
        }

        public override string ToString()
        {
            return string.Format("{0}, Name: '{1}', RenamesCount: {6}, Level: {2}, TutorialCompleted: {7}, UpgradeEndTime: {3}, IsLevelingUp: {4}, IsWaitingLevelUpCollect: {5}", base.ToString(), Name, Level, UpgradeEndTime ?? -1, IsLevelingUp, IsWaitingLevelUpCollect, RenamesCount, TutorialCompleted);
        }
    }
}
