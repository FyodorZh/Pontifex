namespace Transport.Protocols.MessageProtocol
{
//    public interface IResponse
//    {
//        bool isTimeout { get; }
//    }

    public class Response //: IResponse
    {
        private bool _timeout;

        private Response()
        {
        }
        
        public Response(Message message)
        {
            Message = message;
            _timeout = false;
        }

        public Message Message { get; private set; }
        public bool IsTimeout { get { return _timeout; } }

        public static Response Timeout(short command)
        {
            return new Response
            {
                _timeout = true
            };
        }
    }
}
