using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenUtau.Classic;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Util;
using Serilog;

namespace OpenUtau.Core {
    public class SingerManager : SingletonBase<SingerManager> {
        public Dictionary<string, USinger> Singers { get; private set; } = new Dictionary<string, USinger>();
        public Dictionary<USingerType, List<USinger>> SingerGroups { get; private set; } = new Dictionary<USingerType, List<USinger>>();

        private readonly ConcurrentQueue<USinger> reloadQueue = new ConcurrentQueue<USinger>();
        private CancellationTokenSource reloadCancellation;

        private HashSet<USinger> singersUsed = new HashSet<USinger>();

        public void Initialize() {
            SearchAllSingers();
        }

        public void SearchAllSingers() {
            // Console.WriteLine("[SingerManager] SearchAllSingers called.");
            Log.Information("Searching singers.");
            Directory.CreateDirectory(PathManager.Inst.SingersPath);
            try {
                var stopWatch = Stopwatch.StartNew();
                Singers = new Dictionary<string, USinger>();

                // Add default singer
                // Removed non-existent UDefaultSinger usage
                // var defaultSinger = new UDefaultSinger();
                // Singers.Add(defaultSinger.Id, defaultSinger);
                // // Console.WriteLine("[SingerManager] Added default singer.");

                foreach (var path in PathManager.Inst.SingersPaths) {
                    // Console.WriteLine($"[SingerManager] Searching in path: {path}");
                    if (!Directory.Exists(path)) {
                        // Console.WriteLine($"[SingerManager] Path does not exist: {path}");
                        continue;
                    }
                }
                var singers = ClassicSingerLoader.FindAllSingers()
                    .Concat(Vogen.VogenSingerLoader.FindAllSingers())
                    .Distinct();

                foreach (var s in singers) {
                    // Console.WriteLine($"[SingerManager] Found singer: {s.Id}");
                    if (!Singers.ContainsKey(s.Id)) {
                        Singers.Add(s.Id, s);
                    }
                }

                SingerGroups = Singers.Values
                    .GroupBy(s => s.SingerType)
                    .ToDictionary(s => s.Key, s => s.LocalizedOrderBy(singer => singer.LocalizedName).ToList());
                stopWatch.Stop();
                Log.Information($"Search all singers: {stopWatch.Elapsed}");
                // Console.WriteLine($"[SingerManager] Search finished in {stopWatch.Elapsed}. Total singers: {Singers.Count}");
            } catch (Exception e) {
                Log.Error(e, "Failed to search singers.");
                // Console.WriteLine($"[SingerManager] Failed to search singers: {e}");
            }
        }

        public USinger GetSinger(string name) {
            Log.Information($"Attach singer to track: {name}");
            name = name.Replace("%VOICE%", "");
            if (Singers.ContainsKey(name)) {
                return Singers[name];
            }
            return null;
        }

        public void ScheduleReload(USinger singer) {
            reloadQueue.Enqueue(singer);
            ScheduleReload();
        }

        private void ScheduleReload() {
            var newCancellation = new CancellationTokenSource();
            var oldCancellation = Interlocked.Exchange(ref reloadCancellation, newCancellation);
            if (oldCancellation != null) {
                oldCancellation.Cancel();
                oldCancellation.Dispose();
            }
            Task.Run(() => {
                Thread.Sleep(200);
                if (newCancellation.IsCancellationRequested) {
                    return;
                }
                Refresh();
            });
        }

        private void Refresh() {
            var singers = new HashSet<USinger>();
            while (reloadQueue.TryDequeue(out USinger singer)) {
                singers.Add(singer);
            }
            foreach (var singer in singers) {
                Log.Information($"Reloading {singer.Id}");
                new Task(() => {
                    DocManager.Inst.ExecuteCmd(new ProgressBarNotification(0, $"Reloading {singer.Id}"));
                }).Start(DocManager.Inst.MainScheduler);
                int retries = 5;
                while (retries > 0) {
                    retries--;
                    try {
                        singer.Reload();
                        break;
                    } catch (Exception e) {
                        if (retries == 0) {
                            Log.Error(e, $"Failed to reload {singer.Id}");
                        } else {
                            Log.Error(e, $"Retrying reload {singer.Id}");
                            Thread.Sleep(200);
                        }
                    }
                }
                Log.Information($"Reloaded {singer.Id}");
                new Task(() => {
                    DocManager.Inst.ExecuteCmd(new ProgressBarNotification(0, $"Reloaded {singer.Id}"));
                    DocManager.Inst.ExecuteCmd(new OtoChangedNotification(external: true));
                }).Start(DocManager.Inst.MainScheduler);
            }
        }

        //Check which singers are in use and free memory for those that are not
        public void ReleaseSingersNotInUse(UProject project) {
            //Check which singers are in use
            var singersInUse = new HashSet<USinger>();
            foreach (var track in project.tracks) {
                var singer = track.Singer;
                if (singer != null && singer.Found && !singersInUse.Contains(singer)) {
                    singersInUse.Add(singer);
                }
            }
            //Release singers that are no longer in use
            foreach (var singer in singersUsed) {
                if (!singersInUse.Contains(singer)) {
                    singer.FreeMemory();
                }
            }
            //Update singers used
            singersUsed = singersInUse;
        }
    }
}
