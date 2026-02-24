using System;
using Avalonia;
using Avalonia.Controls;
using OpenUtau.App.ViewModels;
using OpenUtau.Core;
using OpenUtau.Core.Util;

namespace OpenUtau.App.Views {
    public partial class PianoRollWindow : Window {
        public MainView? MainWindow {
            get => PianoRoll.MainWindow;
            set => PianoRoll.MainWindow = value;
        }

        public PianoRollViewModel ViewModel => PianoRoll.ViewModel;

        public PianoRollWindow() {
            InitializeComponent();
        }

        public PianoRollWindow(PianoRollViewModel model) {
            InitializeComponent();
            PianoRoll.DataContext = model;
            DataContext = model;

            if (Preferences.Default.PianorollWindowSize.TryGetPosition(out int x, out int y)) {
                Position = new PixelPoint(x, y);
            }
            WindowState = (WindowState)Preferences.Default.PianorollWindowSize.State;
        }

        public void InitializePianoRollWindowAsync() {
            PianoRoll.InitializePianoRollWindowAsync();
        }

        void WindowClosing(object? sender, WindowClosingEventArgs e) {
            if (WindowState != WindowState.Maximized) {
                Preferences.Default.PianorollWindowSize.Set(Width, Height, Position.X, Position.Y, (int)WindowState);
            }
            Hide();
            e.Cancel = true;
        }

        void WindowDeactivated(object sender, EventArgs args) {
            PianoRoll.WindowDeactivated(sender, args);
        }

        public void AttachExpressions() {
            PianoRoll.AttachExpressions();
        }
    }
}

