using Serializer.BinarySerializer;

namespace Shared
{
    namespace CommonData
    {
        public class TutorialData : IDataStruct
        {
            public const string BLOB_PATH = "Assets/LogicResources/Runtime/Data/TutorialData.bytes";

            public TutorialStage[] Stages;

            #region IDataStruct Members

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref Stages);

                return true;
            }

            #endregion
        }
    }
}
