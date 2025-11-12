using System;
using System.Collections.Generic;
using System.Reflection;

namespace Shared.Pool
{
    public static class PoolHelper
    {
        public struct PoolStatData
        {
            public string PoolType;

            public int PeakUsedCount;
            public int UsingCount;
            public int TotalAllocatedCount;
            public int FreeObjectsCount;
        }

        public static List<PoolStatData> GetPoolsUsageStatistics(bool logStat)
        {
            List<PoolStatData> poolData = new List<PoolStatData>();

            foreach (var singleton in ThreadSingletonContext.ActiveInstance)
            {
                IPoolStatistics stat = singleton as IPoolStatistics;
                if (stat == null)
                {
                    continue;
                }

                Type singletonType = singleton.GetType();
                if (logStat)
                {
                    Log.d("Peak: {0}, Using: {1}, Free: {2}, Total: {3}\n{4}", stat.PeakUsedCount,
                        stat.UsingCount, stat.FreeObjectsCount, stat.TotalAllocatedCount, singletonType);
                }

                PoolStatData data = new PoolStatData();
                data.PoolType = singletonType.FullName;

                data.PeakUsedCount = stat.PeakUsedCount;
                data.UsingCount = stat.UsingCount;
                data.TotalAllocatedCount = stat.TotalAllocatedCount;
                data.FreeObjectsCount = stat.FreeObjectsCount;

                poolData.Add(data);
            }

            return poolData;
        }

        public static void ClearPoolsUsageStatistics()
        {
            foreach (var singleton in ThreadSingletonContext.ActiveInstance)
            {
                IPoolStatistics stat = singleton as IPoolStatistics;
                if (stat != null)
                {
                    stat.ClearStatistics();
                }
            }
        }

        public static void TryWarmUpPool(string poolTypeName, int count)
        {
            if (string.IsNullOrEmpty(poolTypeName))
            {
                return;
            }

            Type poolType = Type.GetType(poolTypeName);
            if (poolType == null)
            {
                Log.e("Type {0} not found", poolTypeName);
                return;
            }

            TryWarmUpPool(poolType, count);
        }

        public static void TryWarmUpPool(Type poolType, int count)
        {
            var precacheMethod = poolType.GetMethod(
                "Precache",
                BindingFlags.Public |
                BindingFlags.Static);

            if (precacheMethod != null)
            {
                precacheMethod.Invoke(null, new object[] { count });
            }
        }
    }
}
