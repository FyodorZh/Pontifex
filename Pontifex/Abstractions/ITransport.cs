using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Scriba;

namespace Pontifex.Abstractions
{
    public interface ITransport
    {
        /// <summary>
        /// Тип транспорта. Уникальный идентификатор
        /// </summary>
        string Type { get; }

        /// <summary>
        /// Флаг валидности транспортной системы. В случае ошибки выставляет в false.
        /// Поломанная транспортная система не восстанавливается.
        /// </summary>
        bool IsValid { get; }

        /// <summary>
        /// Флаг запущенности (активности) транспортной системы.
        /// ТС создаётся не запущенной.
        /// </summary>
        bool IsStarted { get; }

        /// <summary>
        /// Запускает транспортную систему. Неблокирующая операция.
        /// Server: После успешного завершения метода к серверу можно коннектиться
        /// Client: Успешное завершение метода значит что транспорт инициализировался и начал ассинхронный процесс подключения к серверу
        /// </summary>
        /// <param name="onStopped"> Если вернули true, то при остановке ТС должен быть вызван onStopped (если он не null) </param>
        /// <returns>
        /// Возвращает false если стартовать транспорт не удалось. После этого ТС переходит в невалидное состояние.
        /// Возвращает true если удалось запустить операцию старта транспортной системы.
        /// </returns>
        bool Start(System.Action<StopReason> onStopped);

        /// <summary>
        /// Останавливает ТС, если она была запущена. Иначе не происходит ничего
        /// </summary>
        bool Stop(StopReason? reason = null);

        ILogger Log { get; }
        
        IMemoryRental Memory { get; }
    }
}