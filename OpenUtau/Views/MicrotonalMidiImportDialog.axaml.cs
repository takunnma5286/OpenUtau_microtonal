using Avalonia.Controls;
using Avalonia.Interactivity;

namespace OpenUtau.App.Views {
    public partial class MicrotonalMidiImportDialog : Window {
        public MicrotonalMidiImportDialog() {
            InitializeComponent();
        }

        void OkClicked(object sender, RoutedEventArgs e) {
            Close(true);
        }

        void CancelClicked(object sender, RoutedEventArgs e) {
            Close(false);
        }
    }
}
