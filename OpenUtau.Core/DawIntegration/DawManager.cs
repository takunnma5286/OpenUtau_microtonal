using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using K4os.Hash.xxHash;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Util;
using Serilog;
using System.IO;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace OpenUtau.Core.DawIntegration {
    public class DawManager : SingletonBase<DawManager>, ICmdSubscriber {
        public DawClient? dawClient = null;
        CancellationTokenSource? renderCancellation = null;
        private Debounce sendLayoutDebounce = new Debounce();
        private Debounce sendAudioDebounce = new Debounce();
        private System.Threading.Timer? commandWatcher;

        public static string CommandFilePath => Path.Combine(Path.GetTempPath(), "openutau_daw_command.txt");

        private DawManager() {
            DocManager.Inst.AddSubscriber(this);
            // Watch for command files from external tools (ReaScript etc.)
            commandWatcher = new System.Threading.Timer(CheckCommandFile, null, 50, 50);
        }

        private void CheckCommandFile(object? state) {
            try {
                var path = CommandFilePath;
                if (!File.Exists(path)) return;

                string content = File.ReadAllText(path).Trim();
                File.Delete(path);

                if (content.StartsWith("openPartEditor:")) {
                    if (int.TryParse(content.Substring("openPartEditor:".Length), out int trackNo)) {
                        Log.Information($"External command: open part editor for track {trackNo}");
                        DocManager.Inst.ExecuteCmd(new OpenPartEditorNotification(trackNo));
                    }
                }
            } catch (Exception ex) {
                Log.Error(ex, "Error checking command file");
            }
        }

        public void OnNext(UCommand cmd, bool isUndo) {
            if (cmd is UNotification && !(
                cmd is DawConnectedNotification ||
                cmd is PartRenderedNotification ||
                cmd is VolumeChangeNotification ||
                cmd is PanChangeNotification
                )) {
                return;
            }

            sendLayoutDebounce.Do(TimeSpan.FromSeconds(1), async () => {
                await UpdateUstx();
                await UpdateTracks();
            });
            sendAudioDebounce.Do(TimeSpan.FromSeconds(1), async () => {
                await UpdateAudio();
            });
        }

        public async Task Disconnect() {
            if (this.dawClient == null) {
                return;
            }
            await UpdateUstx();
            await UpdateTracks();
            await UpdateAudio();

            var dawClient = this.dawClient;
            this.dawClient = null;
            dawClient.Disconnect();
        }

        internal bool isDawClientLocked = false;

        private async Task UpdateUstx() {
            if (dawClient == null) {
                return;
            }

            Log.Information("Updating ustx in DAW...");

            try {
                var ustx = Format.Ustx.FromProject(DocManager.Inst.Project);
                await dawClient.SendNotification(
                    new UpdateUstxNotification(ustx)
                );
                Log.Information("Sent ustx to DAW.");
            } catch (Exception e) {
                Log.Error(e, "Failed to send ustx to DAW.");
            }
        }
        private async Task UpdateTracks() {
            if (dawClient == null) {
                return;
            }

            Log.Information("Updating tracks in DAW...");

            try {
                await dawClient.SendNotification(
                    new UpdateTracksNotification(
                            DocManager.Inst.Project.tracks.Select(track => new UpdateTracksNotification.Track(
                                track.TrackName,
                                track.Volume,
                                track.Pan
                            )).ToList()
                        )
                );
                Log.Information("Sent tracks to DAW.");
            } catch (Exception e) {
                Log.Error(e, "Failed to send tracks to DAW.");
            }
        }


        private async Task UpdateAudio() {
            if (dawClient == null) {
                return;
            }
            try {
                var readyParts = DocManager.Inst.Project.parts.Where(part => part is UVoicePart uPart && uPart.Mix != null)
                    .Select(part => (part as UVoicePart)!)
                    .ToList();

                Log.Information("Rendering prerenders for DAW...");
                var hashToAudioPart = new Dictionary<uint, byte[]>();
                var buffers = readyParts.Select(part => {
                    double startMs = DocManager.Inst.Project.timeAxis.TickPosToMsPos(part.position);
                    double endMs = DocManager.Inst.Project.timeAxis.TickPosToMsPos(part.position + part.duration);
                    int samplePos = (int)(startMs * 44100 / 1000) * 2;
                    int sampleCount = (int)((endMs - startMs) * 44100 / 1000) * 2;

                    // TODO: memoize this
                    var floatBuffer = new float[sampleCount];
                    part.Mix.Mix(samplePos, floatBuffer, 0, sampleCount);
                    var byteBuffer = new byte[floatBuffer.Length * 4];
                    Buffer.BlockCopy(floatBuffer, 0, byteBuffer, 0, byteBuffer.Length);
                    uint hash = XXH32.DigestOf(byteBuffer);

                    hashToAudioPart[hash] = byteBuffer;

                    return (part, startMs, endMs, hash);
                });
                Log.Information("Sending part layout to DAW...");
                var missingAudios = await dawClient.SendRequest<UpdatePartLayoutResponse>(
                    new UpdatePartLayoutRequest(
                        buffers.Select(buffer => new UpdatePartLayoutRequest.Part(
                            buffer.part.trackNo,
                            buffer.startMs,
                            buffer.endMs,
                            buffer.hash
                        )).ToList()
                    )
                );
                Log.Information("Sent part layout to DAW.");

                if (missingAudios.missingAudios.Count > 0) {
                    Log.Information($"DAW requested {missingAudios.missingAudios.Count} missing audios.");
                    var audios = new Dictionary<uint, string>();
                    foreach (var audioHash in missingAudios.missingAudios) {
                        if (!hashToAudioPart.ContainsKey(audioHash)) {
                            Log.Warning($"DAW requested missing audio {audioHash}, but it is not in the project.");
                            continue;
                        }

                        var byteBuffer = hashToAudioPart[audioHash];
                        var compressed = Zstd.Compress(byteBuffer);

                        audios[audioHash] = Convert.ToBase64String(compressed);
                    }

                    await dawClient.SendNotification(
                        new UpdateAudioNotification(audios)
                    );
                    Log.Information("Sent missing audios to DAW.");
                } else {
                    Log.Information("Audios in DAW are up to date.");
                }
            } catch (Exception e) {
                Log.Error(e, "Failed to send status to DAW.");
            }
        }

        public void GenerateSyncMidi(UPart part, string filePath) {
            var project = DocManager.Inst.Project;
            var midiFile = new MidiFile();
            
            // Project resolution (typically 480 PPQ)
            midiFile.TimeDivision = new TicksPerQuarterNoteTimeDivision((short)project.resolution);
            
            // Tempo and Time Signature Map
            midiFile.Chunks.Add(new TrackChunk());
            using (TempoMapManager tempoMapManager = midiFile.ManageTempoMap()) {
                var lastUTimeSignature = new UTimeSignature {
                    barPosition = 0,
                    beatPerBar = 4,
                    beatUnit = 4
                };
                int lastTime = 0;
                foreach (UTimeSignature uTimeSignature in project.timeSignatures) {
                    var time = lastTime + (uTimeSignature.barPosition - lastUTimeSignature.barPosition) * lastUTimeSignature.beatPerBar * 4 / lastUTimeSignature.beatUnit * project.resolution;
                    tempoMapManager.SetTimeSignature(time, new TimeSignature(uTimeSignature.beatPerBar, uTimeSignature.beatUnit));
                    lastUTimeSignature = uTimeSignature;
                    lastTime = time;
                }
                foreach(UTempo uTempo in project.tempos){
                    tempoMapManager.SetTempo(uTempo.position, Tempo.FromBeatsPerMinute(uTempo.bpm));
                }
            }

            var trackChunk = new TrackChunk(
                new SequenceTrackNameEvent("OpenUtau Sync")
            );
            midiFile.Chunks.Add(trackChunk);

            // Sync granularity: 16th notes (PPQ / 4 quarter note multiplier = typically 120 ticks)
            int ppq = ((TicksPerQuarterNoteTimeDivision)midiFile.TimeDivision).TicksPerQuarterNote; // usually 480
            int ticksPerStep = ppq / 16; // 1/64th note (480 / 16 = 30 ticks)
            
            int partStartStep = part.position / ticksPerStep;
            int partEndStep = (part.position + part.Duration + ticksPerStep - 1) / ticksPerStep;

            byte velocity = (byte)Math.Clamp(part.trackNo + 1, 1, 127);
            
            using (var notesManager = trackChunk.ManageNotes()) {
                var notes = notesManager.Objects;
                for (int i = partStartStep; i < partEndStep; i++) {
                    int tickPos = i * ticksPerStep;
                    int noteIndex = i + 1; // 1-indexed for the encoded value
                    int noteDuration = ticksPerStep / 2;
                    if (noteDuration == 0) continue;

                    for (int bit = 0; bit < 32; bit++) {
                        if (((noteIndex >> bit) & 1) == 1) {
                            byte pitch = (byte)Math.Clamp(bit, 0, 127); // bit 0 = C-1 according to MIDI standards depending on offset
                            notes.Add(new Note((SevenBitNumber)pitch, noteDuration, tickPos) {
                                Velocity = (SevenBitNumber)velocity
                            });
                        }
                    }
                }
            }

            midiFile.Write(filePath, true);
        }
    }
}
