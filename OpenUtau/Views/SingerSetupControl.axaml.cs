using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenUtau.App.ViewModels;
using OpenUtau.Core;

namespace OpenUtau.App.Views {
    public partial class SingerSetupControl : UserControl {
        public event EventHandler? RequestClose;

        public SingerSetupControl() {
            InitializeComponent();
        }

        void InstallClicked(object sender, RoutedEventArgs arg) {
            var viewModel = DataContext as SingerSetupViewModel;
            if (viewModel == null) {
                return;
            }
            var scheduler = TaskScheduler.FromCurrentSynchronizationContext();
            var task = viewModel.Install();
            task.ContinueWith((task) => {
                if (task.IsFaulted) {
                    DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(new MessageCustomizableException("Failed to install singer.", "<translate:singersetup.failed>", task.Exception)));
                }
            }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, scheduler);
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
    }
}
