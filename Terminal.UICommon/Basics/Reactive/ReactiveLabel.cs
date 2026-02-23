using System;
using System.Reactive;
using System.Threading;
using Terminal.Gui.Views;

namespace Terminal.UI
{
    public class ReactiveLabel<TData> : Label
        where TData : class
    {
        private readonly object _tickHandler;
        private readonly AnonymousObserver<TData> _observer;
        private readonly Func<TData?, string> _dataToText;

        private volatile TData? _newData;
        private TData? _currentData;
        
        public bool AutoRefreshData { get; set; }

        public IObserver<TData> DataObserver => _observer;

        public ReactiveLabel(Func<TData?, string> dataToText)
        {
            _dataToText = dataToText;
            _tickHandler = App.AddTimeout(TimeSpan.FromMilliseconds(100), Tick);
            _observer = new AnonymousObserver<TData>(data =>
            {
                _newData = data;
            });
        }

        protected override void Dispose(bool disposing)
        {
            App.RemoveTimeout(_tickHandler);
            _observer.Dispose();
            base.Dispose(disposing);
        }

        private bool Tick()
        {
            bool needToRefresh = AutoRefreshData;
            
            var newData = Interlocked.Exchange(ref _newData, null);
            if (newData != null)
            {
                _currentData = newData;
                needToRefresh = true;
            }

            if (needToRefresh)
            {
                Text = _dataToText(_currentData);
            }

            return true;
        }
    }
}