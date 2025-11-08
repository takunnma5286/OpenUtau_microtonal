using System;
using System.Linq;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using OpenUtau.Core;
using OpenUtau.Core.Util;
using ReactiveUI;

namespace OpenUtau.App.Controls {
    class TrackBackground : TemplatedControl {
        public static readonly DirectProperty<TrackBackground, double> TrackHeightProperty =
            AvaloniaProperty.RegisterDirect<TrackBackground, double>(
                nameof(TrackHeight),
                o => o.TrackHeight,
                (o, v) => o.TrackHeight = v);
        public static readonly DirectProperty<TrackBackground, double> TrackOffsetProperty =
            AvaloniaProperty.RegisterDirect<TrackBackground, double>(
                nameof(TrackOffset),
                o => o.TrackOffset,
                (o, v) => o.TrackOffset = v);
        public static readonly DirectProperty<TrackBackground, bool> IsPianoRollProperty =
            AvaloniaProperty.RegisterDirect<TrackBackground, bool>(
                nameof(IsPianoRoll),
                o => o.IsPianoRoll,
                (o, v) => o.IsPianoRoll = v);
        public static readonly DirectProperty<TrackBackground, bool> IsKeyboardProperty =
            AvaloniaProperty.RegisterDirect<TrackBackground, bool>(
                nameof(IsKeyboard),
                o => o.IsKeyboard,
                (o, v) => o.IsKeyboard = v);
        public static readonly DirectProperty<TrackBackground, int> KeyProperty =
            AvaloniaProperty.RegisterDirect<TrackBackground, int>(
                nameof(Key),
                o => o.Key,
                (o, v) => o.Key = v);

        public static readonly DirectProperty<TrackBackground, int> EqualTemperamentProperty =
            AvaloniaProperty.RegisterDirect<TrackBackground, int>(
                nameof(EqualTemperament),
                o => o.EqualTemperament,
                (o, v) => o.EqualTemperament = v);

        public static readonly DirectProperty<TrackBackground, int> MaxToneProperty =
            AvaloniaProperty.RegisterDirect<TrackBackground, int>(
                nameof(MaxTone),
                o => o.MaxTone,
                (o, v) => o.MaxTone = v);

        public static readonly DirectProperty<TrackBackground, double> ConcertPitchProperty =
            AvaloniaProperty.RegisterDirect<TrackBackground, double>(
                nameof(ConcertPitch),
                o => o.ConcertPitch,
                (o, v) => o.ConcertPitch = v);

        public static readonly DirectProperty<TrackBackground, int> ConcertPitchNoteProperty =
            AvaloniaProperty.RegisterDirect<TrackBackground, int>(
                nameof(ConcertPitchNote),
                o => o.ConcertPitchNote,
                (o, v) => o.ConcertPitchNote = v);

        public double TrackHeight {
            get => _trackHeight;
            private set => SetAndRaise(TrackHeightProperty, ref _trackHeight, value);
        }
        public double TrackOffset {
            get => _trackOffset;
            private set => SetAndRaise(TrackOffsetProperty, ref _trackOffset, value);
        }
        public bool IsPianoRoll {
            get => _isPianoRoll;
            set => SetAndRaise(IsPianoRollProperty, ref _isPianoRoll, value);
        }
        public bool IsKeyboard {
            get => _isKeyboard;
            set => SetAndRaise(IsPianoRollProperty, ref _isKeyboard, value);
        }
        public int Key {
            get => _key;
            set => SetAndRaise(KeyProperty, ref _key, value);
        }
        public int EqualTemperament {
            get => _equalTemperament;
            set => SetAndRaise(EqualTemperamentProperty, ref _equalTemperament, value);
        }
        public int MaxTone {
            get => _maxTone;
            set => SetAndRaise(MaxToneProperty, ref _maxTone, value);
        }
        public double ConcertPitch {
            get => _concertPitch;
            set => SetAndRaise(ConcertPitchProperty, ref _concertPitch, value);
        }
        public int ConcertPitchNote {
            get => _concertPitchNote;
            set => SetAndRaise(ConcertPitchNoteProperty, ref _concertPitchNote, value);
        }

        private double _trackHeight;
        private double _trackOffset;
        private bool _isPianoRoll;
        private bool _isKeyboard;
        private int _key;
        private int _equalTemperament = 12;
        private int _maxTone = 12 * 11;
        private double _concertPitch = 440.0;
        private int _concertPitchNote = 69;

        public TrackBackground() {
            MessageBus.Current.Listen<ThemeChangedEvent>()
                .Subscribe(e => InvalidateVisual());
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
            base.OnPropertyChanged(change);
            if (change.Property == TrackHeightProperty ||
                change.Property == TrackOffsetProperty ||
                change.Property == ForegroundProperty ||
                change.Property == KeyProperty ||
                change.Property == ConcertPitchProperty ||
                change.Property == ConcertPitchNoteProperty) {
                InvalidateVisual();
            }
        }

