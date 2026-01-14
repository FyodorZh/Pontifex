namespace Pontifex.Api.Protocol
{
    public class EmptyApi : ProtocolApi
    {
    }

    namespace Client
    {
        public class EmptyApi_Client : EmptyApi
        {
        }
    }

    namespace Server
    {
        public class EmptyApi_Server : EmptyApi
        {
            public bool Disconnected { get; private set; }
            
            public EmptyApi_Server()
            {
                Disconnect.SetProcessor(msg =>
                {
                    Disconnected = true;
                });
            }
        }
    }
}