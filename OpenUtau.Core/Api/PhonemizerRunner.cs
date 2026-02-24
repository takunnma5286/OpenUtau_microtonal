using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using Serilog;

namespace OpenUtau.Api {
    internal class PhonemizerRequest {
        public USinger singer;
        public UVoicePart part;
        public long timestamp;
        public int[] noteIndexes;
        public Phonemizer.Note[][] notes;
        public Phonemizer phonemizer;
        public TimeAxis timeAxis;
    }

    internal class PhonemizerResponse {
        public UVoicePart part;
        public long timestamp;
        public int[] noteIndexes;
        public Phonemizer.Phoneme[][] phonemes;
    }

    internal class PhonemizerRunner : IDisposable {
        private readonly TaskScheduler mainScheduler;
        private readonly CancellationTokenSource shutdown = new CancellationTokenSource();
        private readonly ConcurrentQueue<PhonemizerRequest> requests = new ConcurrentQueue<PhonemizerRequest>();
        private readonly object busyLock = new object();
        private Thread thread;

        public PhonemizerRunner(TaskScheduler mainScheduler) {
            // Console.WriteLine($"[PhonemizerRunner] Creating instance {GetHashCode()}. IsWasm={OS.IsWasm()}");
            this.mainScheduler = mainScheduler;
            if (!OS.IsWasm()) {
                thread = new Thread(PhonemizerLoop) {
                    IsBackground = true,
                    Priority = ThreadPriority.AboveNormal,
                };
                thread.Start();
            }
        }

        public void Push(PhonemizerRequest request) {
            requests.Enqueue(request);
            // Console.WriteLine($"[PhonemizerRunner] Instance {GetHashCode()} Pushed request for part {request.part.name}, timestamp={request.timestamp}. QSize={requests.Count}");
            if (OS.IsWasm()) {
                ScheduleProcess();
            }
        }

        private int isProcessing = 0;
        private void ScheduleProcess() {
            var original = Interlocked.CompareExchange(ref isProcessing, 1, 0);
            if (original == 0) {
                // Console.WriteLine($"[PhonemizerRunner] Instance {GetHashCode()} Starting new ProcessAsync task.");
                // Successfully took the lock, start processing loop
                ProcessAsync().ContinueWith(t => {
                    // if (t.IsFaulted) Console.WriteLine($"[PhonemizerRunner] ProcessAsync Faulted: {t.Exception}");
                });
            } else {
                // Console.WriteLine($"[PhonemizerRunner] Instance {GetHashCode()} ProcessAsync already running (isProcessing={original}).");
            }
        }

        private async Task ProcessAsync() {
            try {
                // Removed initial await to ensure we enter the loop synchronously and don't hang on startup.
                // await Task.Delay(1); 
                // Console.WriteLine($"[PhonemizerRunner] Instance {GetHashCode()} ProcessAsync started execution (SYNC ENTRY).");

                var parts = new HashSet<UVoicePart>();
                var toRun = new List<PhonemizerRequest>();

                while (true) {
                    bool worked = false;
                    while (requests.TryDequeue(out var request)) {
                        // Console.WriteLine($"[PhonemizerRunner] Dequeued request for {request.part.name}");
                        toRun.Add(request);
                        worked = true;
                    }

                    if (toRun.Count > 0) {
                        // Console.WriteLine($"[PhonemizerRunner] Processing batch of {toRun.Count} requests.");
                        foreach (var req in toRun) {
                            parts.Add(req.part);
                        }
                        for (int i = toRun.Count - 1; i >= 0; i--) {
                            if (parts.Remove(toRun[i].part)) {
                                // Console.WriteLine($"[PhonemizerRunner] Phonemizing {toRun[i].part.name}...");
                                try {
                                    // Heavy work, ensure we are not hogging the thread
                                    // Removed await Task.Delay(10); // Causes hangs in WASM
                                    // Console.WriteLine($"[PhonemizerRunner] Phonemizing {toRun[i].part.name}... (SYNC EXEC)");
                                    var result = Phonemize(toRun[i]);
                                    // Console.WriteLine($"[PhonemizerRunner] Phonemized {toRun[i].part.name}. Sending response...");
                                    SendResponse(result);
                                } catch (Exception e) {
                                    // Console.WriteLine($"[PhonemizerRunner] Error in Phonemize: {e}");
                                }
                            }
                        }
                        parts.Clear();
                        toRun.Clear();
                    }

                    if (!worked) {
                        // Queue empty, double check if we can exit
                        if (requests.IsEmpty) {
                            // Console.WriteLine($"[PhonemizerRunner] Queue empty, exiting ProcessAsync.");
                            break;
                        }
                    }

                    // Always yield after a batch to keep UI responsive
                    // Removed await Task.Delay(10);
                }
            } catch (Exception e) {
                // Console.WriteLine($"[PhonemizerRunner] ProcessAsync Error: {e}");
            } finally {
                // Release lock
                Interlocked.Exchange(ref isProcessing, 0);

                // Double check race condition: if items added just as we were exiting
                if (!requests.IsEmpty) {
                    ScheduleProcess();
                }
            }
        }

