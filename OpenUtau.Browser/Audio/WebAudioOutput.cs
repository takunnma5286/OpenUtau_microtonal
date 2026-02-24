using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using OpenUtau.Core;
using OpenUtau.Core.Util;


namespace OpenUtau.Audio {
    public partial class WebAudioOutput : IAudioOutput {
        [JSImport("initAudio", "AppBundle/audio.js")]
        internal static partial void InitAudio();

        [JSImport("playAudio", "AppBundle/audio.js")]
        internal static partial void PlayAudio(byte[] samples, int sampleRate, int channels);

        [JSImport("startHeartbeat", "AppBundle/audio.js")]
        internal static partial void StartHeartbeat(
            [JSMarshalAs<JSType.Function<JSType.Number>>] Func<int> onTick,
            int bufferSize,
            int sampleRate);

        [JSImport("stopHeartbeat", "AppBundle/audio.js")]
        internal static partial void StopHeartbeat();

        private object lockObj = new object();
        // Keep reference to delegate to prevent GC? 
        // The JS side holds reference, but does .NET side?
        // Usually marshaler handles it, but explicit field is safer.
        private Func<int> audioTickDelegate;

        public PlaybackState PlaybackState { get; private set; } = PlaybackState.Stopped;
        public int DeviceNumber => 0;

        private ISampleProvider source;
        private long positionBytes;

        public void Init(ISampleProvider sampleProvider) {
            this.source = sampleProvider;
            audioTickDelegate = ProcessAudio;
            // Console.WriteLine($"[WebAudioOutput.Init] Channels={sampleProvider.WaveFormat.Channels}, SampleRate={sampleProvider.WaveFormat.SampleRate}");
            try {
                InitAudio();
            } catch {
                // Console.WriteLine("WebAudio init failed: " + ex);
            }
        }

        public void Play() {
            lock (lockObj) {
                if (PlaybackState == PlaybackState.Playing) return;
                PlaybackState = PlaybackState.Playing;
                positionBytes = 0;

                try {
                    int bufferSize = Preferences.Default.PlaybackBufferSize;
                    if (bufferSize < 1024) bufferSize = 1024;
                    int sampleRate = source?.WaveFormat.SampleRate ?? 44100;

                    // Console.WriteLine($"Starting playback: bufferSize={bufferSize}, sampleRate={sampleRate}");
                    StartHeartbeat(audioTickDelegate, bufferSize, sampleRate);
                } catch (Exception ex) {
                    // Console.WriteLine("Failed to start heartbeat: " + ex);
                }
            }
        }

        public void Stop() {
            // // Console.WriteLine("WebAudioOutput.Stop() called from:\\n" + Environment.StackTrace);
            lock (lockObj) {
                PlaybackState = PlaybackState.Stopped;
                positionBytes = 0;
                StopHeartbeat();
            }
        }

        public void Pause() {
            lock (lockObj) {
                PlaybackState = PlaybackState.Paused;
                StopHeartbeat();
            }
        }

        public long GetPosition() {
            return positionBytes;
        }

        public List<AudioOutputDevice> GetOutputDevices() {
            return new List<AudioOutputDevice> {
                new AudioOutputDevice {
                    name = "Web Audio API",
                    api = "Browser",
                    guid = Guid.Empty,
                    deviceNumber = 0
                }
            };
        }

        public void SelectDevice(Guid guid, int deviceNumber) { }

        public int ProcessAudio() {
            if (PlaybackState != PlaybackState.Playing) return 0;

            try {
                // Buffer size from preferences
                int bufferSize = Preferences.Default.PlaybackBufferSize;
                if (bufferSize < 1024) bufferSize = 1024; // safety
                float[] buffer = new float[bufferSize];
                int read;

                lock (lockObj) {
                    if (source == null) return 0;
                    read = source.Read(buffer, 0, buffer.Length);
                }

                if (read == 0) {
                    // Console.WriteLine("WebAudioOutput: Source Read returned 0. Stopping.");
                    Stop();
                    return 0;
                }

                byte[] byteBuf = new byte[read * 4];
                Buffer.BlockCopy(buffer, 0, byteBuf, 0, byteBuf.Length);

                PlayAudio(byteBuf, source.WaveFormat.SampleRate, source.WaveFormat.Channels);

                lock (lockObj) {
                    positionBytes += read * 4;
                }

            } catch (Exception e) {
                // Console.WriteLine("WebAudioOutput Error: " + e);
                Stop();
            }
            return 0;
        }
    }
}
