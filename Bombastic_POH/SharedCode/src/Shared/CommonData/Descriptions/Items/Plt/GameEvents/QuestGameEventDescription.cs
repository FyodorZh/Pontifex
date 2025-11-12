using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt.GameEvents
{
    public class QuestGameEventDescription : GameEventDescription
    {
        public QuestGameEventDescription()
        {
        }

        public QuestGameEventDescription(
            PlayerRequirement[] requirements,
            PlayerRequirement[] showOnClientRequirements,
            DropItems[] dropOnActivate,
            AccumulatorRequirement[] deactivateRequirements,
            short[] takeOnDeactivate,
            ExternalDropItems[] externalDrop,
            short questListDescId,
            GameEventCompensationDescription[] compensations)
            : base(requirements, showOnClientRequirements, dropOnActivate, deactivateRequirements, takeOnDeactivate, compensations, externalDrop)
        {
            QuestListDescId = questListDescId;
        }

        public override ItemType ItemDescType2
        {
            get { return ItemType.QuestGameEvent; }
        }

        public override GameEventType EventType
        {
            get { return GameEventType.Quest; }
        }

        [EditorField, EditorLink("Items", "Items")]
        public short QuestListDescId;

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref QuestListDescId);
            
            return base.Serialize(dst);
        }
    }
}