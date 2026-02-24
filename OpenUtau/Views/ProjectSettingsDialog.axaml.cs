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
            var view = this.FindControl<ProjectSettingsView>("View");
            if (view != null) {
                view.Closed += (s, e) => Close();
            }
        }
    }
}
