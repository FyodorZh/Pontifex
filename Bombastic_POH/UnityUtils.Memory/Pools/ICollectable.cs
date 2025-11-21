using Actuarius.Memory;

namespace Shared.Pooling
{
    public interface ICollectable<out TResource>
    {
        /// <summary>
        /// Вызывается всякий раз при записи объекта в пулл. Объект может выгрузить часть своих данных
        /// </summary>
        /// <returns> True если объект можно сохранять для переиспользования </returns>
        bool Collected();

        /// <summary>
        /// Вызывается при изъятии объекта из пула. Объект подготавливается к использованию.
        /// </summary>
        /// <param name="pool"> Пул владелец </param>
        void Restored(IPoolSink<TResource> pool);
    }
}