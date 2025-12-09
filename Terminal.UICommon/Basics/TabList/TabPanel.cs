using System;
using System.Collections.Generic;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Trader.Utils;

namespace Terminal.UI
{
    public class TabPanel : FrameView
    {
        private readonly List<TitleView> _titles = new List<TitleView>();
        private readonly List<PanelViewImpl> _panels = new List<PanelViewImpl>();

        private int _activeTabId = -1;

        public TabPanel(params string[] names)
        {
            foreach (var name in names)
            {
                AddTab(name);
            }
        }

        public void AddTab(string name)
        {
            if (GetPanelId(name) >= 0)
            {
                return;
            }
            
            var title = new TitleView(name)
            {
                Y = 0,
            };
            title.Accepting += (_, _) => OnClick(title);
            _titles.Add(title);

            PanelViewImpl panel = new PanelViewImpl(this, title);
            _panels.Add(panel);

            Reattach(_titles.Count - 1);
            
            if (_activeTabId < 0)
            {
                _activeTabId = 0;
                _panels[0].SetActive(true);
                _titles[0].SetActive(true);
            }

            Border.Add(title);
            Add(panel);
        }

        private void Reattach(int id)
        {
            if (id == 0)
            {
                _titles[id].X = 1;
            }
            else
            {
                _titles[id].X = Pos.Right(_titles[id - 1]) + 1;
            }
        }

        private void OnClick(TitleView title)
        {
            int id = GetPanelId(title.Name);
            if (id >= 0 && _activeTabId != id)
            {
                if (_activeTabId != -1)
                {
                    _panels[_activeTabId].SetActive(false);
                    _titles[_activeTabId].SetActive(false);
                }

                _activeTabId = id;
                _panels[id].SetActive(true);
                _titles[id].SetActive(true);
            }
        }
        
        private int GetPanelId(string name)
        {
            for (int i = 0; i < _panels.Count; ++i)
            {
                if (_panels[i].Name == name)
                {
                    return i;
                }
            }

            return -1;
        }

        public bool TryGetPanel(string name, out PanelView view)
        {
            int id = GetPanelId(name);
            if (id >= 0)
            {
                view = _panels[id];
                return true;
            }

            view = default!;
            return false;
        }

        public PanelView GetTab(string name)
        {
            if (!TryGetPanel(name, out var panel))
            {
                throw new InvalidOperationException();
            }

            return panel;
        }

        private class TitleView : Button
        {
            private string _name;
            private bool _isActive;
            public string Name
            {
                get => _name;
                set
                {
                    _name = value;
                    SetActive(_isActive);
                }
            }
            
            public TitleView(string name)
            {
                _name = name;
                SetActive(false);
                CanFocus = false;
                NoDecorations = true;
                NoPadding = true;
            }

            public void SetActive(bool active)
            {
                _isActive = active;
                Text = active ? $"[{Name}]" : $" {Name} ";
            }
        }

        public abstract class PanelView : View
        {
            public abstract IObservableField<bool> IsActive { get; }
            public abstract string Name { get; }
            public abstract bool SetName(string newName);
        }

        private sealed class PanelViewImpl : PanelView
        {
            private readonly TabPanel _owner;
            private readonly TitleView _title;

            private readonly ObservableField<bool> _isActive = new(false);
            
            public override IObservableField<bool> IsActive => _isActive;
            public override string Name => _title.Name;

            public PanelViewImpl(TabPanel owner, TitleView titleView)
            {
                _owner = owner;
                _title = titleView;
                Width = Dim.Fill();
                Height = Dim.Fill();
                SetActive(false);
            }
            
            public override bool SetName(string newName)
            {
                if (_owner.GetPanelId(newName) < 0)
                {
                    _title.Name = newName;
                    return true;
                }

                return false;
            }
            
            public void SetActive(bool active)
            {
                Visible = active;
                _isActive.Value = active;
            }
        }
    }
}