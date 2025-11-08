using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using ReactiveUI.Fody.Helpers;

namespace OpenUtau.App.ViewModels {
    public class ProjectSettingsViewModel : ViewModelBase {
        [Reactive] public int EqualTemperament { get; set; }
        [Reactive] public double ConcertPitch { get; set; }
        [Reactive] public int ConcertPitchNote { get; set; }

        private UProject project;

        public ProjectSettingsViewModel(UProject project) {
            this.project = project;
            EqualTemperament = project.EqualTemperament;
            ConcertPitch = project.ConcertPitch;
            ConcertPitchNote = project.ConcertPitchNote;
        }

        public void Apply() {
            if (project.EqualTemperament != EqualTemperament ||
                project.ConcertPitch != ConcertPitch ||
                project.ConcertPitchNote != ConcertPitchNote) {
                DocManager.Inst.StartUndoGroup();
                DocManager.Inst.ExecuteCmd(new ConfigureProjectCommand(
                    project,
                    newEqualTemperament: EqualTemperament,
                    newConcertPitch: ConcertPitch,
                    newConcertPitchNote: ConcertPitchNote));
                DocManager.Inst.EndUndoGroup();
            }
        }
    }

    public class ConfigureProjectCommand : UCommand {
        private readonly UProject project;
        private readonly int newEqualTemperament;
        private readonly double newConcertPitch;
        private readonly int newConcertPitchNote;
        private readonly int oldEqualTemperament;
        private readonly double oldConcertPitch;
        private readonly int oldConcertPitchNote;

        public ConfigureProjectCommand(UProject project, int newEqualTemperament, double newConcertPitch, int newConcertPitchNote) {
            this.project = project;
            this.newEqualTemperament = newEqualTemperament;
            this.newConcertPitch = newConcertPitch;
            this.newConcertPitchNote = newConcertPitchNote;
            this.oldEqualTemperament = project.EqualTemperament;
            this.oldConcertPitch = project.ConcertPitch;
            this.oldConcertPitchNote = project.ConcertPitchNote;
        }

        public override string ToString() => "Configure project";
        public override void Execute() {
            project.EqualTemperament = newEqualTemperament;
            project.ConcertPitch = newConcertPitch;
            project.ConcertPitchNote = newConcertPitchNote;
        }
        public override void Unexecute() {
            project.EqualTemperament = oldEqualTemperament;
            project.ConcertPitch = oldConcertPitch;
            project.ConcertPitchNote = oldConcertPitchNote;
        }
    }
}
