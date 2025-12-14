// ReSharper disable once CheckNamespace
namespace Pontifex.Utils
{
    internal static class HostResolver
    {
        public static bool TryParse(string host, out System.Net.IPAddress ipAddress)
        {
            if (System.Net.IPAddress.TryParse(host, out ipAddress))
            {
                return true;
            }

            try
            {
                System.Net.IPAddress ipv4 = null;
                System.Net.IPAddress ipv6 = null;

                System.Net.IPHostEntry hosts = System.Net.Dns.GetHostEntry(host);
                for (int i = 0; i < hosts.AddressList.Length; i++)
                {
                    System.Net.IPAddress tmpIp = hosts.AddressList[i];
                    if (ipv4 == null && tmpIp.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) //save first found ipv4
                    {
                        ipv4 = tmpIp;
                    }
                    else if (ipv6 == null && tmpIp.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6) //save first found ipv6
                    {
                        ipv6 = tmpIp;
                    }
                }

                if (ipv6 != null) //return ipv6 address for guys from 2138 year
                {
                    ipAddress = ipv6;
                    return true;
                }

                if (ipv4 != null) //return ipv4 address if we found it
                {
                    ipAddress = ipv4;
                    return true;
                }

                return false;
            }
            catch
            {
                ipAddress = null;
                return false;
            }
        }
    }
}