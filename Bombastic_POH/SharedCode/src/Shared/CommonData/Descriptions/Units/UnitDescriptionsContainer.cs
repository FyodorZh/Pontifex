using Serializer.BinarySerializer;

namespace Shared
{
    namespace CommonData
    {
        public class UnitDescriptionsContainer : IDataStruct
        {
            public UnitDescriptionData[] Units;
            public MVPBalanceCoefficients MVPBalanceCoeffs;

            public bool Serialize(IBinarySerializer stream)
            {
                stream.Add(ref Units);
                stream.Add(ref MVPBalanceCoeffs);
                return true;
            }
        }
    }
}
