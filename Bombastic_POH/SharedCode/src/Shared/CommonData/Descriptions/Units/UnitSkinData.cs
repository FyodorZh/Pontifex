using System.Collections.Generic;
using Serializer.BinarySerializer;

namespace Shared
{
    namespace CommonData
    {
        public enum UnitSkinDataKind
        {
            Model = 0,
            LobbyModel,
            Icon64x64,
            Icon116x116,
            Icon220x220,
            IconFullPortrait
        }

        public interface IUnitSkinSetData
        {
            int SkinCount { get; }
            IEnumerable<UnitSkinData> Skins { get; }

            bool IsSkinExists(short skinId);
            string GetName(short skinId);
            short GetId(string skinName);
            string GetData(UnitSkinDataKind dataType, short skinId = 0, bool defaultIfNotExist = true);
        }

        public class UnitSkinData : IDataStruct
        {
            private static readonly int mDataTypesNumber = System.Enum.GetValues(typeof(UnitSkinDataKind)).Length;

            private short mSkinId;
            private string mSkinName;
            private string[] mDataList = new string[mDataTypesNumber];

            public UnitSkinData()
            {
            }

            public UnitSkinData(short skinId, string skinName)
            {
                mSkinId = skinId;
                mSkinName = skinName;
            }

            public void SetData(UnitSkinDataKind dataType, string data)
            {
                mDataList[(int)dataType] = data;
            }

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref mSkinId);
                dst.Add(ref mSkinName);
                dst.Add(ref mDataList);
                DBG.Diagnostics.Assert(mDataList.Length == mDataTypesNumber);
                return true;
            }

            public short SkinId
            {
                get { return mSkinId; }
            }

            public string SkinName 
            {
                get { return mSkinName; }
            }

            public string GetData(UnitSkinDataKind dataType)
            {
                return mDataList[(int)dataType];
            }
        }

        public class UnitSkinSetData : IUnitSkinSetData, IDataStruct
        {
            private UnitSkinData[] mSkins;

            public UnitSkinSetData()
            {
            }

            public UnitSkinSetData(UnitSkinData[] skins)
            {
                mSkins = skins;
            }

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref mSkins);
                return true;
            }

            public int SkinCount { get { return mSkins != null ? mSkins.Length : 0; } }
            public IEnumerable<UnitSkinData> Skins
            {
                get { return mSkins; }
            }

            private UnitSkinData FindSkinById(short skinId)
            {
                int nCount = mSkins != null ? mSkins.Length : 0;
                for (int i = 0; i < nCount; ++i)
                {
                    if (mSkins[i].SkinId == skinId)
                    {
                        return mSkins[i];
                    }
                }
                return null;
            }

            public bool IsSkinExists(short skinId)
            {
                return FindSkinById(skinId) != null;
            }

            public string GetData(UnitSkinDataKind dataType, short skinId = 0, bool defaultIfNotExist = true)
            {
                string data = string.Empty;
                var skin = FindSkinById(skinId);
                if (skin != null)
                {
                    data = skin.GetData(dataType);
                }

                if (string.IsNullOrEmpty(data))
                {
                    if (defaultIfNotExist && skinId != 0)
                    {
                        data = GetData(dataType, 0, false);
                    }
                }
                return data;
            }

            public string GetName(short skinId)
            {
                var skin = FindSkinById(skinId);
                if (skin != null)
                {
                    return skin.SkinName;
                }

                return string.Empty;
            }

            public short GetId(string skinName)
            {
                int nCount = mSkins != null ? mSkins.Length : 0;
                for (int i = 0; i < nCount; ++i)
                {
                    if (mSkins[i].SkinName == skinName)
                    {
                        return mSkins[i].SkinId;
                    }
                }
                return 0;
            }
        }
    }
}
