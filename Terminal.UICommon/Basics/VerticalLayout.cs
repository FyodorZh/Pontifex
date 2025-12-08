using System;
using System.Collections.Generic;
using System.Drawing;
using Terminal.Gui;

namespace Terminal.UI
{
    public class VerticalLayout : View
    {
        private Size _size;

        private readonly EventHandler _onDisposing;

        public Size Size => _size;

        public VerticalLayout()
        {
            _onDisposing = (EventHandler)OnElementDisposing;
            ContentClear();
        }

        public VerticalLayout(params View[] elements)
        {
            _onDisposing = (EventHandler)OnElementDisposing;
            ContentClear();
            ContentAdd(elements);
        }
        
        public VerticalLayout(IEnumerable<View> elements)
        {
            _onDisposing = (EventHandler)OnElementDisposing;
            ContentClear();
            ContentAdd(elements);
        }

        public void RefreshSize()
        {
            _size = new Size(0, 0);
            foreach (var element in Subviews)
            {
                element.Y = _size.Height;
                _size.Height += element.Frame.Height;
                _size.Width = Math.Max(_size.Width, element.Frame.Width);
            }
            
            Width = _size.Width;
            Height = _size.Height;
            SetContentSize(_size);
        }

        public void ContentClear()
        {
            foreach (var view in Subviews)
            {
                view.Disposing -= _onDisposing;
            }

            RemoveAll();
            Width = Dim.Absolute(0);
            Height = Dim.Absolute(0);
            _size = new Size(0, 0);
            SetContentSize(_size);
        }

        public TView ContentAdd<TView>(TView element) where TView : View
        {
            ContentAdd((View)element);
            return element;
        }

        public void ContentAdd(View element)
        {
            element.X = 0;
            element.Y = _size.Height;
            Add(element);
            
            _size.Height += element.Frame.Height;
            _size.Width = Math.Max(_size.Width, element.Frame.Width);
            
            Width = _size.Width;
            Height = _size.Height;
            
            SetContentSize(_size);

            element.Disposing += _onDisposing;
        }

        private void OnElementDisposing(object? sender, EventArgs args)
        {
            RefreshSize();
        }

        public void ContentAdd(IEnumerable<View> elements)
        {
            foreach (var element in elements)
            {
                element.Y = _size.Height;
                Add(element);

                _size.Height += element.Frame.Height;
                _size.Width = Math.Max(_size.Width, element.Frame.Width);
                
                element.Disposing += _onDisposing;
            }

            Width = _size.Width;
            Height = _size.Height;
            
            SetContentSize(_size);
        }
    }
}