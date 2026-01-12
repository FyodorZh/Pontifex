using System.Runtime.InteropServices;
using Archivarius;

namespace Pontifex.Api.Protocol
{
    [Guid("D4942561-F8A1-4F0E-BEB6-89FCCAC9FF48")]
    public class DisconnectMessage : IDataStruct
    {
        public void Serialize(ISerializer serializer)
        {
        }
    }
}
