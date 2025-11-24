using System.Collections.Generic;

namespace Transport.Abstractions
{
    public interface IControlProvider
    {
        /// <summary>
        /// Запрашивает интерфейс контроля над ТС.
        /// </summary>
        TControl? TryGetControl<TControl>(string? name = null) where TControl : class, IControl;

        /// <summary>
        /// Возвращает все интерфейсы контроля заданного типа по имени
        /// </summary>
        /// <typeparam name="TControl"></typeparam>
        /// <param name="name"> Имена контроля </param>
        /// <returns></returns>
        IEnumerable<TControl> GetControls<TControl>(string? name) where TControl : class, IControl;
    }
}
