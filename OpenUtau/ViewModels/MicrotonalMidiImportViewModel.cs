using System.Collections.Generic;
using System.Collections.ObjectModel;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace OpenUtau.App.ViewModels {
    public class MidiChannelInfo : ViewModelBase {
        public int ChannelNumber { get; set; }
        [Reactive] public int Offset { get; set; }
    }

    public class MicrotonalMidiImportViewModel : ViewModelBase {
        public ObservableCollection<MidiChannelInfo> Channels { get; set; }

        public MicrotonalMidiImportViewModel() : this(new List<MidiChannelInfo>()) {}

        public MicrotonalMidiImportViewModel(List<MidiChannelInfo> channels) {
            Channels = new ObservableCollection<MidiChannelInfo>(channels);
        }
    }
}