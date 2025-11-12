using System.Collections.Generic;
using Serializer.BinarySerializer;

namespace Shared.Protocol
{
    public class ResourcesList : IDataStruct
    {
        public class UnitResource : IDataStruct
        {
            private short mUnitId;
            private short mSkinId;
            private short mCount;

            public short UnitId { get { return mUnitId; } }
            public short SkinId { get { return mSkinId; } }
            public int Count { get { return mCount; } }

            public void AddCount(int count)
            {
                mCount += (short) count;
            }

            public UnitResource()
            {
            }

            public UnitResource(short unitId, short skinId, int count)
            {
                mUnitId = unitId;
                mSkinId = skinId;
                mCount = (short)count;
            }

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref mUnitId);
                dst.Add(ref mSkinId);
                dst.Add(ref mCount);
                return true;
            }
        }

        public class AbilityResource : IDataStruct
        {
            private ushort mResourceId;
            private short mCount;

            public ushort ResourceId { get { return mResourceId; } }
            public int Count { get { return mCount; } }

            public AbilityResource()
            {
            }

            public AbilityResource(ushort resourceId, int count)
            {
                mResourceId = resourceId;
                mCount = (short)count;
            }

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref mResourceId);
                dst.Add(ref mCount);
                return true;
            }
        }

        private UnitResource[] mUnitResources;
        private AbilityResource[] mAbilityResources;

        public static ResourcesList Empty
        {
            get { return new ResourcesList(new UnitResource[0], new AbilityResource[0]); }
        }

        public bool IsEmpty
        {
            get
            {
                return !(mUnitResources != null && mAbilityResources != null && mUnitResources.Length != 0 && mAbilityResources.Length != 0);
            }
        }

        public ResourcesList()
        {
        }

        public ResourcesList(UnitResource[] unitResources, AbilityResource[] abilityResources)
        {
            mUnitResources = unitResources;
            mAbilityResources = abilityResources;
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref mUnitResources);
            dst.Add(ref mAbilityResources);
            return true;
        }

        public IEnumerable<UnitResource> UnitResources
        {
            get { return mUnitResources; }
        }

        public IEnumerable<AbilityResource> AbilityResources
        {
            get { return mAbilityResources; }
        }
    }
}