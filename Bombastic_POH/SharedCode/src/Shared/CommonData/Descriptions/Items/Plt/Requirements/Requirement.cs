using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public abstract class Requirement : IDataStruct
    {
        [EditorField]
        private RequirementOperation _operation;

        protected Requirement()
        {
        }

        protected Requirement(RequirementOperation operation)
        {
            _operation = operation;
        }

        public RequirementOperation Operation
        {
            get { return _operation; }
        }

        public virtual bool Serialize(IBinarySerializer dst)
        {
            var operationTmp = (byte)_operation;
            dst.Add(ref operationTmp);

            if (dst.isReader)
            {
                _operation = (RequirementOperation)operationTmp;
            }

            return true;
        }
    }
}
