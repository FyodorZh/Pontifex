using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class ReRollDescription : IDataStruct
    {
        public ReRollDescription(ReRollTryDescription[] reRollTries)
        {
            ReRollTries = reRollTries;
        }
    
        public ReRollDescription()
        {
            
        }        

        [EditorField]
        public ReRollTryDescription[] ReRollTries;

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref ReRollTries);

            return true;
        }

        public class ReRollTryDescription : IDataStruct
        {
            public ReRollTryDescription(Price price, int minReRollCount)
            {
                Price = price;
                MinReRollCount = minReRollCount;
            }

            public ReRollTryDescription()
            {                
            }            

            [EditorField]
            public Price Price;

            [EditorField]
            public int MinReRollCount;

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref Price);
                dst.Add(ref MinReRollCount);

                return true;
            }
        }
    }
}
