using System;
using System.Net;
using Pontifex.Factory.Registration;

namespace Pontifex.NetSockets
{
    /// <summary>
    /// Shared parameter and address resolution support for network transport
    /// bindings.
    /// </summary>
    internal static class NetBindingSupport
    {
        public static TransportParameter HostParameter()
        {
            return new StringParameter("host", required: true, positional: true);
        }

        public static TransportParameter PortParameter()
        {
            return new IntegerParameter(
                "port",
                required: true,
                positional: true,
                new IntegerType(0, 65535));
        }

        public static void ResolveAddress(
            ITransportBuildContext context,
            out IPAddress ipAddress,
            out int port)
        {
            if (!context.TryGetString("host", out var host))
            {
                throw new InvalidOperationException(
                    "The resolver guarantees the required 'host' parameter.");
            }

            if (!HostResolver.TryParse(host, out var resolved))
            {
                throw new InvalidOperationException(
                    $"The host '{host}' cannot be resolved.");
            }

            if (!context.TryGetInt64("port", out var portValue))
            {
                throw new InvalidOperationException(
                    "The resolver guarantees the required 'port' parameter.");
            }

            if (portValue < 0 || portValue > 65535)
            {
                throw new InvalidOperationException(
                    $"The port '{portValue}' is outside the valid range.");
            }

            ipAddress = resolved;
            port = (int)portValue;
        }
    }
}
