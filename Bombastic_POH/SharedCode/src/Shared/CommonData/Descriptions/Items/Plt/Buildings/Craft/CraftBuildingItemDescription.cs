using System;
using System.Collections.Generic;
using System.Text;
using Serializer.BinarySerializer;
using Serializer.Extensions;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class CraftBuildingItemLevel : BuildingItemLevel
    {
        [EditorField, EditorLink("Items", "Craft Building Orders")]
        public short? OrderId;

        public CraftOrderDescription[] Orders;

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.AddNullable(ref OrderId);
            return base.Serialize(dst);
        }
    }

    public class CraftBuildingOrders : DescriptionBase
    {
        [EditorField]
        public CraftOrderDescription[] Orders;

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref Orders);
            return base.Serialize(dst);
        }
    }

    public class CraftBuildingItemDescription : BuildingItemDescription, IWithStages
    {
        public override ItemType ItemDescType2
        {
            get { return ItemType.CraftBuilding; }
        }

        [EditorField]
        public CraftBuildingItemLevel[] CraftBuildingGrades;

        [EditorField]
        public CraftBuildingItemStageDescription[] Stages;

        [EditorField]
        public short StartStageId;

        public override BuildingItemLevel[] Grades
        {
            get { return CraftBuildingGrades; }
        }

        short IWithStages.StartStageId
        {
            get { return StartStageId; }
        }

        StageDescription[] IWithStages.Stages
        {
            get { return Stages; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref CraftBuildingGrades);
            dst.Add(ref Stages);
            dst.Add(ref StartStageId);

            return base.Serialize(dst);
        }

        public override void OnPostprocess(ItemsDescriptions itemsDescriptions)
        {
            if (CraftBuildingGrades != null && CraftBuildingGrades.Length > 0)
            {
                for (int i = 0, cnt = CraftBuildingGrades.Length; i < cnt; i++)
                {
                    var level = CraftBuildingGrades[i];
                    if (level.OrderId.HasValue)
                    {
                        short orderId = level.OrderId.Value;
                        CraftBuildingOrders craftBuildingOrders;
                        if (itemsDescriptions.CraftBuildingOrders.TryGetValue(orderId, out craftBuildingOrders))
                        {
                            level.Orders = craftBuildingOrders.Orders;
                        }
                    }
                }
            }
        }
    }
}
