using System;
using System.Reactive.Linq;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace OpenUtau.App.ViewModels {
    public class ProjectSettingsViewModel : ViewModelBase {
        [Reactive] public int EqualTemperament { get; set; }
        [Reactive] public double ConcertPitch { get; set; }
        [Reactive] public int ConcertPitchNote { get; set; }
        [Reactive] public double[]? TuningMap { get; set; }

        private bool _isTunfile;
        public bool IsTunfile {
            get => _isTunfile;
            set {
                if (value != _isTunfile) {
                    this.RaiseAndSetIfChanged(ref _isTunfile, value);
                    this.RaisePropertyChanged(nameof(IsET));
                    if (!_isTunfile) {
                        if (TuningMap != null) {
                            _cachedTuningMap = TuningMap;
                        }
                        TuningMap = null;
                    } else {
                        if (_cachedTuningMap != null) {
                            TuningMap = _cachedTuningMap;
                        }
                    }
                }
            }
        }
        public bool IsET {
            get => !_isTunfile;
            set => IsTunfile = !value;
        }

        [Reactive] public string OctaveLengthDescription { get; set; } = string.Empty;
        public System.Collections.ObjectModel.ObservableCollection<double> OctaveDots { get; } = new System.Collections.ObjectModel.ObservableCollection<double>();

        private double[]? _cachedTuningMap;
        private UProject project;

        public ProjectSettingsViewModel(UProject project) {
            this.project = project;
            EqualTemperament = project.EqualTemperament;
            ConcertPitch = project.ConcertPitch;
            ConcertPitchNote = project.ConcertPitchNote;
            TuningMap = project.TuningMap;

            _isTunfile = TuningMap != null;
            if (_isTunfile) {
                _cachedTuningMap = TuningMap;
            }

            this.WhenAnyValue(x => x.TuningMap)
                .Subscribe(_ => UpdateVisualization());
        }

        private void UpdateVisualization() {
            OctaveDots.Clear();
            if (TuningMap == null || TuningMap.Length < 12) {
                OctaveLengthDescription = string.Empty;
                return;
            }

            int period = 12;
            for (int p = 5; p <= 50; p++) {
                bool isPeriod = true;
                int checks = 0;
                for (int i = 0; i < TuningMap.Length - p; i++) {
                    if (TuningMap[i] <= 0 || TuningMap[i + p] <= 0) continue;

                    double ratio = TuningMap[i + p] / TuningMap[i];
                    if (Math.Abs(ratio - 2.0) > 0.06) {
                        isPeriod = false;
                        break;
                    }
                    checks++;
                }
                if (isPeriod && checks > 0) {
                    period = p;
                    break;
                }
            }

            int baseIndex = 60;
            if (baseIndex + period >= TuningMap.Length) baseIndex = 0;

            double freqBase = TuningMap[baseIndex];
            double freqTop = TuningMap[baseIndex + period];

            if (freqBase <= 0 || freqTop <= 0) {
                OctaveLengthDescription = "Invalid Frequencies";
                return;
            }

            double totalCents = 1200.0 * Math.Log(freqTop / freqBase, 2);
            OctaveLengthDescription = $"1 Octave ({period} Keys) = {totalCents:F1} cents";

            for (int i = 0; i <= period; i++) {
                double freq = TuningMap[baseIndex + i];
                if (freq <= 0) continue;
                double cents = 1200.0 * Math.Log(freq / freqBase, 2);
                if (totalCents > 0) {
                    OctaveDots.Add(cents / totalCents);
                }
            }
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
