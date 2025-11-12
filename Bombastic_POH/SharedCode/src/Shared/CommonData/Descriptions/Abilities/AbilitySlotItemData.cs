using Serializer.BinarySerializer;
using Serializer.Extensions;
using Shared.CommonData.Attributes;

namespace Shared
{
    namespace CommonData
    {
        public interface IAbilitySlotItemData
        {
            string Name { get; }
            int AbilityId { get; }
            DeltaTime ActiveTimeout { get; }
            string AbilityIcon { get; }
        }

        public class AbilitySlotItemData :  IAbilitySlotItemData, IDataStruct
        {
            public int AbilityId = -1;
            [EditorField]
            public string Name;
            [EditorField]
            public DeltaTime ActiveTimeout;
            [EditorField(EditorFieldParameter.UnityAsset)]
            public string AbilityIcon;

            public AbilitySlotItemData() { }

            public AbilitySlotItemData(
                int abilityId,
                string abilityName,
                DeltaTime activeTimeout,
                string abilityIcon)
            {
                AbilityId = abilityId;
                Name = abilityName;
                ActiveTimeout = activeTimeout;
                AbilityIcon = abilityIcon;
            }

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref AbilityId);
                dst.Add(ref Name);
                dst.AddDeltaTime(ref ActiveTimeout);
                dst.Add(ref AbilityIcon);

                return true;
            }

            int IAbilitySlotItemData.AbilityId { get { return AbilityId; } }
            string IAbilitySlotItemData.Name { get { return Name; } }
            Shared.DeltaTime IAbilitySlotItemData.ActiveTimeout { get { return ActiveTimeout; } }
            string IAbilitySlotItemData.AbilityIcon { get { return this.AbilityIcon; } }
        }
    }

}
