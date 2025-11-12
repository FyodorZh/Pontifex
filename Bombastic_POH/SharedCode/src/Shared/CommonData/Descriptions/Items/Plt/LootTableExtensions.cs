using System;
using System.Collections.Generic;

namespace Shared.CommonData.Plt
{
    public static class LootTableExtensions
    {
        public static List<DropItem> GenerateItems(this LootTable[] lootTables, IRandom random)
        {
            if (lootTables == null || lootTables.Length == 0)
            {
                return new List<DropItem>(0);
            }

            var results = new List<DropItem>(lootTables.Length);
            for (int index = 0; index < lootTables.Length; ++index)
            {
                var item = GenerateItem(lootTables[index], random);
                if (item != null)
                {
                    results.Add(item);
                }
            }

            return results;
        }

        public static DropItem GenerateItem(this LootTable lootTable, IRandom random)
        {
            var totalWeight = CalculateTotalWeight(lootTable.Items);
            if (totalWeight <= 0)
            {
                return null;
            }

            var rndWeight = random.Next(totalWeight);
            var currWeight = 0;

            for (int index = 0; index < lootTable.Items.Length; ++index)
            {
                var eachLootItem = lootTable.Items[index];

                currWeight += eachLootItem.Weight;

                if (rndWeight < currWeight)
                {
                    var count = GetItemCount(eachLootItem, random);
                    if (count != 0)
                    {
                        return new DropItem(eachLootItem.ItemDescId, count, eachLootItem.DropItemParams);
                    }
                }
            }

            return null;
        }

        private static int CalculateTotalWeight(LootItem[] items)
        {
            int sum = 0;
            for (int index = 0; index < items.Length; ++index)
            {
                sum += items[index].Weight;
            }

            return sum;
        }

        private static int GetItemCount(LootItem lootItem, IRandom random)
        {
            if (lootItem.MaxCount <= 0 || lootItem.MaxCount < lootItem.MinCount)
            {
                return 0;
            }

            if (lootItem.MaxCount == lootItem.MinCount)
            {
                return lootItem.MaxCount;
            }

            var min = Math.Max(1, lootItem.MinCount);
            return random.Next(min, lootItem.MaxCount + 1);
        }
    }
}
