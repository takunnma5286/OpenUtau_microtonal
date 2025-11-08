using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenUtau.App.ViewModels;

namespace OpenUtau.App.Views {
    public partial class ProjectSettingsDialog : Window {
        public ProjectSettingsDialog() {
            InitializeComponent();
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
