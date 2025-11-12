using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class QuestListItemDataDescription : DescriptionBase
    {
        public QuestListItemDataDescription()
        {
        }

        public QuestListItemDataDescription(
            short id,
            string tag,
            bool dailyQuest,
            int dailyResetHour,
            Quest[] quests)
        {
            Id = id;
            Tag = tag;
            DailyQuest = dailyQuest;
            DailyResetHour = dailyResetHour;
            Quests = quests;
        }

        [EditorField] public bool DailyQuest;
        
        [EditorField] public int DailyResetHour;

        [EditorField] public Quest[] Quests;

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref Quests);
            dst.Add(ref DailyQuest);
            dst.Add(ref DailyResetHour);
            return base.Serialize(dst);
        }

           public class Quest : DescriptionBase
        {
            public Quest()
            {
            }

            public Quest(
                short id,
                string tag,
                PlayerRequirement[] playerRequirements,
                AccumulatorRequirement[] accumulatorRequirements,
                DropItems dropItems,
                PlayerRequirement[] giveRequirements,
                int sortNumber,
                bool isAllQuestsCompletedQuest)
            {
                IsAllQuestsCompletedQuest = isAllQuestsCompletedQuest;
                Id = id;
                Tag = tag;
                PlayerRequirements = playerRequirements;
                AccumulatorRequirements = accumulatorRequirements;
                DropItems = dropItems;
                GiveRequirements = giveRequirements;
            }


            [EditorField] public PlayerRequirement[] PlayerRequirements;

            [EditorField] public AccumulatorRequirement[] AccumulatorRequirements;

            [EditorField] public DropItems DropItems;

            [EditorField] public PlayerRequirement[] GiveRequirements;

            [EditorField] public float Order;

            [EditorField] public QuestVisualDescription Visual;

            [EditorField] public bool IsAllQuestsCompletedQuest;

            public override bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref PlayerRequirements);
                dst.Add(ref AccumulatorRequirements);
                dst.Add(ref DropItems);
                dst.Add(ref GiveRequirements);
                dst.Add(ref Order);
                dst.Add(ref Visual);
                dst.Add(ref IsAllQuestsCompletedQuest);

                return base.Serialize(dst);
            }

            public class QuestVisualDescription : IDataStruct
            {
                [EditorField(EditorFieldParameter.LocalizedString)]
                public string Title;

                [EditorField(EditorFieldParameter.LocalizedString)]
                public string Description;

                [EditorField(EditorFieldParameter.Color32)]
                public uint IconBackColor;

                [EditorField(EditorFieldParameter.UnityTexture)]
                public string IconImage;

                [EditorField]
                public WhereToFindItem[] GoTo;

                public bool Serialize(IBinarySerializer dst)
                {
                    dst.Add(ref Title);
                    dst.Add(ref Description);
                    dst.Add(ref IconBackColor);
                    dst.Add(ref IconImage);
                    dst.Add(ref GoTo);

                    return true;
                }
            }
        }
    }
}