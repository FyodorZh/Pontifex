using Serializer.BinarySerializer;
using Shared.Meta;

namespace Shared.NeoMeta.PlayerData
{
    public class UpdatePlayerClientInfoRequest : IDataStruct
    {
        public UpdatePlayerClientInfoRequest()
        {
        }

        public UpdatePlayerClientInfoRequest(string deviceName,
            PlatformType platformType,
            string runtimePlatform,
            string deviceId,
            string countryCode,
            string languageCode,
            string clientVersion,
            string osVersion)
        {
            DeviceName = deviceName;
            PlatformType = platformType;
            RuntimePlatform = runtimePlatform;
            DeviceId = deviceId;
            CountryCode = countryCode;
            LanguageCode = languageCode;
            ClientVersion = clientVersion;
            OsVersion = osVersion;
        }

        public string DeviceName;
        public PlatformType PlatformType;
        public string RuntimePlatform;
        public string DeviceId;
        public string CountryCode;
        public string LanguageCode;
        public string ClientVersion;
        public string OsVersion;

        public bool Serialize(IBinarySerializer dst)
        {
            var platformTmp = (byte) PlatformType;
            dst.Add(ref platformTmp);
            PlatformType = (PlatformType) platformTmp;

            dst.Add(ref DeviceName);
            dst.Add(ref RuntimePlatform);
            dst.Add(ref DeviceId);
            dst.Add(ref CountryCode);
            dst.Add(ref LanguageCode);
            dst.Add(ref ClientVersion);
            dst.Add(ref OsVersion);

            return true;
        }
    }
}