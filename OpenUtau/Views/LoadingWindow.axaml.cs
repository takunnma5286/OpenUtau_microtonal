using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;

using Avalonia.Threading;

namespace OpenUtau.App.Views {
    public partial class LoadingWindow : Window {
        private static LoadingWindow? loadingDialog;
        private static bool isCurrentlyLoading = false;
        private static CancellationTokenSource? globalLoadingCancellationTokenSource;

        public LoadingWindow() {
            InitializeComponent();
        }

        public static void InitializeLoadingWindow() {
            loadingDialog = new LoadingWindow() {
                Title = "Loading"
            };
            loadingDialog.Text.Text = "Loading...";
        }

        private static void ShowLoadingWindow(Control parent) {
            if (!isCurrentlyLoading) {
                return;
            }

            if (loadingDialog == null) {
                InitializeLoadingWindow();
            }

            loadingDialog?.ShowDialogSafeAsync(parent);
        }

        private static void CloseLoadingWindow() {
            if (loadingDialog != null) {
                loadingDialog.Close();

                //Recreate loading dialog to make sure it is initialized before being shown
                loadingDialog = null;
                InitializeLoadingWindow();
            }
        }

        public static bool IsLoading() {
            return isCurrentlyLoading;
        }

        /// <summary>
        /// Returns a task that shows a loading popup for a task after a time delay (Default 250ms)
        /// </summary>
        public static async Task LoadForAsyncTask(Task loadingTask, Control parentWindow, int timeBeforeLoadingPopup = 250) {
            CancellationTokenSource cts = new CancellationTokenSource();
            Task loadingPopupTask = ShowLoadingAfterTime(cts.Token, parentWindow, 1);

            await loadingTask;

            // Cancel loading box creation task early once window successfully created
            cts.Cancel();
            // Close loading box if opened
            CloseLoadingWindow();
            isCurrentlyLoading = false;
        }

        /// <summary>
        /// Returns a task that shows a loading popup for a task after a time delay (Default 250ms)
        /// </summary>
        public static async Task<T> LoadForAsyncTask<T>(Task<T> loadingTask, Control parentWindow, int timeBeforeLoadingPopup = 250) {
            CancellationTokenSource cts = new CancellationTokenSource();
            Task loadingPopupTask = ShowLoadingAfterTime(cts.Token, parentWindow, 1);

            await loadingTask;

            // Cancel loading box creation task early once window successfully created
            cts.Cancel();
            // Close loading box if opened
            CloseLoadingWindow();
            isCurrentlyLoading = false;

            return loadingTask.Result;
        }

        private static async Task ShowLoadingAfterTime(CancellationToken ct, Control parentWindow, int milisDelay) {
            // Must have at least 1ms of delay opening loading window to avoid strange race conditions with UI thread (?)
            await Task.Delay(Math.Max(milisDelay, 1), ct);

            if (ct.IsCancellationRequested) {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() => ShowLoadingWindow(parentWindow));
        }

        public static async Task RunAsyncOnUIThread(Action func) {
            await Dispatcher.UIThread.InvokeAsync(func);
        }

        public static void BeginLoadingImmediate(Control parentWindow) {
            BeginLoading(parentWindow, 0);
        }

        public static void BeginLoading(Control parentWindow, int milisDelay = 250) {
            if (isCurrentlyLoading) {
                return;
            }

            isCurrentlyLoading = true;
            if (milisDelay == 0) {
                ShowLoadingWindow(parentWindow);
                return;
            }

            globalLoadingCancellationTokenSource = new CancellationTokenSource();
            Task.Run(() => ShowLoadingAfterTime(globalLoadingCancellationTokenSource.Token, parentWindow, milisDelay), globalLoadingCancellationTokenSource.Token);
        }

        public static void EndLoading() {
            if (!isCurrentlyLoading) {
                return;
            }
            isCurrentlyLoading = false;

            globalLoadingCancellationTokenSource?.Cancel();
            CloseLoadingWindow();
        }
    }
}
