using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using LogConsumers;

namespace TransportAnalyzer
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Log.AddConsumer(new StudioDebugConsumer(), true);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TransportAnalyzer());
        }
    }
}
