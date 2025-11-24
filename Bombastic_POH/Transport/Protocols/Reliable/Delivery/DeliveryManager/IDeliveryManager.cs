using System;
using Shared;
using Transport.Abstractions;
using IMultiRefByteArray = Actuarius.Memory.IMultiRefByteArray;

namespace Transport.Protocols.Reliable.Delivery
{
    internal interface IDeliveryManager
    {
        event Action<DeliveryId, IMultiRefByteArray, short> Received;

        event Action<DeliveryId> FailedToDeliver;

        event Action<DeliveryId> Delivered;

        int DeliveryMaxByteSize { get; }

        /// <summary>
        /// Тредобезопасно, но вызовы должны быть сериализированны между собой!
        /// Шедулит сообщение для отправки
        /// </summary>
        /// <param name="id"> Уникальный id отправляемого сообщения</param>
        /// <param name="data"> Отправляемое сообщение </param>
        /// <param name="responseProcessTime"></param>
        /// <returns></returns>
        SendResult ScheduleDelivery(DeliveryId id, IMultiRefByteArray data, short responseProcessTime = 0);

        /// <summary>
        /// ТредоНЕбезопасно!
        /// Обрабатывает полученные данные.
        /// </summary>
        /// <param name="message"></param>
        /// <returns> false если обработать сообщение не удалось </returns>
        bool ProcessIncoming(Message message);

        /// <summary>
        /// ТредоНЕбезопасно!
        /// </summary>
        /// <returns></returns>
        IMacroOwner<Message> ProcessOutgoing(IDeliveryAttemptScheduler scheduler, DateTime now);

        void Clear();
    }
}