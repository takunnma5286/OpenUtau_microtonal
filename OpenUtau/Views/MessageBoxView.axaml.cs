using System;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using OpenUtau.Core;
using Serilog;

namespace OpenUtau.App.Views {
    public partial class MessageBoxView : UserControl {
        public event EventHandler? Closed;

        public TextBlock? MessageText => this.FindControl<TextBlock>("Text");
        public StackPanel? ContentPanel => this.FindControl<StackPanel>("TextPanel");
        public StackPanel? ButtonPanel => this.FindControl<StackPanel>("Buttons");

        public MessageBoxView() {
            InitializeComponent();
        }

        private void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);
        }

        public void SetText(string text) {
            Dispatcher.UIThread.Post(() => {
                var textBlock = this.FindControl<TextBlock>("Text");
                if (textBlock != null) {
                    textBlock.Text = text;
                }
            });
        }

        public void SetTextWithLink(string text, StackPanel textPanel) {
            // @"http(s)?://([\w-]+\.)+[\w-]+(/[A-Z0-9-.,_/?%&=]*)?"
            var regex = new Regex(@"http(s)?://[^(\r\n|\n| )]+", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var match = regex.Match(text);
            if (match.Success) {
                textPanel.Children.Add(new TextBlock { Text = text.Substring(0, match.Index) });
                var hyperlink = new Button();
                hyperlink.Content = match.Value.Trim();
                hyperlink.Click += OnUrlClick;
                textPanel.Children.Add(hyperlink);

                SetTextWithLink(text.Substring(match.Index + match.Length), textPanel);
            } else {
                if (!string.IsNullOrEmpty(text)) {
                    textPanel.Children.Add(new TextBlock { Text = text });
                }
            }
        }

        private void OnUrlClick(object? sender, RoutedEventArgs e) {
            try {
                if (sender is Button button && button.Content is string url) {
                    OS.OpenWeb(url);
                }
            } catch (Exception ex) {
                Log.Error(ex, "Failed to open url");
            }
        }

        public void Close() {
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }
}
