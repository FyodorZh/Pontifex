using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public abstract class ExternalDropSource : IDataStruct
    {
        protected ExternalDropSource()
        {
        }

        protected ExternalDropSource(int probabilityPercent)
        {
            ProbabilityPercent = probabilityPercent;
        }

        [EditorField]
        public int ProbabilityPercent;
        
        public virtual bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref ProbabilityPercent);

            return true;
        }
    }
}