using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using OpenUtau.App.ViewModels;

namespace OpenUtau.App.Views {
    public partial class MainWindow : Window {
        public MainWindow() {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        public void InitProject() {
            this.FindControl<MainView>("MainView")?.InitProject();
        }

        private void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);
        }

        public void WindowClosing(object? sender, WindowClosingEventArgs e) {
            // Delegate closing logic to MainView if needed, or handle it here
            // MainView attaches to WindowClosing in OnLoaded, so we don't need to do much here?
            // Actually, MainView logic handles everything.
            // But we need to ensure MainView is initialized.
        }
    }
}
