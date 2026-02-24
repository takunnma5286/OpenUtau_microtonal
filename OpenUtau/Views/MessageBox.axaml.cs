using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OpenUtau.Core;
using Serilog;

namespace OpenUtau.App.Views {
    public partial class MessageBox : Window {
        public enum MessageBoxButtons { Ok, OkCancel, YesNo, YesNoCancel, OkCopy }
        public enum MessageBoxResult { Ok, Cancel, Yes, No }

        public MessageBox() {
            InitializeComponent();
        }

        private void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);
        }

        private MessageBoxView? GetView() {
            return this.FindControl<MessageBoxView>("View");
        }

        public void SetText(string text) {
            GetView()?.SetText(text);
        }

        public static Task<MessageBoxResult> ShowError(Control parent, Exception? e, string message = "", bool fromNotif = false) {
            string text = message;
            string title = ThemeManager.GetString("errors.caption");

            var builder = new StringBuilder();
            if (e != null) {
                if (e is AggregateException ae && ae.Flatten().InnerExceptions.Count == 1) {
                    e = ae.InnerExceptions.First();
                }

                if (e is MessageCustomizableException mce) {
                    text = Translate(mce);
                    builder.AppendLine(mce.SubstanceException.Message);
                    builder.AppendLine();
                    builder.Append(mce.SubstanceException.ToString());
                    if (!mce.ShowStackTrace) {
                        return Show(parent, text, title, MessageBoxButtons.Ok);
                    }
                } else if (e is AggregateException nestedAe) {
                    foreach (var ie in nestedAe.Flatten().InnerExceptions) {
                        if (!string.IsNullOrWhiteSpace(text)) {
                            text += "\n";
                        }
                        if (ie is MessageCustomizableException innnerMce) {
                            text += Translate(innnerMce);
                            builder.AppendLine(innnerMce.SubstanceException.Message);
                            builder.AppendLine();
                            builder.Append(innnerMce.SubstanceException.ToString());
                        } else {
                            text += ie.Message;
                            builder.AppendLine(ie.Message);
                            builder.AppendLine();
                            builder.AppendLine(ie.ToString());
                        }
                        builder.AppendLine();
                    }
                } else {
                    builder.AppendLine(e.Message);
                    builder.AppendLine();
                    builder.Append(e.ToString());
                    if (string.IsNullOrEmpty(text)) {
                        text = e.Message;
                    }
                }
            }
            builder.AppendLine();
            builder.AppendLine();
            builder.AppendLine(System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "Unknown Version");

            return Show(parent, text, title, MessageBoxButtons.OkCopy, builder.ToString());

            string Translate(MessageCustomizableException mce) {
                string text;
                if (string.IsNullOrWhiteSpace(mce.TranslatableMessage)) {
                    text = mce.Message;
                } else {
                    text = mce.TranslatableMessage;
                    try {
                        var matches = System.Text.RegularExpressions.Regex.Matches(mce.TranslatableMessage, "<translate:(.*?)>");
                        foreach (System.Text.RegularExpressions.Match match in matches) {
                            if (ThemeManager.TryGetString(match.Groups[1].Value, out string translated)) {
                                text = text.Replace(match.Value, translated);
                            } else {
                                text = mce.Message;
                                break;
                            }
                        }
                    } catch {
                        text = mce.Message;
                    }
                }

                if (mce.Replaces != null && mce.Replaces.Length > 0) {
                    return string.Format(text, mce.Replaces);
                } else {
                    return text;
                }
            }
        }

        public static Task<MessageBoxResult> Show(Control parent, string text, string title, MessageBoxButtons buttons, string? stackTrace = null) {
            MessageBoxView? view = null;
            Window? window = null;

            if (OS.IsWasm()) {
                view = new MessageBoxView();
            } else {
                var msgBox = new MessageBox();
                msgBox.Title = title;
                window = msgBox;
                view = msgBox.GetView();
            }

            if (view == null) return Task.FromResult(MessageBoxResult.Cancel);

            var textBlock = view.MessageText;
            var textPanel = view.ContentPanel;
            var buttonPanel = view.ButtonPanel;

            if (textBlock != null) textBlock.IsVisible = false;
            if (textPanel != null) view.SetTextWithLink(text, textPanel);

            if (stackTrace != null && textPanel != null) {
                var stackTracePanel = new StackPanel();
                var expander = new Expander() { Header = ThemeManager.GetString("errors.details"), Content = stackTracePanel };
                textPanel.Children.Add(expander);
                view.SetTextWithLink(stackTrace, stackTracePanel);
            }

            var res = MessageBoxResult.Ok;

            void AddButton(string caption, MessageBoxResult r, bool def = false) {
                var btn = new Button { Content = caption };
                btn.Click += (_, __) => {
                    res = r;
                    if (window != null) window.Close();
                    else view.Close();
                };
                buttonPanel?.Children.Add(btn);
                if (def)
                    res = r;
            }

            if (buttons == MessageBoxButtons.Ok || buttons == MessageBoxButtons.OkCancel || buttons == MessageBoxButtons.OkCopy)
                AddButton(ThemeManager.GetString("dialogs.messagebox.ok"), MessageBoxResult.Ok, true);
            if (buttons == MessageBoxButtons.YesNo || buttons == MessageBoxButtons.YesNoCancel) {
                AddButton(ThemeManager.GetString("dialogs.messagebox.yes"), MessageBoxResult.Yes);
                AddButton(ThemeManager.GetString("dialogs.messagebox.no"), MessageBoxResult.No, true);
            }

            if (buttons == MessageBoxButtons.OkCancel || buttons == MessageBoxButtons.YesNoCancel)
                AddButton(ThemeManager.GetString("dialogs.messagebox.cancel"), MessageBoxResult.Cancel, true);
            if (buttons == MessageBoxButtons.OkCopy) {
                var btn = new Button { Content = ThemeManager.GetString("dialogs.messagebox.copy") };
                btn.Click += (_, __) => {
                    try {
                        TopLevel.GetTopLevel(parent)?.Clipboard?.SetTextAsync(text + "\n" + stackTrace);
                    } catch { }
                };
                buttonPanel?.Children.Add(btn);
            }

            var tcs = new TaskCompletionSource<MessageBoxResult>();

            if (window != null) {
                window.Closed += delegate { tcs.TrySetResult(res); };
                if (parent != null)
                    window.ShowDialogSafeAsync(parent);
                else window.Show();
            } else {
                // WASM Logic
                var closeOverlay = ShowInOverlay(parent, view);
                view.Closed += delegate {
                    tcs.TrySetResult(res);
                    closeOverlay();
                };
            }

            return tcs.Task;
        }

        public static MessageBox? ShowModal(Control parent, string text, string title) {
            if (OS.IsWasm()) {
                // Modal not supported in same way on WASM, treat as async Show but return null or dummy?
                // Or better, just show it.
                // Since this method returns MessageBox object, and we can't created it on WASM...
                // existing calls expect a return value to close it later (msgbox.Close()).
                // We'll have to return null on WASM or refactor callers.
                // Refactoring callers is safer. But for now, let's just show it and return null.
                var view = new MessageBoxView();
                view.SetText(text);
                // We can't really control it from outside if we return null.
                // This is used in RegenFrq for progress.
                ShowInOverlay(parent, view);
                return null;
            }

            var msgbox = new MessageBox() {
                Title = title
            };
            msgbox.GetView()?.SetText(text);
            if (parent != null)
                msgbox.ShowDialogSafeAsync(parent);
            else msgbox.Show();
            return msgbox;
        }

        public static Task<MessageBoxResult> ShowProcessing(
               Control parent,
               string text,
               string title,
               Action<MessageBox?, CancellationToken> action, // Allow null MessageBox for WASM
               Action<Task>? onFinished = null) {

            MessageBox? msgbox = null;
            MessageBoxView? view = null;

            if (OS.IsWasm()) {
                view = new MessageBoxView();
                view.SetText(text);
            } else {
                msgbox = new MessageBox() { Title = title };
                msgbox.GetView()?.SetText(text);
            }

            var res = MessageBoxResult.Ok;
            var tokenSource = new CancellationTokenSource();

            var scheduler = TaskScheduler.FromCurrentSynchronizationContext();
            var task = Task.Run(() => {
                action.Invoke(msgbox, tokenSource.Token); // Requires refactoring action signature if msgbox is null on WASM
                return res;
            }, tokenSource.Token);
            task.ContinueWith(t => {
                if (msgbox != null) msgbox.Close();
                else view?.Close();

                if (onFinished != null) {
                    onFinished(task);
                }
            }, scheduler);

            var btn = new Button { Content = ThemeManager.GetString("dialogs.messagebox.cancel") };
            btn.Click += (_, __) => {
                if (msgbox != null) msgbox.Close();
                else view?.Close();
            };

            if (msgbox != null) {
                msgbox.GetView()?.ButtonPanel?.Children.Add(btn);
                msgbox.Closed += delegate {
                    if (task.IsCompleted) return;
                    res = MessageBoxResult.Cancel;
                    tokenSource.Cancel();
                };
                if (parent != null) msgbox.ShowDialogSafeAsync(parent);
                else msgbox.Show();
            } else if (view != null) {
                view.ButtonPanel?.Children.Add(btn);
                var closeOverlay = ShowInOverlay(parent, view);
                view.Closed += delegate {
                    if (task.IsCompleted) {
                        closeOverlay();
                        return;
                    }
                    res = MessageBoxResult.Cancel;
                    tokenSource.Cancel();
                    closeOverlay();
                };
            }

            return task;
        }

        public static Action ShowInOverlay(Control parent, Control view) {
            var topLevel = TopLevel.GetTopLevel(parent);
            var overlay = topLevel?.GetVisualDescendants().OfType<ContentControl>().FirstOrDefault(c => c.Name == "DialogOverlay");

            if (overlay != null) {
                overlay.IsVisible = true;
                Panel? container = overlay.Content as Panel;
                if (container == null) {
                    container = new Grid();
                    overlay.Content = container;
                }

                // Ensure view is added to the container
                if (!container.Children.Contains(view)) {
                    container.Children.Add(view);
                }

                return () => {
                    if (container.Children.Contains(view)) {
                        container.Children.Remove(view);
                    }
                    if (container.Children.Count == 0) {
                        overlay.IsVisible = false;
                    }
                };
            } else {
                // Console.WriteLine("CRITICAL: DialogOverlay not found for MessageBox!");
                return () => { };
            }
        }

        public class ProgressDialogController {
            private MessageBoxView? view;
            private MessageBox? window;
            private Control? overlay;

            public ProgressDialogController(MessageBoxView? view, Control? overlay) {
                this.view = view;
                this.overlay = overlay;
            }

            public ProgressDialogController(MessageBox window) {
                this.window = window;
            }

            public void SetText(string text) {
                Dispatcher.UIThread.Post(() => {
                    view?.SetText(text);
                    window?.SetText(text);
                });
            }

            public void Close() {
                Dispatcher.UIThread.Post(() => {
                    if (window != null) window.Close();
                    else if (view != null) {
                        view.Close();

                        if (overlay is ContentControl cc && cc.Content is Panel container) {
                            var parent = view.Parent as Control;
                            if (parent != null && container.Children.Contains(parent)) {
                                container.Children.Remove(parent);
                            } else if (container.Children.Contains(view)) {
                                container.Children.Remove(view);
                            }

                            if (container.Children.Count == 0) {
                                cc.IsVisible = false;
                            }
                        }
                    }
                });
            }
        }

        public static ProgressDialogController ShowProgress(Control? parent, string title, string initialText) {
            if (OS.IsWasm()) {
                if (parent == null) return new ProgressDialogController(null, null);

                var view = new MessageBoxView();
                view.SetText(initialText);

                var topLevel = TopLevel.GetTopLevel(parent);
                var overlay = topLevel?.GetVisualDescendants().OfType<ContentControl>().FirstOrDefault(c => c.Name == "DialogOverlay");

                if (overlay != null) {
                    overlay.IsVisible = true;
                    Panel? container = overlay.Content as Panel;
                    if (container == null) {
                        container = new Grid();
                        overlay.Content = container;
                    }

                    // Wrap in Grid to center it
                    var grid = new Grid();
                    grid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
                    Grid.SetRow(view, 0);
                    grid.Children.Add(view);

                    container.Children.Add(grid);

                    return new ProgressDialogController(view, overlay);
                }
                return new ProgressDialogController(null, null);
            } else {
                var msgbox = new MessageBox() { Title = title };
                msgbox.SetText(initialText);
                if (parent != null) {
                    var win = parent as Window ?? Window.GetTopLevel(parent) as Window;
                    if (win != null) msgbox.Show(win);
                    else msgbox.Show();
                } else {
                    msgbox.Show();
                }
                return new ProgressDialogController(msgbox);
            }
        }
    }
}
