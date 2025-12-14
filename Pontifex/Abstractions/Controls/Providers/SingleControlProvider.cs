using System.Collections.Generic;
using Pontifex.Abstractions;

namespace Pontifex.Utils
{
    public class SingleControlProvider : IControlProvider
    {
        private readonly IControl? _control;

        public SingleControlProvider(IControl? control)
        {
            _control = control;
        }

        TControl? IControlProvider.TryGetControl<TControl>(string? name)
            where TControl : class
        {
            if (_control is TControl ctl)
            {
                if (name == null || ctl.Name == name)
                {
                    return ctl;
                }
            }
            return null;
        }

        IEnumerable<TControl> IControlProvider.GetControls<TControl>(string? name)
        {
            if (_control is TControl ctl)
            {
                if (name == null || ctl.Name == name)
                {
                    yield return ctl;
                }
            }
        }
    }
}
