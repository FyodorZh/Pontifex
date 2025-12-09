using System.Collections.ObjectModel;
using Scriba;
using Terminal.Gui.Drivers;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Terminal.UI;

namespace Pontifex.UI
{
    public class LoggerView : View, Scriba.ILogConsumer
    {
        private readonly ObservableCollection<TrimmedString> _collection = new();

        public int Count => _collection.Count;

        public LoggerView()
        {
            var listView = new SimpleScrollableList(this)
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(0),
                Height = Dim.Fill(0),
            };

            listView.SetSource(_collection);
            listView.CollectionChanged += (sender, args) =>
            {
                if (args.NewItems != null)
                {
                    int count = args.NewItems.Count;
                    if ((_collection.Count - count) - listView.TopItem == listView.Frame.Height)
                    {
                        listView.TopItem += count;
                        listView.SelectedItem = _collection.Count - 1;
                    }
                }
            };

            listView.KeyDown += (sender, args) =>
            {
                if (args.KeyCode == KeyCode.Enter || args.KeyCode == KeyCode.Space)
                {
                    MessageBox.Query(App, "", _collection[listView.SelectedItem!.Value].Value, "OK");
                }
            };
        }

        public void AddLog(string severity, string msg)
        {
            msg = FormatMsg(severity, msg);
            _collection.Add(new TrimmedString(msg));
            if (_collection.Count > 1000)
            {
                _collection.RemoveAt(0);
            }
        }
        
        private static string FormatMsg(string severity, string msg)
        {
            return $"{DateTime.Now:HH:mm:ss}: {severity}: {msg}";//severity + ": " + msg;
        }

        private class TrimmedString
        {
            public string Value { get; }
            public string Trimmed { get; }

            public TrimmedString(string value)
            {
                Value = value.Length > 4000 ? value.Substring(0, 4000) + "..." : value;
                Trimmed = value.Length > 500 ? value.Substring(0, 500) + "..." : value;
            }

            public override string ToString()
            {
                return Trimmed;
            }
        }

        public void Message(MessageData logMessage)
        {
            TextWriter sb = new StringWriter();
            logMessage.WriteMessageTo(sb);
            this.AddLog(logMessage.Severity, sb.ToString());
        }

        public void AddRef()
        {
        }

        public void Release()
        {
        }
    }
}