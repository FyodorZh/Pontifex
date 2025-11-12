using System.Text;
using Shared;
using Transport.Abstractions.Handlers;

namespace MetaGame
{
    public class MetaAcknowledger : IAckHandler
    {
        public readonly string secretKey;

        public MetaAcknowledger(string secretKey)
        {
            this.secretKey = secretKey;
        }

        public static MetaAcknowledger fromBytes(ByteArraySegment data, int secretKeyLength)
        {
            if (data.IsValid && data.Count == secretKeyLength)
            {
                var secretKey = Encoding.UTF8.GetString(data.Array, data.Offset, data.Count);
                return new MetaAcknowledger(secretKey);
            }
            return null;
        }

        #region Implementation of IAckClientHandler

        byte[] IAckHandler.GetAckData()
        {
            return Encoding.UTF8.GetBytes(secretKey);
        }

        #endregion
    }
}