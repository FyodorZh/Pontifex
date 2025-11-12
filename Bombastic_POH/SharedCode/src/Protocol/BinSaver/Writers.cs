using System.Collections.Generic;

namespace Shared.Protocol
{
    public sealed class Writers : ThreadSingleton<Writers>
    {
        private readonly ProtocolWriter mWriter = new ProtocolWriter();

        public Writers()
        {
        }

        public static ProtocolWriter getWriter()
        {
            var writer = Instance.mWriter;
            writer.Reset();
            return writer;
        }
    }
}
