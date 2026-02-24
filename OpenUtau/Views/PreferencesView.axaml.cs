using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using OpenUtau.App.ViewModels;
using OpenUtau.Core;

using Avalonia.Markup.Xaml;

namespace OpenUtau.App.Views {
    public partial class PreferencesView : UserControl {
        private PreferencesViewModel? viewModel => this.DataContext as PreferencesViewModel;

        public PreferencesView() {
            InitializeComponent();
        }

        private void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);
        }

        void OpenSingersFolder(object sender, RoutedEventArgs e) {
            try {
                Directory.CreateDirectory(viewModel!.SingerPath);
                OS.OpenFolder(viewModel!.SingerPath);
            } catch (Exception ex) {
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(ex));
            }
        }

        void OpenAddlSingersFolder(object sender, RoutedEventArgs e) {
            try {
                if (Directory.Exists(viewModel!.AdditionalSingersPath)) {
                    OS.OpenFolder(viewModel!.AdditionalSingersPath);
                }
            } catch (Exception ex) {
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(ex));
            }
        }

        void ResetAddlSingersPath(object sender, RoutedEventArgs e) {
            viewModel!.SetAddlSingersPath(string.Empty);
        }

        async void SelectAddlSingersPath(object sender, RoutedEventArgs e) {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;
            Action<string> logger = msg => {
                if (viewModel != null) {
                    viewModel.ImportLog += msg + "\n";
                    var box = this.FindControl<TextBox>("ImportLogBox");
                    if (box != null) {
                        box.CaretIndex = int.MaxValue;
                    }
                }
            };
            var path = await FilePicker.OpenFolderAboutSinger(topLevel, "prefs.paths.addlsinger", logger);
            if (string.IsNullOrEmpty(path)) {
                return;
            }
            if (Directory.Exists(path)) {
                viewModel!.SetAddlSingersPath(path);
            }
        }

        async void ReloadSingers(object sender, RoutedEventArgs e) {
            LoadingWindow.BeginLoading(this);
            await Task.Run(() => {
                SingerManager.Inst.SearchAllSingers();
            });
            DocManager.Inst.ExecuteCmd(new SingersRefreshedNotification());
            LoadingWindow.EndLoading();
        }

        void ResetVLabelerPath(object sender, RoutedEventArgs e) {
            viewModel!.SetVLabelerPath(string.Empty);
        }

        async void SelectVLabelerPath(object sender, RoutedEventArgs e) {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;
            var type = OS.IsWindows() ? FilePicker.EXE : OS.IsMacOS() ? FilePicker.APP : FilePickerFileTypes.All;
            var path = await FilePicker.OpenFile(topLevel, "prefs.advanced.vlabelerpath", type);
            if (string.IsNullOrEmpty(path)) {
                return;
            }
            if (OS.AppExists(path)) {
                viewModel!.SetVLabelerPath(path);
            }
        }

        void ResetSetParamPath(object sender, RoutedEventArgs e) {
            viewModel!.SetSetParamPath(string.Empty);
        }

        async void SelectSetParamPath(object sender, RoutedEventArgs e) {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;
            var path = await FilePicker.OpenFile(topLevel, "prefs.otoeditor.setparampath", FilePicker.EXE);
            if (string.IsNullOrEmpty(path)) {
                return;
            }
            if (File.Exists(path)) {
                viewModel!.SetSetParamPath(path);
            }
        }

        void ResetWinePath(object sender, RoutedEventArgs e) {
            ((PreferencesViewModel)DataContext!).SetWinePath(string.Empty);
        }

        async void SelectWinePath(object sender, RoutedEventArgs e) {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;
            var path = await FilePicker.OpenFile(topLevel, "prefs.advanced.winepath", FilePicker.UnixExecutable);
            if (string.IsNullOrEmpty(path)) {
                return;
            }
            if (File.Exists(path)) {
                ((PreferencesViewModel)DataContext!).SetWinePath(path);
            }
        }

        void DetectWinePath(object sender, RoutedEventArgs e) {
            string[] wineNames = { "wine", "wine64", "wine32", "wine32on64" };
            string winePath = string.Empty;

            foreach (string wineName in wineNames) {
                winePath = OS.WhereIs(wineName);
                if (!string.IsNullOrEmpty(winePath)) {
                    break;
                }
            }

            if (string.IsNullOrEmpty(winePath)) {
                return;
            }

            ((PreferencesViewModel)DataContext!).SetWinePath(winePath);
        }
    }
}
