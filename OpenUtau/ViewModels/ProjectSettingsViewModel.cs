using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using ReactiveUI.Fody.Helpers;

namespace OpenUtau.App.ViewModels {
    public class ProjectSettingsViewModel : ViewModelBase {
        [Reactive] public int EqualTemperament { get; set; }
        [Reactive] public double ConcertPitch { get; set; }
        [Reactive] public int ConcertPitchNote { get; set; }
        [Reactive] public double[]? TuningMap { get; set; }

        private UProject project;

        public ProjectSettingsViewModel(UProject project) {
            this.project = project;
            EqualTemperament = project.EqualTemperament;
            ConcertPitch = project.ConcertPitch;
            ConcertPitchNote = project.ConcertPitchNote;
            TuningMap = project.TuningMap;
        }

        public void Apply() {
            if (project.EqualTemperament != EqualTemperament ||
                project.ConcertPitch != ConcertPitch ||
                project.ConcertPitchNote != ConcertPitchNote ||
                project.TuningMap != TuningMap) {
                DocManager.Inst.StartUndoGroup();
                DocManager.Inst.ExecuteCmd(new ConfigureMicrotonalCommand(
                    project,
                    EqualTemperament,
                    ConcertPitch,
                    ConcertPitchNote,
                    TuningMap));
                DocManager.Inst.EndUndoGroup();
            }
        }
    }
}
