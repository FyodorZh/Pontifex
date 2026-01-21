using Archivarius;

namespace Pontifex.Api
{
    public struct EmptyMessage : IDataStruct
    {
        public void Serialize(ISerializer serializer)
        {
        }
    }
}
