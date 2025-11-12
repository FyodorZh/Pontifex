using NewProtocol;

namespace TransportStressTest
{
    public class StressTestProtocol : Protocol
    {
        public readonly C2SRawMessageDecl Request = new C2SRawMessageDecl();
        public readonly S2CRawMessageDecl Response = new S2CRawMessageDecl();
    }
}