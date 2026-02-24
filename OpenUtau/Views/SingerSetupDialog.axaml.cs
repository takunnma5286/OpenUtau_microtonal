using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenUtau.App.ViewModels;
using OpenUtau.Core;

namespace OpenUtau.App.Views {
    public partial class SingerSetupDialog : Window {
        public SingerSetupDialog() {
            InitializeComponent();
            var control = this.FindControl<SingerSetupControl>("Control");
            if (control != null) {
                control.RequestClose += (_, _) => Close();
            }
        }
    }
}