        int mod(int a, int b){
            return (a % b + b) % b;
        }

        public override void Render(DrawingContext context) {
            if (TrackHeight == 0) {
                return;
            }
            int track = (int)TrackOffset;
            double top = TrackHeight * (track - TrackOffset);
            string[] degreeNames;
            switch(Preferences.Default.DegreeStyle){
                case 1:
                    degreeNames = MusicMath.Solfeges;
                    break;
                case 2:
                    degreeNames = MusicMath.NumberedNotations;
                    break;
                default:
                    degreeNames = Enumerable.Repeat("", 12).ToArray();
                    break;
            }
            while (top < Bounds.Height) {
                bool isAltTrack = IsAltTrack(track) ^ (ThemeManager.IsDarkMode && !IsKeyboard);
                bool isCenterKey = IsKeyboard && IsCenterKey(track);
                var brush = isCenterKey ? ThemeManager.CenterKeyBrush
                    : IsKeyboard ? (isAltTrack ? ThemeManager.BlackKeyBrush : ThemeManager.WhiteKeyBrush)
                    : isAltTrack ? Foreground : Background;
                context.DrawRectangle(
                    brush,
                    null,
                    new Rect(0, (int)top, Bounds.Width, TrackHeight));
                if (IsKeyboard && TrackHeight >= 12) {
                    brush = isCenterKey ? ThemeManager.CenterKeyNameBrush
                        : isAltTrack ? ThemeManager.BlackKeyNameBrush
                            : ThemeManager.WhiteKeyNameBrush;
                    int tone = MaxTone - 1 - track;
                    string toneName = MusicMath.GetToneName(tone, EqualTemperament);
                    var toneTextLayout = TextLayoutCache.Get(toneName, brush, 12);
                    var toneTextPosition = new Point(Bounds.Width - 4 - (int)toneTextLayout.Width, (int)(top + (TrackHeight - toneTextLayout.Height) / 2));
                    using (var state = context.PushTransform(Matrix.CreateTranslation(toneTextPosition))) {
                        toneTextLayout.Draw(context, new Point());
                    }
                    //scale degree display
                    int degree = mod(tone - Key, 12);
                    string degreeName = degreeNames[degree];
                    var degreeTextLayout = TextLayoutCache.Get(degreeName, brush, 12);
                    var degreeTextPosition = new Point(4, (int)(top + (TrackHeight - degreeTextLayout.Height) / 2));
                    using (var state = context.PushTransform(Matrix.CreateTranslation(degreeTextPosition))) {
                        degreeTextLayout.Draw(context, new Point());
                    }
                }
                track++;
                top += TrackHeight;
            }
        }

        private bool IsAltTrack(int track) {
            if (!IsPianoRoll) {
                return track % 2 == 1;
            }
            int tone = MaxTone - 1 - track;
            if (tone < 0) {
                return false;
            }
            int ET = EqualTemperament;

            if (ET == 12) {
                return new int[] { 1, 3, 6, 8, 10 }.Contains(mod(tone, 12));
            }

            // Try to solve 7n + 5m = ET for positive integers n, m.
            int n = -1, m = -1;
            for (int i = 1; i * 7 < ET; ++i) {
                if ((ET - 7 * i) % 5 == 0) {
                    m = (ET - 7 * i) / 5;
                    if (m > 0) {
                        n = i;
                        break;
                    }
                }
            }

            if (n > 0 && m > 0) { // Found a 7-white-5-black structure
                int t = mod(tone, ET);
                var blockBoundaries = new int[] {
                    n, n + m, 2 * n + m, 2 * n + 2 * m, 3 * n + 2 * m, 4 * n + 2 * m,
                    4 * n + 3 * m, 5 * n + 3 * m, 5 * n + 4 * m, 6 * n + 4 * m, 6 * n + 5 * m, ET
                };
                var blockIsBlack = new bool[] {
                    false, true, false, true, false, false, true, false, true, false, true, false
                };
                int blockIndex = 0;
                while (blockIndex < 11 && t >= blockBoundaries[blockIndex]) {
                    blockIndex++;
                }
                return blockIsBlack[blockIndex];
            }

            // Fallback: white key every octave
            return mod(tone, ET) != 0;
        }

        private bool IsCenterKey(int track) {
            int tone = MaxTone - 1 - track;
            double C4_FREQ = 261.6255653005986; // Frequency of Middle C (C4)
            double targetTone = EqualTemperament * Math.Log2(C4_FREQ / ConcertPitch) + ConcertPitchNote;
            return tone == (int)Math.Round(targetTone);
        }
    }
}
