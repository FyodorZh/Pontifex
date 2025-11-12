using System;
using System.Text;

namespace SharedCode.Shared
{
    public static class Base64Helper
    {
        public static string Base64EncodeString(string plainText)
        {
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes);
        }

        public static string Base64DecodeString(string base64EncodedData)
        {
            var base64EncodedBytes = Convert.FromBase64String(base64EncodedData);
            return Encoding.UTF8.GetString(base64EncodedBytes);
        }

        public static string encode(byte[] data)
        {
            return Convert.ToBase64String(data);
        }

        public static byte[] decode(string base64EncodedData)
        {
            return Convert.FromBase64String(base64EncodedData);
        }
    }
}