using System.Collections.Generic;
using System.IO;
using Serializer.Tools;
using Shared.Collections;

namespace Shared
{
    namespace CommonData
    {
        public static class UnitDescriptionConstants
        {
            public const string HERO_DESCRIPTION_BLOB_PATH = "Assets/LogicResources/Runtime/Logic/Blobs/Descriptions/Units/HeroDescriptions.bytes";
        }

        public sealed class Descriptions
        {
            private MVPBalanceCoefficients mMVPBalanceCoefficients;
            public IMVPBalanceCoefficients MVPBalanceCoefficients { get { return mMVPBalanceCoefficients; } }

            public ReadOnlyDictionary<short, UnitDescription> UnitDescriptions { get; private set; }

            private readonly DescriptionsByPath<UnitDescription> mStorage = new DescriptionsByPath<UnitDescription>();

            public Descriptions()
            {
                UnitDescriptions = new ReadOnlyDictionary<short, UnitDescription>(mStorage.UnitDescriptions);
            }

            public UnitDescription GetUnitDescription(short descriptionId)
            {
                return mStorage.GetUnitDescription(descriptionId);
            }

            public void UnloadDescriptions(string descriptionPath)
            {
                UnLoadDescriptions(descriptionPath);
            }

            public bool SetDescriptionsData(string descriptionsPath, byte[] bytes)
            {
                bool isHero = Path.GetFileName(descriptionsPath).Contains(Path.GetFileName(UnitDescriptionConstants.HERO_DESCRIPTION_BLOB_PATH));

                DBG.Diagnostics.Assert(bytes != null, "Set Description blob, bytes are null for path \"{0}\"", descriptionsPath);
                mStorage.AddNewPathIfNeeded(descriptionsPath);

                using (var ctor = new CommonDataConstructor<UnitDescriptionsContainer, CommonResoucesFactory>(bytes, true))
                {
                    if (!ctor.IsInited || ctor.Data.Units == null)
                    {
                        Log.e("Can't deserialize unit blob data.");
                        return false;
                    }

                    if (isHero)
                    {
                        mMVPBalanceCoefficients = ctor.Data.MVPBalanceCoeffs;
                        if (mMVPBalanceCoefficients == null)
                        {
                            Log.e("MVP Balance Coefficients are null in {0}", descriptionsPath);
                            mMVPBalanceCoefficients = new MVPBalanceCoefficients();
                        }
                    }

                    int count = ctor.Data.Units.Length;
                    for (int i = 0; i < count; i++)
                    {
                        UnitDescriptionData desc = ctor.Data.Units[i];
                        if (mStorage.ContainsDescription(desc.DescriptionId))
                        {
                            Log.e("Dublicated unit id detected: \"{0}\", skip it", desc.DescriptionId);
                            continue;
                        }
                        mStorage.AddNewUnitDescription(descriptionsPath, desc.DescriptionId, new UnitDescription(desc));
                    }
                }
                return true;
            }

            public void UnloadAllDescriptions()
            {
                mStorage.UnloadAllDescriptions();
            }

            public void UnLoadDescriptions(string dataPath)
            {
                mStorage.UnLoadDescriptions(dataPath);
            }

            public bool ContainsDescriptions(string path)
            {
                return mStorage.ContainsDescriptions(path);
            }

            public void AddDescriptions(string descriptionsPath, Descriptions newDescriptions, bool forceReload)
            {
                mStorage.AddDescriptions(descriptionsPath, newDescriptions.UnitDescriptions, forceReload);
                if (mMVPBalanceCoefficients == null)
                {
                    mMVPBalanceCoefficients = newDescriptions.mMVPBalanceCoefficients;
                }
            }

            public bool LoadBlob(string descriptionPath, bool forceReload)
            {
                if (mStorage.ContainsDescriptions(descriptionPath) && !forceReload)
                {
                    return true;
                }

                if (forceReload)
                {
                    if (mStorage.ContainsDescriptions(descriptionPath))
                    {
                        UnLoadDescriptions(descriptionPath);
                    }
                }
                if (File.Exists(descriptionPath))
                {
                    byte[] bytes = File.ReadAllBytes(descriptionPath);
                    SetDescriptionsData(descriptionPath, bytes);
                }
                else
                {
                    Log.e("LoadDescriptions: can't find descriptions blob, {0}" + descriptionPath);
                    return false;
                }
                return true;
            }
        }
    }
}