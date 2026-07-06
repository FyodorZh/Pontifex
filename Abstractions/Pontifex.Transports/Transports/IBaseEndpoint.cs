using System;
using System.Collections.Generic;

namespace Pontifex
{
    public interface IBaseEndpoint
    {
        /// <summary>
        /// Populates <paramref name="dst"/> with all <see cref="IControl"/> interfaces
        /// exposed by this endpoint, optionally filtered by <paramref name="predicate"/>.
        /// </summary>
        /// <original>Возвращает все интерфейсы контроля</original>
        void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null);
    }
}