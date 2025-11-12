using System;

namespace Transport
{
    /// <summary>
    /// Реализует бизнесс-логика.
    /// Протокол может быть здесь
    /// </summary>
    public interface IClientEndPointHandler
    {
        /// <summary>
        /// Логический коннект. Информирует бизнесс-логику, что всё в транспорте настроено, можно им пользоваться.
        /// </summary>
        /// <param name="endPoint"> EndoPoint к клиенту </param>
        void OnConnected(IClientEndPoint endPoint);

        /// <summary>
        /// Логический дисконнект. Информирует бизнесс-логику, что пользоваться транспортом более нельзя.
        /// Фактически транспорт может быть ещё жив.
        /// OnReceived() более не вызываются
        /// </summary>
        void OnDisconnected();

        /// <summary>
        /// После OnConnected() начинают приходить данные от клиента.
        /// После OnDisconnected() данные НЕ приходят
        /// </summary>
        /// <param name="data"></param>
        void OnReceived(byte[] data);
    }

    /// <summary>
    /// Реализует транспортная система.
    /// </summary>
    public interface IClientEndPoint
    {
        bool Send(byte[] data);
        bool Disconnect();
        bool IsConnected { get; }
    }

    /// <summary>
    /// Реализует бизнесс-логика
    /// </summary>
    public interface IAcknowledger<out ITHandler>
        where ITHandler : IClientEndPointHandler
    {
        ITHandler Acknowledge(byte[] data);
    }

    public interface IServer<in ITHandler>
        where ITHandler : IClientEndPointHandler
    {
        string Type { get; }
        bool Start(IAcknowledger<ITHandler> acknowledger, Action onStopped);
        bool Stop();
    }
}