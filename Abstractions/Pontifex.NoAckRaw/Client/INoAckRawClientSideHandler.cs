using Pontifex.Utils;

namespace Pontifex.NoAckRaw
{
    public interface INoAckRawClientSideHandler : IHandler
    {
        /// <summary>
        /// Вызывается после полной инициализации транспорта
        /// </summary>
        /// <param name="endpoint"> Настороенный и готовый к работе эндпоинт для отправки сообщений </param>
        void OnStarted(INoAckRawClientSideEndpoint endpoint);

        /// <summary>
        /// Информирует об пришедшем сообщении от сервера.
        /// Начинает работать после Started()
        /// </summary>
        /// <param name="message"> Данные, присланные сервером. Отдаются во владение обработчику </param>
        void OnReceived(UnionDataList message);
        /// <summary>
        /// Информирует о разрушении транспорта. Приходит строго после Started()
        /// Эндпоинт становится невалидным
        /// </summary>
        void OnStopped();
    }
}