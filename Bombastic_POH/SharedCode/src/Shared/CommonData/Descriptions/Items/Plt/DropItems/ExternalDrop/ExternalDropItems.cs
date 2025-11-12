using System;
using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class ExternalDropUnit : IDataStruct
    {
        public ExternalDropUnit()
        {
        }

        public ExternalDropUnit(short unitDescId, short skinDescId, int templateStrategyId)
        {
            UnitDescId = unitDescId;
            SkinDescId = skinDescId;
            TemplateStrategyId = templateStrategyId;
        }

        [EditorField]
        public short UnitDescId;

        [EditorField]
        public short SkinDescId;

        [EditorField]
        public int TemplateStrategyId;

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref UnitDescId);
            dst.Add(ref SkinDescId);
            dst.Add(ref TemplateStrategyId);

            return true;
        }
    }
    
    public class ExternalDropItems : IDataStruct
    {
        public ExternalDropItems()
        {
        }

        public ExternalDropItems(DropItems dropItems, PlayerRequirement[] requirements, ExternalDropSource[] dropSources, ExternalDropUnit[] dropUnits)
        {
            DropItems = dropItems;
            Requirements = requirements;
            DropSources = dropSources;
            DropUnits = dropUnits;
        }

        [EditorField]
        public DropItems DropItems;

        [EditorField]
        public PlayerRequirement[] Requirements;

        [EditorField]
        public ExternalDropSource[] DropSources;

        [EditorField]
        public ExternalDropUnit[] DropUnits;
        
        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref DropItems);
            dst.Add(ref Requirements);
            dst.Add(ref DropSources);
            dst.Add(ref DropUnits);

            return true;
        }
    }
}