using Pontifex.UI;
using Scriba;
using Terminal.Gui;

namespace Pontifex.Test
{
    public class SessionView : Toplevel
    {
        public SessionView()
        {
            // Closing += (sender, args) =>
            // {
            //     args.Cancel = true;
            // };

            Title = "Session View";
            var menuBar = new MenuBar();
            menuBar.Menus =
            [
                new MenuBarItem("_File", [
                    new MenuItem("_GC.Collect", "", () => { GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive); }),
                    new MenuItem("_Quit", "", () => { Application.RequestStop(); })
                ]),
                new MenuBarItem("_TMP", [
                    new MenuItem("abc", "", () => {
                    {
                        for (int i = 0; i < 10; ++i) Log.i("Hello " + i);
                    }})
                ])
            ];
            
            Add(menuBar);

            var loggerView = new LoggerView()
            {
                X = 0, Y = 1, Width = Dim.Fill(), Height = Dim.Fill()
            };
            
            Log.AddConsumer(loggerView, true);
            
            Add(loggerView);

/*
            Presenter.Session.Started.SubscribeUI(started =>
            {
                if (started)
                {
                    var list = new List<MenuBarItem>(menuBar.Menus)
                    {
                        new MenuBarItem("_Systems", [
                            new MenuItem("_Scripting", "", () =>
                            {

                            })
                        ])
                    };
                    menuBar.Menus = list.ToArray();
                    Logger.Info("Started.");
                }
            });

            Win.Add(menuBar);

            AddSubView(new ConsoleView());
            //AddSubView(new CommandLineView());

            Presenter.Start();
            */
        }
    }
}