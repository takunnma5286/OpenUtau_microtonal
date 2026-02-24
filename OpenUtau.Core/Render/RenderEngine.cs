using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenUtau.Core.SignalChain;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Util;
using OpenUtau.Classic;
using Serilog;

namespace OpenUtau.Core.Render {
    public class Progress {
        readonly int total;
        int completed = 0;
        public Progress(int total) {
            this.total = total;
        }

        public void Complete(int n, string info) {
            Interlocked.Add(ref completed, n);
            Notify(completed * 100.0 / total, info);
        }

        public void Clear() {
            Notify(0, string.Empty);
        }

        private void Notify(double progress, string info) {
            var notif = new ProgressBarNotification(progress, info);
            var task = new Task(() => DocManager.Inst.ExecuteCmd(notif));
            task.Start(DocManager.Inst.MainScheduler);
        }
    }

    class RenderPartRequest {
        public UVoicePart part;
        public long timestamp;
        public int trackNo;
        public RenderPhrase[] phrases;
        public WaveSource[] sources;
        public WaveMix mix;
    }

    class RenderEngine {
        readonly UProject project;
        readonly int startTick;
        readonly int endTick;
        readonly int trackNo;

        public RenderEngine(UProject project, int startTick = 0, int endTick = -1, int trackNo = -1) {
            this.project = project;
            this.startTick = startTick;
            this.endTick = endTick;
            this.trackNo = trackNo;
        }

