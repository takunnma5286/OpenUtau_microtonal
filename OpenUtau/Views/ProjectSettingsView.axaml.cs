using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using OpenUtau.App.ViewModels;
using OpenUtau.Core;
using OpenUtau.Core.Util;

namespace OpenUtau.App.Views {
    public partial class ProjectSettingsView : UserControl {
        public event EventHandler? Closed;

        public ProjectSettingsView() {
            InitializeComponent();
        }

        private void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);
        }

        async void ImportTunClicked(object sender, RoutedEventArgs e) {
            var file = await FilePicker.OpenFileAboutProject(
                TopLevel.GetTopLevel(this)!, "Import Tun", FilePicker.TUN);
            if (!string.IsNullOrEmpty(file) && DataContext is ProjectSettingsViewModel vm) {
                try {
                    var config = TunFile.Load(file);
                    vm.ConcertPitch = config.ConcertPitch;
                    vm.ConcertPitchNote = config.ConcertPitchNote;
                    vm.TuningMap = config.TuningMap;
                    vm.IsTunfile = true;
                } catch (Exception ex) {
                    DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(ex));
                }
            }
        }

        void OkClicked(object sender, RoutedEventArgs e) {
            (DataContext as ProjectSettingsViewModel)?.Apply();
            Closed?.Invoke(this, EventArgs.Empty);
        }

        void CancelClicked(object sender, RoutedEventArgs e) {
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }
}
