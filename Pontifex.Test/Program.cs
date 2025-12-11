using Terminal.UI;

namespace Pontifex.Test
{
    static class Program
    {
        static void Main(string[] args)
        {
            TransportFactory factory = new TransportFactory();
            
            UISystem.Init();
            UISystem.Run(new SessionView(factory));

            
            
            
            
            
            
            // Log.AddConsumer(new ConsoleConsumer(), true);
            //
            // Init();
            // Console.WriteLine("Hello, World!");
            //
            // bool work = true;
            //
            // string url = "zip|9:log|direct|dir123";
            //
            // var server = mServerFactory.Construct(url);
            // ((IAckRawServer)server).Init(new AckRawServerLogic());
            // server.Start(r =>
            // {
            //     Console.WriteLine("Server stopped " + r);
            //     work = false;
            // }, StaticLogger.Instance);
            //
            // var client = mClientFactory.Construct(url);
            // ((IAckRawClient)client).Init(new AckRawClientLogic(1, 1000));
            // client.Start(r =>
            // {
            //     Console.WriteLine("Client stopped " + r);
            //     work = false;
            // }, StaticLogger.Instance);
            //
            // while (work)
            // {
            //     Thread.Sleep(100);
            // }
            
        }
    }
}