using Pontifex.Abstractions;
using Pontifex.Abstractions.Endpoints;
using Scriba;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Pontifex.Test
{
    public class ControlsPanel : FrameView
    {
        private IAckRawBaseEndpoint? _endpoint;

        private readonly List<ControlView> _views = new List<ControlView>();
        private readonly List<IControl> _controls = new();

        private bool _disposed;

        public ControlsPanel()
        {
            Title = "Controls";
            Task.Delay(1000).ContinueWith(_ => Tick());
        }

        protected override void Dispose(bool disposing)
        {
            _disposed = true;
            base.Dispose(disposing);
        }

        public void SetEndpoint(IAckRawBaseEndpoint? endpoint)
        {
            Application.Invoke(() =>
            {
                _endpoint = endpoint;
                Refresh();
            });
        }

        private void Tick()
        {
            Application.Invoke(() =>
            {
                if (_disposed)
                    return;
                Refresh();
                Task.Delay(1000).ContinueWith(_ => Tick());
            });
        }

        private void Refresh()
        {
            try
            {
                _controls.Clear();
                _endpoint?.GetControls(_controls);

                bool changed = false;

                int controlId = 0;
                int viewId = 0;
                while (controlId < _controls.Count || viewId < _views.Count)
                {
                    if (controlId == _controls.Count)
                    {
                        Remove(_views[viewId]);
                        _views[viewId].Dispose();
                        _views.RemoveAt(viewId);
                        changed = true;
                    }
                    else if (viewId == _views.Count)
                    {
                        _views.Add(ControlViewFactory.Construct(_controls[controlId]));
                        controlId++;
                        viewId++;
                        changed = true;
                    }
                    else
                    {
                        var control = _controls[controlId];
                        var view = _views[viewId];
                        if (control != view.Control)
                        {
                            _views.Insert(viewId, ControlViewFactory.Construct(control));
                            changed = true;
                        }

                        controlId++;
                        viewId++;
                    }
                }

                if (changed)
                {
                    Pos prevPos = 0;
                    foreach (var view in _views)
                    {
                        view.Y = prevPos;
                        view.RefreshSize();
                        Add(view);
                        prevPos = Pos.Bottom(view);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
            }
        }
    }
}