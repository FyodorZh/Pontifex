using System.Collections.Generic;

namespace Shared
{
    namespace CommonData
    {
        public sealed class DescriptionsByPath<T>
            where T : IUnitDescription
        {
            public Dictionary<short, T> UnitDescriptions = new Dictionary<short, T>();
            private readonly Dictionary<string, List<short>> mDescriptionsPaths = new Dictionary<string, List<short>>();

            public T GetUnitDescription(short descriptionId)
            {
                T result;
                if (UnitDescriptions.TryGetValue(descriptionId, out result))
                {
                    return result;
                }
                Log.e("Can't find unit description with id \"{0}\"", descriptionId);
                return default(T);
            }

            public bool ContainsDescriptions(string path)
            {
                return mDescriptionsPaths.ContainsKey(path);
            }
            public bool ContainsDescription(short descriptionId)
            {
                return UnitDescriptions.ContainsKey(descriptionId);
            }

            public void UnloadAllDescriptions()
            {
                List<string> pathsForRemove = new List<string>();
                foreach (string path in mDescriptionsPaths.Keys)
                {
                    if (mDescriptionsPaths.ContainsKey(path))
                    {
                        var descForUnload = mDescriptionsPaths[path];
                        for (int i = 0; i < descForUnload.Count; ++i)
                        {
                            UnitDescriptions.Remove(descForUnload[i]);
                        }

                        pathsForRemove.Add(path);
                    }
                    else
                    {
                        Log.e("dont have descriptions from file " + path);
                    }
                }

                for (int i = 0; i < pathsForRemove.Count; ++i)
                {
                    mDescriptionsPaths.Remove(pathsForRemove[i]);
                }
            }

            public void UnLoadDescriptions(string dataPath)
            {
                if (mDescriptionsPaths.ContainsKey(dataPath))
                {
                    List<short> descForUnload = mDescriptionsPaths[dataPath];
                    for (int i = 0; i < descForUnload.Count; ++i)
                    {
                        UnitDescriptions.Remove(descForUnload[i]);
                    }

                    mDescriptionsPaths.Remove(dataPath);
                }
                else
                {
                    Log.e("dont have descriptions from file " + dataPath);
                }
            }
            public void AddDescriptions(string descriptionsPath, IDictionary<short, T> newDescriptions, bool forceReload)
            {
                if (mDescriptionsPaths.ContainsKey(descriptionsPath) && !forceReload)
                {
                    return;
                }

                if (forceReload)
                {
                    if (mDescriptionsPaths.ContainsKey(descriptionsPath))
                    {
                        UnLoadDescriptions(descriptionsPath);
                    }
                }

                if (!mDescriptionsPaths.ContainsKey(descriptionsPath))
                {
                    mDescriptionsPaths.Add(descriptionsPath, new List<short>());
                }

                foreach (var item in newDescriptions)
                {
                    if (UnitDescriptions.ContainsKey(item.Key))
                    {
                        Log.e("Duplicate unit description Id: {0}, {1}. Conflicts with: {2}", item.Value.DescriptionId, item.Value.Name, UnitDescriptions[item.Value.DescriptionId].Name);
                        continue;
                    }
                    UnitDescriptions.Add(item.Value.DescriptionId, item.Value);
                    mDescriptionsPaths[descriptionsPath].Add(item.Value.DescriptionId);
                }
            }
            public void AddNewPathIfNeeded(string descriptionsPath)
            {
                if (!mDescriptionsPaths.ContainsKey(descriptionsPath))
                {
                    mDescriptionsPaths.Add(descriptionsPath, new List<short>());
                }
            }

            public void AddNewUnitDescription(string descriptionsPath, short unitId, T unitDescription)
            {
                UnitDescriptions.Add(unitId, unitDescription);
                mDescriptionsPaths[descriptionsPath].Add(unitId);
            }
        }
    }

}