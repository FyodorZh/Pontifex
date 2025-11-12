using System;

namespace Shared.LogicSynchronizer
{
    /// <summary>
    /// Помечает оборачиваемый тип
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false)]
    public class WrapperAttribute : Attribute
    {
        public bool CanBeReadOnly { get; set; }

        public bool Log { get; set; }

        public WrapperAttribute()
        {
            CanBeReadOnly = false;
            Log = false;
        }
    }
}