using Serializer.BinarySerializer;

namespace Shared.NeoMeta
{
    public class StartMaintenanceRequest : IDataStruct
    {
        public StartMaintenanceRequest()
        {
        }
        
        public StartMaintenanceRequest(long maintenanceDate)
        {
            MaintenanceDate = maintenanceDate;
        }
        
        public long MaintenanceDate;
        
        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref MaintenanceDate);
            return true;
        }
    }
}