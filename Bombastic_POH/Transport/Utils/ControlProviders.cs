using System.Collections.Generic;
using Transport.Abstractions;

namespace Transport.Utils
{
    public class VoidControlProvider : SingleControlProvider
    {
        public VoidControlProvider()
            : base(null)
        { }
    }

    public class SingleControlProvider : IControlProvider
    {
        private readonly IControl mControl;

        public SingleControlProvider(IControl control)
        {
            mControl = control;
        }

        TControl IControlProvider.TryGetControl<TControl>(string name)
        {
            TControl ctl = mControl as TControl;
            if (ctl != null)
            {
                if (name == null || ctl.Name == name)
                {
                    return ctl;
                }
            }
            return null;
        }

        IEnumerable<TControl> IControlProvider.GetControls<TControl>(string name)
        {
            TControl ctl = mControl as TControl;
            if (ctl != null)
            {
                if (name == null || ctl.Name == name)
                {
                    yield return ctl;
                }
            }
        }
    }

    public class CombinedControlProvider : IControlProvider
    {
        private readonly IControlProvider mProviderA;
        private readonly IControlProvider mProviderB;

        public CombinedControlProvider(IControlProvider a, IControlProvider b)
        {
            mProviderA = a;
            mProviderB = b;
        }

        public TControl TryGetControl<TControl>(string name) where TControl : class, IControl
        {
            TControl ctl = mProviderA.TryGetControl<TControl>(name);
            if (ctl == null)
            {
                ctl = mProviderB.TryGetControl<TControl>(name);
            }
            return ctl;
        }

        IEnumerable<TControl> IControlProvider.GetControls<TControl>(string name)
        {
            foreach (var ctl in mProviderA.GetControls<TControl>(name))
            {
                yield return ctl;
            }

            foreach (var ctl in mProviderB.GetControls<TControl>(name))
            {
                yield return ctl;
            }
        }
    }
}
