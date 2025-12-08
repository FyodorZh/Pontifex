// using System;
// using System.Collections.Generic;
// using Terminal.Gui;
// using Terminal.Gui.App;
// using Terminal.Gui.Drivers;
// using Terminal.Gui.ViewBase;
// using Terminal.Gui.Views;
// using Terminal.UICommon;
//
// namespace Pontifex.UI
// {
//     public class ConsoleView : View<IConsolePresenter, View>
//     {
//         private readonly Dictionary<string, (TabPanel.PanelView, LoggerView)> _taggedLogs = new();
//
//         private readonly TabPanel _tabs = new();
//
//         private IConsole _console = null!;
//         private int _commandDepth = 0;
//
//         private readonly List<(string tag, LogSeverity severity, string msg)> _pendingLogs = new();
//
//         protected override void OnAttached()
//         {
//             Win.ColorScheme = Colors.ColorSchemes["Base"];
//             Win.CanFocus = false;
//             Win.Title = "Logs";
//             Win.X = 0;
//             Win.Y = 1;
//             Win.Width = Dim.Fill();
//             Win.Height = Dim.Fill();
//
//             _tabs.Width = Dim.Fill();
//             _tabs.Height = Dim.Fill();
//             Win.Add(_tabs);
//
//             AddLogTab("Global");
//             AddScriptingTab();
//
//             Presenter.NewGlobalLog += OnGlobalLog;
//             Presenter.NewTagLog += OnTagLog;
//         }
//
//         private void AddLogTab(string name)
//         {
//             _tabs.AddTab(name);
//             var tab = _tabs.GetTab(name);
//
//             LoggerView loggerView = new LoggerView()
//             {
//                 X = 0, Y = 0,
//                 Width = Dim.Fill(),
//                 Height = Dim.Fill()
//             };
//             tab.Add(loggerView);
//
//             Disposables.Add(tab.IsActive.SubscribeUI(isActive =>
//             {
//                 if (isActive)
//                 {
//                     var newName = tab.Name.TrimStart('*');
//                     if (newName != tab.Name)
//                     {
//                         tab.SetName(newName);
//                     }
//                 }
//             }));
//
//             _taggedLogs.Add(name, (tab, loggerView));
//         }
//
//         private void AddScriptingTab()
//         {
//             string name = "Scripting";
//             _tabs.AddTab(name);
//             var tab = _tabs.GetTab(name);
//
//             LoggerView loggerView = new LoggerView()
//             {
//                 X = 0, Y = 0,
//                 Width = Dim.Fill(),
//                 Height = Dim.Fill(1)
//             };
//             tab.Add(loggerView);
//
//             Disposables.Add(tab.IsActive.SubscribeUI(isActive =>
//             {
//                 if (isActive)
//                 {
//                     _console = Presenter.ManiConsole;
//
//                     TextField cmdField = new TextField()
//                     {
//                         X = 0, Y = Pos.Bottom(loggerView), Width = Dim.Fill()
//                     };
//                     tab.Add(cmdField);
//                     cmdField.KeyDown += (sender, args) =>
//                     {
//                         if (args.KeyCode == KeyCode.Enter)
//                         {
//                             args.Handled = true;
//                             string cmd = cmdField.Text;
//                             cmdField.Text = "";
//                             _commandDepth = 0;
//                             _console.ExecuteCommand(cmd).ContinueWithUI(t =>
//                             {
//                                 if (t.IsCompletedSuccessfully)
//                                 {
//                                     loggerView.AddLog(LogSeverity.Info, cmd);
//                                     loggerView.AddLog(LogSeverity.Info, t.Result.ToString()!);
//                                 }
//                             });
//                         }
//                         else if (args.KeyCode == KeyCode.CursorUp)
//                         {
//                             args.Handled = true;
//                             _commandDepth = Math.Min(_commandDepth + 1, _console.CommandsHistory.Count - 1);
//                             cmdField.Text = _console.CommandsHistory.Count > 0 ? _console.CommandsHistory[_console.CommandsHistory.Count - 1 - _commandDepth].command : "";
//                         }
//                         else if (args.KeyCode == KeyCode.CursorDown)
//                         {
//                             args.Handled = true;
//                             _commandDepth = Math.Max(_commandDepth - 1, -1);
//                             cmdField.Text = _commandDepth < 0 ? "" : _console.CommandsHistory[_console.CommandsHistory.Count - 1 - _commandDepth].command;
//                         }
//                     };
//                 }
//             }));
//
//             _taggedLogs.Add(name, (tab, loggerView));
//         }
//
//         protected override void OnClosed()
//         {
//             Presenter.NewGlobalLog -= OnGlobalLog;
//             Presenter.NewTagLog -= OnTagLog;
//         }
//
//         private void OnGlobalLog(LogSeverity severity, string msg)
//         {
//             OnTagLog("Global", severity, msg);
//         }
//
//         private void OnTagLog(string tag, LogSeverity severity, string msg)
//         {
//             lock (_pendingLogs)
//             {
//                 _pendingLogs.Add((tag, severity, msg));
//                 if (_pendingLogs.Count == 1)
//                 {
//                     Application.Invoke(() =>
//                     {
//                         (string tag, LogSeverity severity, string msg)[] list;
//                         lock (_pendingLogs)
//                         {
//                             list = _pendingLogs.ToArray();
//                             _pendingLogs.Clear();
//                         }
//
//                         foreach (var item in list)
//                         {
//                             var tag = item.tag;
//                             var severity = item.severity;
//                             var msg = item.msg;
//
//                             if (!_taggedLogs.TryGetValue(tag, out var pair))
//                             {
//                                 AddLogTab(tag);
//                                 pair = _taggedLogs[tag];
//                             }
//
//                             (TabPanel.PanelView panel, LoggerView logger) = pair;
//
//                             string name = tag + "#" + (logger.Count + 1);
//                             if (!panel.IsActive.Value)
//                             {
//                                 name = "*" + name;
//                             }
//
//                             panel.SetName(name);
//                             logger.AddLog(severity, msg);
//                         }
//                     });
//                 }
//             }
//         }
//     }
// }