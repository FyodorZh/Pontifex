using Terminal.UI;

namespace Pontifex.Test
{
    static class Program
    {
        static void Main(string[] args)
        {
            //SpeedTest.Test(); return;
            TransportFactory factory = new TransportFactory();
            UISystem.Init();
            UISystem.Run(new SessionView(factory));
        }
    }
}