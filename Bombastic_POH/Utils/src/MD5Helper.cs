using System.Text;
namespace Shared
{
    public static class MD5Helper
    {
        /// <summary>
        /// Hash an input string and return the hash as 
        /// a 32 character hexadecimal string.
        /// </summary>
        public static string GetMd5Hash(string input)
        {
            // Convert the input string to a byte array and compute the hash.
            return GetMd5Hash(StringToAscii(input));
        }

        public static string GetMd5Hash(byte[] data)
        {
            byte[] hashData;
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                hashData = md5.ComputeHash(data);
            }
            
            // Create a new Stringbuilder to collect the bytes
            // and create a string.
            StringBuilder sBuilder = new StringBuilder();

            // Loop through each byte of the hashed data 
            // and format each one as a hexadecimal string.
            for (int i = 0; i < hashData.Length; i++)
            {
                sBuilder.Append(hashData[i].ToString("x2"));
            }

            // Return the hexadecimal string.
            return sBuilder.ToString();
        }

        private static byte[] StringToAscii(string s)
        {
            byte[] retval = new byte[s.Length];
            for (int ix = 0; ix < s.Length; ++ix)
            {
                char ch = s[ix];
                if (ch <= 0x7f)
                    retval[ix] = (byte)ch;
                else
                    retval[ix] = (byte)'?';
            }
            return retval;
        }
    }
}
