using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenUtau.App.ViewModels;
using OpenUtau.Core;
using OpenUtau.Core.Util;

namespace OpenUtau.App.Views {
    public partial class ProjectSettingsDialog : Window {
        public ProjectSettingsDialog() {
            InitializeComponent();
        }

        async void ImportTunClicked(object sender, RoutedEventArgs e) {
            var file = await FilePicker.OpenFileAboutProject(
                this, "Import Tun", FilePicker.TUN);
            if (!string.IsNullOrEmpty(file) && DataContext is ProjectSettingsViewModel vm) {
                try {
                    var config = TunFile.Load(file);
                    vm.ConcertPitch = config.ConcertPitch;
                    vm.ConcertPitchNote = config.ConcertPitchNote;
                    vm.TuningMap = config.TuningMap;
                    // Auto-calculate EqualTemperament if possible, or just keep it?
                    // TunFile.Load sets ConcertPitch.
                    // But EqualTemperament might be implied 12 if not specified, 
                    // but TunFile.Load returns MicrotonalConfig which includes EqualTemperament (default 12).
                    // Wait, TunFile.Load initializes MicrotonalConfig.
                    // Let's check MicrotonalConfig in MusicMath again.
                    // It doesn't seem to parse ET from Tun file generally?
                    // Tun files usually imply 12ET base but with deviations.
                    // The Load method I wrote earlier:
                    /*
                    var config = new MicrotonalConfig();
                    config.TuningMap = new double[128];
                    for (int i = 0; i < 128; i++) {
                         config.TuningMap[i] = 440.0 * Math.Pow(2, (i - 69) / 12.0);
                    }
                    ...
                    */
                    // It defaults ET to 12.
                    // So we should maybe not change vm.EqualTemperament unless Tun file specifies it (which standard .tun doesn't).
                    // Or maybe we should?
                    // If the user imports a .tun, they usually want the specific frequencies.
                    // The TuningMap overrides ET for math usually.

                } catch (Exception ex) {
                    DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(ex));
                }
            }
        }

        void OkClicked(object sender, RoutedEventArgs e) {
            (DataContext as ProjectSettingsViewModel)?.Apply();
            Close();
        }

        void CancelClicked(object sender, RoutedEventArgs e) {
            Close();
        }
    }
}
