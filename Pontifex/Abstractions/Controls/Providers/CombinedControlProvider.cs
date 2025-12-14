using System.Collections.Generic;
using Pontifex.Abstractions;

namespace Pontifex.Utils
{
    public class CombinedControlProvider : IControlProvider
    {
        private readonly IControlProvider _providerA;
        private readonly IControlProvider _providerB;

        public CombinedControlProvider(IControlProvider a, IControlProvider b)
        {
            _providerA = a;
            _providerB = b;
        }

        public TControl? TryGetControl<TControl>(string? name) where TControl : class, IControl
        {
            return _providerA.TryGetControl<TControl>(name) ?? _providerB.TryGetControl<TControl>(name);
        }

        IEnumerable<TControl> IControlProvider.GetControls<TControl>(string? name)
        {
            foreach (var ctl in _providerA.GetControls<TControl>(name))
            {
                yield return ctl;
            }

            foreach (var ctl in _providerB.GetControls<TControl>(name))
            {
                yield return ctl;
            }
        }
    }
}