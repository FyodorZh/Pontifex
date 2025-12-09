using System;
using System.Collections.Generic;
using System.Linq;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Terminal.UI;

namespace Terminal.UICommon
{
    public class PaletteConstructor : SmartWindow
    {
        public PaletteConstructor(ColorSchemeInfo? initialScheme, Action<ColorSchemeInfo> onColorSchemeChanged)
        {
            Title = "Palette Constructor";
            X = Pos.Center();
            Y = Pos.Center();
            Width = 150;
            Height = 16;

            ColorSchemeInfo info;
            if (initialScheme != null)
            {
                info = initialScheme;
            }
            else
            {
                info = new ColorSchemeInfo();
                info.LoadFromSchemeManager(false);
            }

            Stack<(string schemeName, string paletteName, Color foreground, Color background)[]> undoStack = new();
            
            TabPanel schemesPanel = new TabPanel(ColorSchemeInfo.SchemesList.Select(s => s.ToString()).ToArray())
            {
                X = 0, Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(1)
            };
            Add(schemesPanel);

            var saveButton = new Button()
            {
                X = 0, Y = Pos.Bottom(schemesPanel), Text = "Apply",
            };
            Add(saveButton);
            saveButton.Accepting += (sender, args) =>
            {
                onColorSchemeChanged(info);
                args.Handled = true;
            };

            var undoButton = new Button()
            {
                X = Pos.Right(saveButton) + 1, Y = Pos.Bottom(schemesPanel), Text = "Undo",
            };
            Add(undoButton);
            undoButton.Accepting += (sender, args) =>
            {
                if (undoStack.TryPop(out var result))
                {
                    info.Import(result, true);
                    info.SaveToSchemeManager();
                }
                args.Handled = true;
            };
            
            foreach (var scheme in ColorSchemeInfo.SchemesList)
            {
                var schemeTab = schemesPanel.GetTab(scheme.ToString());
                TabPanel tabs = new TabPanel(PaletteInfo.Aspects.ToArray())
                {
                    X = 0, Y = 0,
                    Width = Dim.Fill(),
                    Height = Dim.Fill()
                };
                schemeTab.Add(tabs);

                foreach (var tabName in PaletteInfo.Aspects.ToArray())
                {
                    var tab = tabs.GetTab(tabName);
                    ColorPicker frontPicker = new ColorPicker()
                    {
                        X = 1, Y = 1, Title = "Front Color",
                    };
                    ColorPicker backPicker = new ColorPicker()
                    {
                        X = 0, Y = Pos.Bottom(frontPicker), Title = "Back Color",
                    };
                    tab.Add(frontPicker, backPicker);

                    var tmpScheme = scheme;
                    var tmpTabName = tabName;
                    var colorPair = info[scheme][tabName];
                    frontPicker.SelectedColor =  colorPair.Foreground;
                    backPicker.SelectedColor =  colorPair.Background;

                    bool noUpdate = false;
                    
                    colorPair.Updated += () =>
                    {
                        noUpdate = true;
                        frontPicker.SelectedColor = colorPair.Foreground;
                        backPicker.SelectedColor = colorPair.Background;
                        noUpdate = false;
                        //Log.i($"Updated data for {tmpScheme} / {tmpTabName}");
                    };

                    void OnFrontOnColorChanged(object? sender, ResultEventArgs<Color> args)
                    {
                        if (noUpdate)
                        {
                            return;
                        }
                        //Log.i($"Updated picker for {tmpScheme} / {tmpTabName}");
                        undoStack.Push(info.Export());
                        colorPair.Update(frontPicker.SelectedColor, backPicker.SelectedColor, false);
                        info.SaveToSchemeManager();
                    }

                    frontPicker.ColorChanged += OnFrontOnColorChanged;
                    backPicker.ColorChanged += OnFrontOnColorChanged;
                }
            }
        }
    }
}