        // for playback or export
        public Tuple<WaveMix, List<Fader>> RenderMixdown(TaskScheduler uiScheduler, ref CancellationTokenSource cancellation, bool wait = false) {
            var newCancellation = new CancellationTokenSource();
            var oldCancellation = Interlocked.Exchange(ref cancellation, newCancellation);
            if (oldCancellation != null) {
                oldCancellation.Cancel();
                oldCancellation.Dispose();
            }
            double startMs = project.timeAxis.TickPosToMsPos(startTick);
            double endMs = endTick == -1 ? double.PositiveInfinity : project.timeAxis.TickPosToMsPos(endTick);
            var faders = new List<Fader>();
            var requests = PrepareRequests()
                .Where(request => request.sources.Length > 0 && request.sources.Max(s => s.EndMs) > startMs && (double.IsPositiveInfinity(endMs) || request.sources.Min(s => s.offsetMs) < endMs))
                .ToArray();
            for (int i = 0; i < project.tracks.Count; ++i) {
                if (trackNo != -1 && trackNo != i) {
                    continue;
                }
                var track = project.tracks[i];
                var trackRequests = requests
                    .Where(req => req.trackNo == i)
                    .ToArray();
                var trackSources = trackRequests.Select(req => req.mix)
                    .OfType<ISignalSource>()
                    .ToList();
                trackSources.AddRange(project.parts
                    .Where(part => part is UWavePart && part.trackNo == i)
                    .Select(part => part as UWavePart)
                    .Where(part => part.Samples != null)
                    .Select(part => {
                        double offsetMs = project.timeAxis.TickPosToMsPos(part.position);
                        double estimatedLengthMs = project.timeAxis.TickPosToMsPos(part.End) - offsetMs;
                        var waveSource = new WaveSource(
                            offsetMs,
                            estimatedLengthMs,
                            part.skipMs, part.channels);
                        waveSource.SetSamples(part.Samples);
                        return (ISignalSource)waveSource;
                    }));
                var trackMix = new WaveMix(trackSources);
                var fader = new Fader(trackMix);
                fader.Scale = PlaybackManager.DecibelToVolume(track.Muted ? -24 : track.Volume);
                fader.Pan = (float)track.Pan;
                fader.SetScaleToTarget();
                faders.Add(fader);
            }
            if (OS.IsWasm()) {
                // Console.WriteLine("[RenderEngine] RenderMixdown executing synchronously for WASM.");
                try {
                    // In WASM, we cannot trust Task.Run or background threads to spin up reliably if the main thread is busy?
                    // Or rather, we should just run it and let the async nature handle yields.
                    // But RenderRequestsAsync returns Task. We can't block.
                    // IMPORTANT: RenderRequestsAsync does await inside.
                    // If we just call it without await, it runs until first await.
                    // We need to ensure it continues.
                    // So we must hook it up.

                    // We will fire and forget, but attach the continuation.
                    RenderRequestsAsync(requests, newCancellation, playing: !wait).ContinueWith(t => {
                        if (t.IsFaulted && !wait) {
                            Log.Error(t.Exception.Flatten(), "Failed to render.");
                            PlaybackManager.Inst.StopPlayback();
                            MessageCustomizableException customEx;
                            if (t.Exception.Flatten().InnerExceptions.ToList().Any(e => e is DllNotFoundException)) {
                                customEx = new MessageCustomizableException("Failed to render.", "<translate:errors.failed.render>: <translate:errors.install.cpp>", t.Exception);
                            } else {
                                customEx = new MessageCustomizableException("Failed to render.", "<translate:errors.failed.render>", t.Exception);
                            }
                            DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(customEx));
                        }
                    }, CancellationToken.None, TaskContinuationOptions.None, uiScheduler);
                } catch (Exception) {
                    // ignore immediate start errors
                }
            } else {
                var task = Task.Run(async () => {
                    // Console.WriteLine("[RenderEngine] RenderMixdown Task.Run started.");
                    await RenderRequestsAsync(requests, newCancellation, playing: !wait);
                    // Console.WriteLine("[RenderEngine] RenderMixdown Task.Run completed.");
                });
                task.ContinueWith(task => {
                    if (task.IsFaulted && !wait) {
                        Log.Error(task.Exception.Flatten(), "Failed to render.");
                        PlaybackManager.Inst.StopPlayback();
                        MessageCustomizableException customEx;
                        if (task.Exception.Flatten().InnerExceptions.ToList().Any(e => e is DllNotFoundException)) {
                            customEx = new MessageCustomizableException("Failed to render.", "<translate:errors.failed.render>: <translate:errors.install.cpp>", task.Exception);
                        } else {
                            customEx = new MessageCustomizableException("Failed to render.", "<translate:errors.failed.render>", task.Exception);
                        }
                        DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(customEx));
                    }
                }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, uiScheduler);
                if (wait) {
                    task.Wait();
                }
            }
            return Tuple.Create(new WaveMix(faders), faders);
        }

        // for playback
        public Tuple<MasterAdapter, List<Fader>> RenderProject(TaskScheduler uiScheduler, ref CancellationTokenSource cancellation) {
            double startMs = project.timeAxis.TickPosToMsPos(startTick);
            var renderMixdownResult = RenderMixdown(uiScheduler, ref cancellation, wait: false);
            var master = new MasterAdapter(renderMixdownResult.Item1);
            master.SetPosition((int)(startMs * 44100 / 1000) * 2);
            return Tuple.Create(master, renderMixdownResult.Item2);
        }

        // for export
        public List<WaveMix> RenderTracks(TaskScheduler uiScheduler, ref CancellationTokenSource cancellation) {
            var newCancellation = new CancellationTokenSource();
            var oldCancellation = Interlocked.Exchange(ref cancellation, newCancellation);
            if (oldCancellation != null) {
                oldCancellation.Cancel();
                oldCancellation.Dispose();
            }
            var trackMixes = new List<WaveMix>();
            var requests = PrepareRequests();
            if (requests.Length == 0) {
                return trackMixes;
            }
            Enumerable.Range(0, requests.Max(req => req.trackNo) + 1)
                .Select(trackNo => requests.Where(req => req.trackNo == trackNo).ToArray())
                .ToList()
                .ForEach(trackRequests => {
                    if (trackRequests.Length == 0) {
                        trackMixes.Add(null);
                    } else {
                        RenderRequestsAsync(trackRequests, newCancellation).Wait();
                        var mix = new WaveMix(trackRequests.Select(req => req.mix).ToArray());
                        trackMixes.Add(mix);
                    }
                });
            return trackMixes;
        }

        // for pre render
        public void PreRenderProject(ref CancellationTokenSource cancellation) {
            var newCancellation = new CancellationTokenSource();
            var oldCancellation = Interlocked.Exchange(ref cancellation, newCancellation);
            if (oldCancellation != null) {
                oldCancellation.Cancel();
                oldCancellation.Dispose();
            }
            Task.Run(async () => {
                try {
                    await Task.Delay(200);
                    if (newCancellation.Token.IsCancellationRequested) {
                        return;
                    }
                    await RenderRequestsAsync(PrepareRequests(), newCancellation);
                } catch (Exception e) {
                    if (!newCancellation.IsCancellationRequested) {
                        Log.Error(e, "Failed to pre-render.");
                    }
                }
            });
        }

        private RenderPartRequest[] PrepareRequests() {
            // Console.WriteLine($"[RenderEngine] PrepareRequests called. trackNo={trackNo}, startTick={startTick}, endTick={endTick}");
            RenderPartRequest[] requests;
            SingerManager.Inst.ReleaseSingersNotInUse(project);
            lock (project) {
                var initialContext = project.parts
                    .Where(part => part is UVoicePart && (trackNo == -1 || part.trackNo == trackNo))
                    .ToArray();
                // Console.WriteLine($"[RenderEngine] Found {initialContext.Length} candidate parts.");

                requests = initialContext
                    .Where(part => {
                        bool muted = Preferences.Default.SkipRenderingMutedTracks && project.tracks[part.trackNo].Muted;
                        // if (muted) Console.WriteLine($"[RenderEngine] Part {part.name} skipped (Muted)");
                        return !muted;
                    })
                    .Select(part => part as UVoicePart)
                    .Select(part => {
                        var req = part.GetRenderRequest();
                        // if (req == null) Console.WriteLine($"[RenderEngine] Part {part.name} returned null RenderRequest");
                        // else Console.WriteLine($"[RenderEngine] Part {part.name} returned RenderRequest with {req.phrases.Length} phrases.");
                        return req;
                    })
                    .Where(request => request != null)
                    .ToArray();
            }
            // Console.WriteLine($"[RenderEngine] Processing {requests.Length} requests detailed.");
            foreach (var request in requests) {
                if (endTick != -1) {
                    var originalPhrases = request.phrases.Length;
                    request.phrases = request.phrases
                        .Where(phrase => phrase.end > startTick && (endTick == -1 || phrase.position < endTick))
                        .ToArray();
                    // Console.WriteLine($"[RenderEngine] Phrases filtered from {originalPhrases} to {request.phrases.Length} by tick range.");
                }
                request.sources = new WaveSource[request.phrases.Length];
                for (var i = 0; i < request.phrases.Length; i++) {
                    var phrase = request.phrases[i];
                    try {
                        var firstPhone = phrase.phones.First();
                        var lastPhone = phrase.phones.Last();
                        var layout = phrase.renderer.Layout(phrase);
                        if (layout == null) {
                            // Console.WriteLine($"[RenderEngine] Phrase {i} layout is null.");
                            continue;
                        }
                        double posMs = layout.positionMs - layout.leadingMs;
                        double durMs = layout.estimatedLengthMs;
                        request.sources[i] = new WaveSource(posMs, durMs, 0, 1);
                        // Console.WriteLine($"[RenderEngine] Phrase {i} source created: pos={posMs}, dur={durMs}");
                    } catch (Exception ex) {
                        // Console.WriteLine($"[RenderEngine] Error preparing phrase {i}: {ex.Message}");
                    }
                }
                request.mix = new WaveMix(request.sources);
            }
            return requests;
        }

        private async Task RenderRequestsAsync(
            RenderPartRequest[] requests,
            CancellationTokenSource cancellation,
            bool playing = false) {
            if (requests.Length == 0 || cancellation.IsCancellationRequested) {
                return;
            }
            // Console.WriteLine($"[RenderEngine] RenderRequestsAsync started with {requests.Length} requests. Playing={playing}");
            var tuples = requests
                .SelectMany(req => req.phrases
                    .Zip(req.sources, (phrase, source) => Tuple.Create(phrase, source, req)))
                .ToArray();
            if (playing) {
                var orderedTuples = tuples
                    .Where(tuple => tuple.Item1.end > startTick)
                    .OrderBy(tuple => tuple.Item1.end)
                    .Concat(tuples.Where(tuple => tuple.Item1.end <= startTick))
                    .ToArray();
                tuples = orderedTuples;
            }
            var progress = new Progress(tuples.Sum(t => t.Item1.phones.Length));
            foreach (var tuple in tuples) {
                var phrase = tuple.Item1;
                var source = tuple.Item2;
                var request = tuple.Item3;
                // Console.WriteLine($"[RenderEngine] Invoking Renderer {phrase.renderer} for phrase {phrase.hash:x16}");
                try {
                    var result = await phrase.renderer.Render(phrase, progress, request.trackNo, cancellation, true);
                    if (cancellation.IsCancellationRequested) {
                        break;
                    }
                    source.SetSamples(result.samples);
                    if (request.sources.All(s => s.HasSamples)) {
                        request.part.SetMix(request.mix);
                        DocManager.Inst.ExecuteCmd(new PartRenderedNotification(request.part));
                    }
                } catch (Exception ex) {
                    // Console.WriteLine($"[RenderEngine] Renderer failed: {ex}");
                    throw;
                }
            }
            progress.Clear();
        }

        public static void ReleaseSourceTemp() {
            VoicebankFiles.Inst.ReleaseSourceTemp();
        }
    }
}
