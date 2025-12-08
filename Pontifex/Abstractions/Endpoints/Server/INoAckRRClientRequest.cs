using Actuarius.Memory;
using Pontifex.Utils;

namespace Transport.Abstractions.Endpoints.Server
{
    /// <summary>
    /// Инкапсулирует единичный запрос от клиента
    /// </summary>
    public interface INoAckReliableRRCallback
    {
        int MessageMaxByteSize { get; }
            
        /// <summary>
        /// Отсылает данные клиенту
        /// </summary>
        /// <param name="data"> Что отправить </param>
        /// <returns> FALSE - сразу что-то пошло не так, данные доставлены не будут </returns>
        SendResult Response(UnionDataList data);
    }
}
