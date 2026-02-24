using Avalonia.Controls;
using Avalonia.Interactivity;

namespace OpenUtau.App.Views {
    public partial class MicrotonalMidiImportDialog : Window {
        public MicrotonalMidiImportDialog() {
            InitializeComponent();
        }

        public bool DialogResult { get; private set; } = false;

        void OkClicked(object sender, RoutedEventArgs e) {
            DialogResult = true;
            Close();
        }

        void CancelClicked(object sender, RoutedEventArgs e) {
            DialogResult = false;
            Close();
        }
    }
}
