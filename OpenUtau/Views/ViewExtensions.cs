using System.Threading.Tasks;
using Avalonia.Controls;

namespace OpenUtau.App.Views {
    public static class ViewExtensions {
        public static Task ShowDialogSafeAsync(this Window dialog, Control parent) {
            var top = TopLevel.GetTopLevel(parent);
            if (top is Window w) {
                return dialog.ShowDialog(w);
            } else {
                var tcs = new TaskCompletionSource<object?>();
                dialog.Closed += (s, e) => tcs.TrySetResult(null);
                dialog.Show();
                return tcs.Task;
            }
        }
    }
}
