using System;

namespace Shared.Meta
{
    [Flags]
    public enum PlatformType
    {
        Unknown = 0,
        Android = 1 << 0,
        Ios = 1 << 1
    }
}
