namespace Pontifex.Utils
{
    internal static class UrlStringParser
    {
        // host:port
        public static bool TryParseAddress(string address, out System.Net.IPAddress ip, out int port)
        {
            try
            {
                var list = address.Split(':');
                port = int.Parse(list[1]);
                return HostResolver.TryParse(list[0], out ip);
            }
            catch
            {
                ip = null;
                port = -1;
                return false;
            }
        }
    }
}