        void PhonemizerLoop() {
            var parts = new HashSet<UVoicePart>();
            var toRun = new List<PhonemizerRequest>();
            while (!shutdown.IsCancellationRequested) {
                lock (busyLock) {
                    while (requests.TryDequeue(out var request)) {
                        toRun.Add(request);
                    }
                    if (toRun.Count == 0) {
                        Monitor.Wait(busyLock, 100);
                        continue;
                    }

                    foreach (var request in toRun) {
                        parts.Add(request.part);
                    }
                    for (int i = toRun.Count - 1; i >= 0; i--) {
                        if (parts.Remove(toRun[i].part)) {
                            SendResponse(Phonemize(toRun[i]));
                        }
                    }
                    parts.Clear();
                    toRun.Clear();
                }
            }
        }

        void SendResponse(PhonemizerResponse response) {
            Task.Factory.StartNew(_ => {
                // Console.WriteLine($"[PhonemizerRunner] SendResponse executing for {response.part.name}");
                if (DocManager.Inst.Project.parts.Contains(response.part)) {
                    response.part.SetPhonemizerResponse(response);
                    // Console.WriteLine($"[PhonemizerRunner] SetPhonemizerResponse called on part.");
                } else {
                    // Console.WriteLine($"[PhonemizerRunner] Part no longer in project.");
                }
                DocManager.Inst.Project.Validate(new ValidateOptions {
                    SkipTiming = true,
                    Part = response.part,
                    SkipPhonemizer = true,
                });
                DocManager.Inst.ExecuteCmd(new PhonemizedNotification());
            }, null, CancellationToken.None, TaskCreationOptions.None, mainScheduler);
        }

        static PhonemizerResponse Phonemize(PhonemizerRequest request) {
            var notes = request.notes;
            var phonemizer = request.phonemizer;
            if (request.singer == null) {
                return new PhonemizerResponse() {
                    noteIndexes = request.noteIndexes,
                    part = request.part,
                    phonemes = new Phonemizer.Phoneme[][] { },
                    timestamp = request.timestamp,
                };
            }
            phonemizer.SetSinger(request.singer);
            phonemizer.SetTiming(request.timeAxis);
            try {
                phonemizer.SetUp(notes, DocManager.Inst.Project, DocManager.Inst.Project.tracks[request.part.trackNo]);
            } catch (Exception e) {
                Log.Error(e, $"phonemizer failed to setup.");
            }

            var result = new List<Phonemizer.Phoneme[]>();
            for (int i = notes.Length - 1; i >= 0; i--) {
                Phonemizer.Result phonemizerResult;
                bool prevIsNeighbour = false;
                bool nextIsNeighbour = false;
                Phonemizer.Note[] prevs = null;
                Phonemizer.Note? prev = null;
                Phonemizer.Note? next = null;
                if (i > 0) {
                    prevs = notes[i - 1];
                    prev = notes[i - 1][0];
                    var prevLast = notes[i - 1].Last();
                    prevIsNeighbour = prevLast.position + prevLast.duration >= notes[i][0].position;
                }
                if (i < notes.Length - 1) {
                    next = notes[i + 1][0];
                    var thisLast = notes[i].Last();
                    nextIsNeighbour = thisLast.position + thisLast.duration >= next.Value.position;
                }

                if (next != null && result.Count > 0 && result[0].Length > 0) {
                    var end = notes[i].Last().position + notes[i].Last().duration;
                    int endPushback = Math.Min(0, result[0][0].position - end);
                    notes[i][notes[i].Length - 1].duration += endPushback;
                }
                try {
                    phonemizerResult = phonemizer.Process(
                        notes[i],
                        prev,
                        next,
                        prevIsNeighbour ? prev : null,
                        nextIsNeighbour ? next : null,
                        (prevIsNeighbour ? prevs : null) ?? new Phonemizer.Note[0]);
                } catch (Exception e) {
                    Log.Error(e, $"phonemizer error {notes[i][0].lyric}");
                    phonemizerResult = new Phonemizer.Result() {
                        phonemes = new Phonemizer.Phoneme[] {
                            new Phonemizer.Phoneme {
                                phoneme = "error"
                            }
                        }
                    };
                }
                if (phonemizer.LegacyMapping) {
                    for (var k = 0; k < phonemizerResult.phonemes.Length; k++) {
                        var phoneme = phonemizerResult.phonemes[k];
                        if (request.singer.TryGetMappedOto(phoneme.phoneme, notes[i][0].tone, out var oto)) {
                            phonemizerResult.phonemes[k].phoneme = oto.Alias;
                        }
                    }
                }
                for (var j = 0; j < phonemizerResult.phonemes.Length; j++) {
                    phonemizerResult.phonemes[j].position += notes[i][0].position;
                }
                result.Insert(0, phonemizerResult.phonemes);
            }
            try {
                phonemizer.CleanUp();
            } catch (Exception e) {
                Log.Error(e, $"phonemizer failed to cleanup.");
            }
            return new PhonemizerResponse() {
                noteIndexes = request.noteIndexes,
                part = request.part,
                phonemes = result.ToArray(),
                timestamp = request.timestamp,
            };
        }

        public void WaitFinish() {
            if (OS.IsWasm()) return;
            while (true) {
                if (requests.IsEmpty) return;
                Thread.Sleep(10);
            }
        }

        public void Dispose() {
            if (shutdown.IsCancellationRequested) {
                return;
            }
            shutdown.Cancel();
            if (thread != null) {
                while (thread.IsAlive) {
                    Thread.Sleep(100);
                }
                thread = null;
            }
        }
    }
}
