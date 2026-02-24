using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;
using NAudio.Wave;
using NumSharp;
using OpenUtau.Classic;
using OpenUtau.Core.Format;
using Serilog;

namespace OpenUtau.Core.Render {
    public class SynthRequestError : Exception { }

    public class CutOffExceedDurationError : SynthRequestError { }

    public class CutOffBeforeOffsetError : SynthRequestError { }

    public static partial class Worldline {
        // JavaScript Interop Bridge initialization
        [JSImport("initWorldline", "worldline_bridge")]
        public static partial Task InitWorldlineAsync();

        [JSImport("WorldlineTest", "worldline_bridge")]
        public static partial int WorldlineTest();


#if !BROWSER
        [LibraryImport("worldline", EntryPoint = "F0")]
        private static partial int F0(
            float[] samples, int length, int fs, double framePeriod,
            int method, ref IntPtr f0);
#endif

        [JSImport("F0ViaGlobal", "worldline_bridge")]
        private static partial void F0_JS(int fs, double framePeriod, int method);
        public static double[] F0(float[] samples, int fs, double framePeriod, int method) {
            // method=-1 means skip F0 estimation (FRQ file exists)
            if (method == -1) {
                Log.Information("[F0] Method=-1, skipping F0 estimation (FRQ file will be used)");
                int numFrames = (int)Math.Ceiling(samples.Length / (double)fs * 1000.0 / framePeriod);
                return new double[numFrames]; // Return zeros, will be filled from FRQ
            }

            if (OperatingSystem.IsBrowser()) {
                try {
                    Log.Information($"[F0] Called with {samples.Length} samples, fs={fs}, framePeriod={framePeriod}, method={method}");
                    var globalThis = JSHost.GlobalThis;

                    // Pass array via JSON
                    Log.Information("[F0] Serializing samples to JSON...");
                    var json = System.Text.Json.JsonSerializer.Serialize(samples);
                    Log.Information($"[F0] JSON length: {json.Length}");
                    globalThis.SetProperty("_worldlineInputJson", json);
                    Log.Information("[F0] Set _worldlineInputJson property");

                    // Call F0
                    Log.Information("[F0] Calling F0_JS...");
                    F0_JS(fs, framePeriod, method);
                    Log.Information("[F0] F0_JS returned");

                    // Read result
                    Log.Information("[F0] Reading result from _worldlineOutput...");
                    using (var resultJS = globalThis.GetPropertyAsJSObject("_worldlineOutput")) {
                        if (resultJS == null) {
                            Log.Error("[F0] ❌ Result is null!");
                            throw new InvalidOperationException("F0 result not found");
                        }

                        var length = (int)resultJS.GetPropertyAsInt32("length");
                        Log.Information($"[F0] Result array length: {length}");
                        var result = new double[length];
                        for (int i = 0; i < length; i++) {
                            result[i] = (double)resultJS.GetPropertyAsDouble(i.ToString());
                        }
                        Log.Information($"[F0] ✅ Returning {result.Length} values");
                        return result;
                    }
                } catch (Exception e) {
                    Log.Error(e, "Failed to estimate F0 (WASM).");
                    return null;
                }
            }
#if !BROWSER
            try {
                IntPtr buffer = IntPtr.Zero;
                int length = F0(samples, samples.Length, fs, framePeriod, method, ref buffer);
                var data = new double[length];
                Marshal.Copy(buffer, data, 0, length);
                Marshal.FreeCoTaskMem(buffer);
                return data;
            } catch (Exception e) {
                Log.Error(e, "Failed to estimate F0.");
                return null;
            }
#else
            throw new PlatformNotSupportedException("F0 is not supported on this platform.");
#endif
        }


#if !BROWSER
        [LibraryImport("worldline", EntryPoint = "DecodeMgc")]
        private static partial int DecodeMgc(
            int f0Length, double[] mgc, int mgcSize,
            int fftSize, int fs, ref IntPtr spectrogram);
#endif

        public static double[,] DecodeMgc(int f0Length, double[] mgc, int fftSize, int fs) {
            if (OperatingSystem.IsBrowser()) {
                throw new NotImplementedException("DecodeMgc via JSImport not yet implemented. Need to use JSObject for arrays.");
            }
#if !BROWSER
            try {
                int mgcSize = mgc.Length / f0Length;
                unsafe {
                    IntPtr buffer = IntPtr.Zero;
                    int size = DecodeMgc(f0Length, mgc, mgcSize, fftSize, fs, ref buffer);
                    var data = new double[f0Length * size];
                    Marshal.Copy(buffer, data, 0, data.Length);
                    Marshal.FreeCoTaskMem(buffer);
                    var output = new double[f0Length, size];
                    Buffer.BlockCopy(data, 0, output, 0, data.Length * sizeof(double));
                    return output;
                }
            } catch (Exception e) {
                Log.Error(e, "Failed to decode.");
                return null;
            }
#else
            throw new PlatformNotSupportedException("DecodeMgc is not supported on this platform.");
#endif
        }

#if !BROWSER
        [LibraryImport("worldline", EntryPoint = "DecodeBap")]
        private static partial int DecodeBap(
            int f0Length, double[] bap,
            int fftSize, int fs, ref IntPtr aperiodicity);
#endif

        public static double[,] DecodeBap(int f0Length, double[] bap, int fftSize, int fs) {
            if (OperatingSystem.IsBrowser()) {
                throw new NotImplementedException("DecodeBap via JSImport not yet implemented. Need to use JSObject for arrays.");
            }
#if !BROWSER
            try {
                unsafe {
                    IntPtr buffer = IntPtr.Zero;
                    int size = DecodeBap(f0Length, bap, fftSize, fs, ref buffer);
                    var data = new double[f0Length * size];
                    Marshal.Copy(buffer, data, 0, data.Length);
                    Marshal.FreeCoTaskMem(buffer);
                    var output = new double[f0Length, size];
                    Buffer.BlockCopy(data, 0, output, 0, data.Length * sizeof(double));
                    return output;
                }
            } catch (Exception e) {
                Log.Error(e, "Failed to decode.");
                return null;
            }
#else
            throw new PlatformNotSupportedException("DecodeBap is not supported on this platform.");
#endif
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct AnalysisConfig {
            public int fs;
            public int hop_size;
            public int fft_size;
            public float f0_floor;
            public double frame_ms;
        };


#if !BROWSER
        [LibraryImport("worldline", EntryPoint = "InitAnalysisConfig")]
        private static partial void InitAnalysisConfig(ref AnalysisConfig config,
             int fs, int hop_size, int fft_size);
#endif

        [JSImport("InitAnalysisConfig", "worldline_bridge")]
        private static partial JSObject InitAnalysisConfig_JS(int fs, int hopSize, int fftSize);


        public static AnalysisConfig InitAnalysisConfig(int fs, double frame_ms) {
            if (OperatingSystem.IsBrowser()) {
                using var configJS = InitAnalysisConfig_JS(fs, 0, 0);
                var config = new AnalysisConfig {
                    fs = (int)configJS.GetPropertyAsInt32("fs"),
                    hop_size = (int)configJS.GetPropertyAsInt32("hop_size"),
                    fft_size = (int)configJS.GetPropertyAsInt32("fft_size"),
                    f0_floor = (float)configJS.GetPropertyAsDouble("f0_floor"),
                    frame_ms = configJS.GetPropertyAsDouble("frame_ms")
                };
                config.frame_ms = frame_ms;
                return config;
            }
#if !BROWSER
            var nativeConfig = new AnalysisConfig();
            InitAnalysisConfig(ref nativeConfig, fs, 0, 0);
            nativeConfig.frame_ms = frame_ms;
            return nativeConfig;
#else
            throw new PlatformNotSupportedException("InitAnalysisConfig is not supported on this platform.");
#endif
        }

        public static AnalysisConfig InitAnalysisConfig(int fs, int hop_size, int fft_size) {
            if (OperatingSystem.IsBrowser()) {
                using var configJS = InitAnalysisConfig_JS(fs, hop_size, fft_size);
                return new AnalysisConfig {
                    fs = (int)configJS.GetPropertyAsInt32("fs"),
                    hop_size = (int)configJS.GetPropertyAsInt32("hop_size"),
                    fft_size = (int)configJS.GetPropertyAsInt32("fft_size"),
                    f0_floor = (float)configJS.GetPropertyAsDouble("f0_floor"),
                    frame_ms = configJS.GetPropertyAsDouble("frame_ms")
                };
            }
#if !BROWSER
            AnalysisConfig config = new AnalysisConfig();
            InitAnalysisConfig(ref config, fs, hop_size, fft_size);
            return config;
#else
            throw new PlatformNotSupportedException("InitAnalysisConfig is not supported on this platform.");
#endif
        }

#if BROWSER
        // WorldAnalysis - Not implemented for WASM
        public static unsafe void WorldAnalysis(
            float[] samples, int fs, double frame_ms,
            out double[] f0, out double[,] sp_env, out double[,] ap) {
            throw new NotImplementedException("WorldAnalysis via JSImport not yet implemented.");
        }
#else
        [LibraryImport("worldline", EntryPoint = "WorldAnalysis")]
        private static unsafe partial void WorldAnalysis(
            ref AnalysisConfig config, float[] samples, int num_samples,
            double** f0_out, double** sp_env_out, double** ap_out,
            int* f0_length_out);
        public static unsafe void WorldAnalysis(
            float[] samples, int fs, double frame_ms,
            out double[] f0, out double[,] sp_env, out double[,] ap) {
            var config = InitAnalysisConfig(fs, frame_ms);
            unsafe {
                double* f0Ptr, spEnvPtr, apPtr;
                int length;
                WorldAnalysis(ref config, samples, samples.Length,
                    &f0Ptr, &spEnvPtr, &apPtr, &length);
                f0 = new double[length];
                sp_env = new double[length, config.fft_size / 2 + 1];
                ap = new double[length, config.fft_size / 2 + 1];
                Marshal.Copy(new IntPtr(f0Ptr), f0, 0, length);
                Copy2D(new IntPtr(spEnvPtr), sp_env, length, config.fft_size / 2 + 1);
                Copy2D(new IntPtr(apPtr), ap, length, config.fft_size / 2 + 1);
                Marshal.FreeCoTaskMem(new IntPtr(f0Ptr));
                Marshal.FreeCoTaskMem(new IntPtr(spEnvPtr));
                Marshal.FreeCoTaskMem(new IntPtr(apPtr));
            }
        }
#endif

        static unsafe void Copy2D(IntPtr ptr, double[,] data, int rows, int cols) {
            var span = new Span<double>((void*)ptr, rows * cols);
            for (int i = 0; i < rows; ++i) {
                for (int j = 0; j < cols; ++j) {
                    data[i, j] = span[i * cols + j];
                }
            }
        }


        [JSImport("WorldAnalysisF0InViaGlobal", "worldline_bridge")]
        private static partial void WorldAnalysisF0In_JS(int fs, int hopSize, int fftSize, float f0Floor, double frameMs);

#if !BROWSER
        [LibraryImport("worldline", EntryPoint = "WorldAnalysisF0In")]
        private static unsafe partial void WorldAnalysisF0In_Native(
            ref AnalysisConfig config, float[] samples, int num_samples,
            double[] f0_in, int num_frames, double* sp_env_out, double* ap_out);
#endif

        public static unsafe void WorldAnalysisF0In(ref AnalysisConfig config, float[] samples,
            double[] f0In, out NDArray spEnv, out NDArray ap) {
            if (OperatingSystem.IsBrowser()) {
                try {
                    var totalSw = System.Diagnostics.Stopwatch.StartNew();
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    Log.Information($"[WorldAnalysisF0In] Called with {samples.Length} samples, {f0In.Length} frames");

                    int numFrames = f0In.Length;
                    int spSize = config.fft_size / 2 + 1;

                    var globalThis = JSHost.GlobalThis;

                    // Prepare input using Base64 encoding for efficient binary transfer
                    sw.Restart();
                    var samplesBytes = new byte[samples.Length * sizeof(float)];
                    Buffer.BlockCopy(samples, 0, samplesBytes, 0, samplesBytes.Length);
                    var f0InBytes = new byte[f0In.Length * sizeof(double)];
                    Buffer.BlockCopy(f0In, 0, f0InBytes, 0, f0InBytes.Length);
                    Log.Information($"[WorldAnalysisF0In] ⏱️ Buffer.BlockCopy: {sw.ElapsedMilliseconds}ms");

                    sw.Restart();
                    var inputData = new {
                        samplesBase64 = Convert.ToBase64String(samplesBytes),
                        f0InBase64 = Convert.ToBase64String(f0InBytes),
                        samplesLength = samples.Length,
                        f0InLength = f0In.Length
                    };
                    var inputJson = System.Text.Json.JsonSerializer.Serialize(inputData);
                    Log.Information($"[WorldAnalysisF0In] ⏱️ Base64+Serialize: {sw.ElapsedMilliseconds}ms, JSON length: {inputJson.Length}");

                    sw.Restart();
                    globalThis.SetProperty("_worldlineInput", inputJson);
                    Log.Information($"[WorldAnalysisF0In] ⏱️ SetProperty: {sw.ElapsedMilliseconds}ms");

                    // Call JavaScript function
                    sw.Restart();
                    WorldAnalysisF0In_JS(config.fs, config.hop_size, config.fft_size, config.f0_floor, config.frame_ms);
                    Log.Information($"[WorldAnalysisF0In] ⏱️ JS Call: {sw.ElapsedMilliseconds}ms");

                    // Read output JSON
                    sw.Restart();
                    var outputJson = globalThis.GetPropertyAsString("_worldlineOutput");
                    if (string.IsNullOrEmpty(outputJson)) {
                        throw new InvalidOperationException("WorldAnalysisF0In output not found");
                    }
                    Log.Information($"[WorldAnalysisF0In] ⏱️ GetProperty: {sw.ElapsedMilliseconds}ms, JSON length: {outputJson.Length}");

                    // Parse output - use Base64 for efficient binary transfer
                    sw.Restart();
                    using var outputDoc = System.Text.Json.JsonDocument.Parse(outputJson);
                    var root = outputDoc.RootElement;

                    var resultNumFrames = root.GetProperty("numFrames").GetInt32();
                    var resultSpSize = root.GetProperty("spSize").GetInt32();

                    // Decode Base64 to byte arrays, then convert to double arrays
                    var spEnvBase64 = root.GetProperty("spEnvBase64").GetString()
                        ?? throw new InvalidOperationException("spEnvBase64 is null");
                    var apBase64 = root.GetProperty("apBase64").GetString()
                        ?? throw new InvalidOperationException("apBase64 is null");
                    Log.Information($"[WorldAnalysisF0In] ⏱️ JSON Parse: {sw.ElapsedMilliseconds}ms");

                    sw.Restart();
                    var spEnvBytes = Convert.FromBase64String(spEnvBase64);
                    var apBytes = Convert.FromBase64String(apBase64);
                    Log.Information($"[WorldAnalysisF0In] ⏱️ Base64 Decode: {sw.ElapsedMilliseconds}ms");

                    // Convert bytes to double arrays
                    sw.Restart();
                    int arrayLength = resultNumFrames * resultSpSize;
                    var spEnvArray = new double[arrayLength];
                    var apArray = new double[arrayLength];
                    Buffer.BlockCopy(spEnvBytes, 0, spEnvArray, 0, spEnvBytes.Length);
                    Buffer.BlockCopy(apBytes, 0, apArray, 0, apBytes.Length);

                    // Create NDArrays and copy data efficiently
                    spEnv = np.ndarray(new Shape(resultNumFrames, resultSpSize), typeof(double));
                    ap = np.ndarray(new Shape(resultNumFrames, resultSpSize), typeof(double));

                    // Use direct memory copy via NDArray's underlying storage
                    unsafe {
                        var spEnvSpan = new Span<double>(spEnv.Data<double>().Address, arrayLength);
                        var apSpan = new Span<double>(ap.Data<double>().Address, arrayLength);
                        spEnvArray.AsSpan().CopyTo(spEnvSpan);
                        apArray.AsSpan().CopyTo(apSpan);
                    }
                    Log.Information($"[WorldAnalysisF0In] ⏱️ NDArray Copy: {sw.ElapsedMilliseconds}ms");

                    totalSw.Stop();
                    Log.Information($"[WorldAnalysisF0In] ✅ Complete - Total: {totalSw.ElapsedMilliseconds}ms");
                } catch (Exception e) {
                    Log.Error(e, "Failed to call WorldAnalysisF0In (WASM).");
                    throw;
                }
            } else {
#if !BROWSER
                int numFrames = f0In.Length;
                int spSize = config.fft_size / 2 + 1;
                spEnv = np.ndarray(new Shape(numFrames, spSize), typeof(double));
                ap = np.ndarray(new Shape(numFrames, spSize), typeof(double));
                Log.Information($"[Worldline] Calling native WorldAnalysisF0In with {numFrames} frames");
                WorldAnalysisF0In_Native(ref config, samples, samples.Length, f0In, numFrames,
                    spEnv.Data<double>().Address, ap.Data<double>().Address);
#else
                throw new PlatformNotSupportedException("WorldAnalysisF0In is not supported on this platform.");
#endif
            }
        }


        [JSImport("WorldSynthesisViaGlobal", "worldline_bridge")]
        private static partial void WorldSynthesis_JS(bool isMgc, int mgcSize, bool isBap, int fftSize, double framePeriod, int fs);

        public static double[] WorldSynthesis(
            double[] f0,
            double[] mgcOrSp, bool isMgc, int mgcSize,
            double[] bapOrAp, bool isBap, int fftSize,
            double framePeriod, int fs,
            double[] gender, double[] tension,
            double[] breathiness, double[] voicing) {
            if (OperatingSystem.IsBrowser()) {
                try {
                    Log.Information($"[WorldSynthesis] Called with f0.Length={f0.Length}, mgcOrSp.Length={mgcOrSp.Length}");

                    var globalThis = JSHost.GlobalThis;

                    // Prepare input JSON
                    var inputData = new {
                        f0 = f0,
                        mgcOrSp = mgcOrSp,
                        bapOrAp = bapOrAp,
                        gender = gender,
                        tension = tension,
                        breathiness = breathiness,
                        voicing = voicing
                    };
                    var inputJson = System.Text.Json.JsonSerializer.Serialize(inputData);
                    globalThis.SetProperty("_worldlineInput", inputJson);
                    Log.Information($"[WorldSynthesis] Set input JSON, length: {inputJson.Length}");

                    // Call JavaScript function
                    Log.Information("[WorldSynthesis] Calling WorldSynthesis_JS...");
                    WorldSynthesis_JS(isMgc, mgcSize, isBap, fftSize, framePeriod, fs);
                    Log.Information("[WorldSynthesis] WorldSynthesis_JS returned");

                    // Read output JSON
                    var outputJson = globalThis.GetPropertyAsString("_worldlineOutput");
                    if (string.IsNullOrEmpty(outputJson)) {
                        throw new InvalidOperationException("WorldSynthesis output not found");
                    }

                    Log.Information($"[WorldSynthesis] Got output JSON, length: {outputJson.Length}");

                    // Parse output
                    var result = System.Text.Json.JsonSerializer.Deserialize<double[]>(outputJson);
                    if (result == null) {
                        throw new InvalidOperationException("Failed to deserialize WorldSynthesis output");
                    }

                    Log.Information($"[WorldSynthesis] ✅ Complete, result.Length={result.Length}");
                    return result;
                } catch (Exception e) {
                    Log.Error(e, "Failed to call WorldSynthesis (WASM).");
                    throw;
                }
            } else {
#if !BROWSER
                unsafe {
                    IntPtr buffer = IntPtr.Zero;
                    int size = WorldSynthesis_Flat(
                        f0, f0.Length,
                        mgcOrSp, isMgc, mgcSize,
                        bapOrAp, isBap, fftSize,
                        framePeriod, fs, ref buffer,
                        gender, tension, breathiness, voicing);
                    var data = new double[size];
                    Marshal.Copy(buffer, data, 0, size);
                    Marshal.FreeCoTaskMem(buffer);
                    return data;
                }
#else
                throw new PlatformNotSupportedException("WorldSynthesis is not supported on this platform.");
#endif
            }
        }

        // 2D array overload - flattens and calls 1D version
        public static double[] WorldSynthesis(
            double[] f0,
            double[,] mgcOrSp, bool isMgc, int mgcSize,
            double[,] bapOrAp, bool isBap, int fftSize,
            double framePeriod, int fs,
            double[] gender, double[] tension,
            double[] breathiness, double[] voicing) {
            // Flatten 2D arrays to 1D
            int f0Length = f0.Length;
            double[] mgcOrSpFlat = new double[f0Length * mgcOrSp.GetLength(1)];
            double[] bapOrApFlat = new double[f0Length * bapOrAp.GetLength(1)];

            Buffer.BlockCopy(mgcOrSp, 0, mgcOrSpFlat, 0, mgcOrSpFlat.Length * sizeof(double));
            Buffer.BlockCopy(bapOrAp, 0, bapOrApFlat, 0, bapOrApFlat.Length * sizeof(double));

            return WorldSynthesis(
                f0,
                mgcOrSpFlat, isMgc, mgcSize,
                bapOrApFlat, isBap, fftSize,
                framePeriod, fs,
                gender, tension, breathiness, voicing);
        }


#if !BROWSER

        [LibraryImport("worldline", EntryPoint = "WorldSynthesis")]
        private static partial int WorldSynthesis_Flat(
            double[] f0, int f0Length,
            double[] mgcOrSp, [MarshalAs(UnmanagedType.I1)] bool isMgc, int mgcSize,
            double[] bapOrAp, [MarshalAs(UnmanagedType.I1)] bool isBap, int fftSize,
            double framePeriod, int fs, ref IntPtr y,
            double[] gender, double[] tension,
            double[] breathiness, double[] voicing);

#endif


        [StructLayout(LayoutKind.Sequential)]
        struct SynthRequest {
            public int sample_fs;
            public int sample_length;
            public IntPtr sample;
            public int frq_length;
            public IntPtr frq;
            public int tone;
            public double con_vel;
            public double offset;
            public double required_length;
            public double consonant;
            public double cut_off;
            public double volume;
            public double modulation;
            public double tempo;
            public int pitch_bend_length;
            public IntPtr pitch_bend;
            public int flag_g;
            public int flag_O;
            public int flag_P;
            public int flag_Mt;
            public int flag_Mb;
            public int flag_Mv;
        };

        class SynthRequestWrapper : IDisposable {
            public SynthRequest request;
            private bool disposedValue;
            private GCHandle[] handles;

            public SynthRequestWrapper(ResamplerItem item) {
                int fs;
                double[] sample;
                Log.Information($"[SynthRequestWrapper] Opening wave file: {item.inputFile}");
                using (var waveStream = Wave.OpenFile(item.inputFile)) {
                    fs = waveStream.WaveFormat.SampleRate;
                    sample = Wave.GetSamples(waveStream.ToSampleProvider().ToMono(1, 0))
                        .Select(f => (double)f).ToArray();
                }
                string frqFile = VoicebankFiles.GetFrqFile(item.inputFile);
                GCHandle? pinnedFrq = null;
                byte[] frq = null;
                if (File.Exists(frqFile)) {
                    using (var frqStream = File.OpenRead(frqFile)) {
                        using (var memStream = new MemoryStream()) {
                            frqStream.CopyTo(memStream);
                            frq = memStream.ToArray();
                            pinnedFrq = GCHandle.Alloc(frq, GCHandleType.Pinned);
                        }
                    }
                }

                var pinnedSample = GCHandle.Alloc(sample, GCHandleType.Pinned);
                var pinnedPitchBend = GCHandle.Alloc(item.pitches, GCHandleType.Pinned);
                handles = pinnedFrq == null
                    ? new[] { pinnedSample, pinnedPitchBend }
                    : new[] { pinnedSample, pinnedPitchBend, pinnedFrq.Value };
                request = new SynthRequest {
                    sample_fs = fs,
                    sample_length = sample.Length,
                    sample = pinnedSample.AddrOfPinnedObject(),
                    frq_length = frq?.Length ?? 0,
                    frq = pinnedFrq?.AddrOfPinnedObject() ?? IntPtr.Zero,
                    tone = item.tone,
                    con_vel = item.velocity,
                    offset = item.offset,
                    required_length = item.durRequired,
                    consonant = item.consonant,
                    cut_off = item.cutoff,
                    volume = item.phone.direct ? 0 : item.volume,
                    modulation = item.modulation,
                    tempo = item.tempo,
                    pitch_bend_length = item.pitches.Length,
                    pitch_bend = pinnedPitchBend.AddrOfPinnedObject(),
                    flag_g = 0,
                    flag_O = 0,
                    flag_P = 86,
                    flag_Mt = 0,
                    flag_Mb = 0,
                    flag_Mv = 100,
                };
                var flag = item.flags.FirstOrDefault(f => f.Item1 == "g");
                if (flag != null && flag.Item2.HasValue) {
                    request.flag_g = flag.Item2.Value;
                }
                flag = item.flags.FirstOrDefault(f => f.Item1 == "O");
                if (flag != null && flag.Item2.HasValue) {
                    request.flag_O = flag.Item2.Value;
                }
                flag = item.flags.FirstOrDefault(f => f.Item1 == "P");
                if (flag != null && flag.Item2.HasValue) {
                    request.flag_P = flag.Item2.Value;
                }
                flag = item.flags.FirstOrDefault(f => f.Item1 == "Mt");
                if (flag != null && flag.Item2.HasValue) {
                    request.flag_Mt = flag.Item2.Value;
                }
                flag = item.flags.FirstOrDefault(f => f.Item1 == "Mb");
                if (flag != null && flag.Item2.HasValue) {
                    request.flag_Mb = flag.Item2.Value;
                }
                flag = item.flags.FirstOrDefault(f => f.Item1 == "Mv");
                if (flag != null && flag.Item2.HasValue) {
                    request.flag_Mv = flag.Item2.Value;
                }
                Validate(request);
            }
            static void Validate(SynthRequest request) {
                int frame_ms = 10;
                var total_ms = 1000.0 * request.sample_length / request.sample_fs;
                var in_start_ms = request.offset;
                var in_length_ms = request.cut_off < 0
                    ? -request.cut_off
                    : total_ms - request.offset - request.cut_off;
                int in_start_frame = (int)(in_start_ms / frame_ms);
                int in_length_frame = (int)(Math.Ceiling(in_start_ms + in_length_ms) / frame_ms) - in_start_frame;
                if ((in_start_frame + in_length_frame) * frame_ms * request.sample_fs > request.sample_length * 1000.0) {
                    throw new CutOffExceedDurationError();
                }
                if (in_length_frame <= 0) {
                    throw new CutOffBeforeOffsetError();
                }
            }

            protected virtual void Dispose(bool disposing) {
                if (!disposedValue) {
                    foreach (var handle in handles) {
                        handle.Free();
                    }
                    disposedValue = true;
                }
            }

            public void Dispose() {
                Dispose(disposing: true);
                GC.SuppressFinalize(this);
            }
        }

        // TODO: Implement via JSImport
        static int Resample(IntPtr request, ref IntPtr y) {
            throw new NotImplementedException("Resample via JSImport not yet implemented");
        }

        public static float[] Resample(ResamplerItem item) {
            throw new NotImplementedException("Resample via JSImport not yet implemented");
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate void LogCallback(string log);

        // TODO: Implement via JSImport
        static IntPtr PhraseSynthNew() {
            throw new NotImplementedException("PhraseSynthNew via JSImport not yet implemented");
        }

        static void PhraseSynthDelete(IntPtr phrase_synth) {
            throw new NotImplementedException("PhraseSynthDelete via JSImport not yet implemented");
        }

        static void PhraseSynthAddRequest(
            IntPtr phrase_synth, IntPtr request,
            double posMs, double skipMs, double lengthMs,
            double fadeInMs, double fadeOutMs, LogCallback logCallback) {
            throw new NotImplementedException("PhraseSynthAddRequest via JSImport not yet implemented");
        }

        static void PhraseSynthSetCurves(
            IntPtr phraseSynth, double[] f0,
            double[] gender, double[] tension,
            double[] breathiness, double[] voicing,
            int length, LogCallback logCallback) {
            throw new NotImplementedException("PhraseSynthSetCurves via JSImport not yet implemented");
        }

        static int PhraseSynthSynth(
            IntPtr phrase_synth,
            ref IntPtr y, LogCallback logCallback) {
            throw new NotImplementedException("PhraseSynthSynth via JSImport not yet implemented");
        }

        public class PhraseSynth : IDisposable {
            private IntPtr ptr;
            private bool disposedValue;

            public PhraseSynth() {
                ptr = PhraseSynthNew();
            }

            protected virtual void Dispose(bool disposing) {
                if (!disposedValue) {
                    PhraseSynthDelete(ptr);
                    disposedValue = true;
                }
            }

            ~PhraseSynth() {
                Dispose(disposing: false);
            }

            public void Dispose() {
                Dispose(disposing: true);
                GC.SuppressFinalize(this);
            }

            public void AddRequest(
                ResamplerItem item, double posMs, double skipMs,
                double lengthMs, double fadeInMs, double fadeOutMs) {
                var requestWrapper = new SynthRequestWrapper(item);
                SynthRequest request = requestWrapper.request;
                try {
                    unsafe {
                        PhraseSynthAddRequest(
                            ptr, new IntPtr(&request),
                            posMs, skipMs, lengthMs,
                            fadeInMs, fadeOutMs, Log.Information);
                    }
                } finally {
                    requestWrapper.Dispose();
                }
            }

            public void SetCurves(
                double[] f0, double[] gender,
                double[] tension, double[] breathiness,
                double[] voicing) {
                PhraseSynthSetCurves(
                    ptr, f0,
                    gender, tension, breathiness, voicing,
                    f0.Length, Log.Information);
            }

            public float[] Synth() {
                IntPtr buffer = IntPtr.Zero;
                int size = PhraseSynthSynth(ptr, ref buffer, Log.Information);
                var data = new float[size];
                Marshal.Copy(buffer, data, 0, size);
                Marshal.FreeCoTaskMem(buffer);
                return data;
            }
        }

        class SynthSegment {
            public readonly AnalysisConfig config;
            public NDArray f0;
            public NDArray spEnv;
            public NDArray ap;

            public int skipFrames;
            public int p0;
            public int p1;
            public int p3;
            public int p4;

            public SynthSegment(AnalysisConfig cfg, ResamplerItem item,
                double posMs, double skipMs, double lengthMs,
                double fadeInMs, double fadeOutMs) {
                var totalSw = System.Diagnostics.Stopwatch.StartNew();
                var sw = System.Diagnostics.Stopwatch.StartNew();
                const int fs = 44100;
                config = cfg;
                float[] samples = new float[0];
                using (var waveStream = Wave.OpenFile(item.inputFile)) {
                    int wavFs = waveStream.WaveFormat.SampleRate;
                    if (wavFs != fs) {
                        throw new Exception($"Unsupported sample rate {wavFs} Hz in {item.inputFile}. Only {fs} Hz is supported.");
                    }
                    samples = Wave.GetSamples(waveStream.ToSampleProvider().ToMono(1, 0)).ToArray();
                }
                if (samples.Length == 0) {
                    throw new Exception($"Empty samples in {item.inputFile}.");
                }
                // Console.WriteLine($"[SynthSegment] ⏱️ Load WAV: {sw.ElapsedMilliseconds}ms");
                sw.Restart();

                var frq = new Frq();
                bool hasFrq = frq.Load(item.inputFile);
                var f0Src = F0(samples, fs, cfg.frame_ms, hasFrq ? -1 : 0);
                if (hasFrq) {
                    for (int i = 0; i < f0Src.Length; ++i) {
                        double ratio = (double)config.hop_size / frq.hopSize;
                        int index0 = (int)Math.Floor(i * ratio);
                        int index1 = (int)Math.Ceiling((i + 1) * ratio);
                        index0 = Math.Min(frq.f0.Length - 1, index0);
                        index1 = Math.Min(frq.f0.Length - 1, index1);
                        double sumF0 = 0.0;
                        int count = 0;
                        for (int j = index0; j <= index1; ++j) {
                            if (frq.f0[j] > config.f0_floor) {
                                sumF0 += frq.f0[j];
                                count += 1;
                            }
                        }
                        if (count > 0) {
                            f0Src[i] = sumF0 / count;
                        } else {
                            f0Src[i] = 0.0;
                        }
                    }
                }

                int srcStartFrame = (int)(item.offset / cfg.frame_ms);
                srcStartFrame = Math.Max(0, srcStartFrame);
                double srcEndMs = item.cutoff < 0
                    ? -item.cutoff + item.offset
                    : (samples.Length / (double)fs * 1000.0) - item.cutoff;
                int srcEndFrame = (int)Math.Ceiling(srcEndMs / cfg.frame_ms);
                srcEndFrame = Math.Min(f0Src.Length, srcEndFrame);
                if (srcEndFrame <= srcStartFrame) {
                    throw new CutOffBeforeOffsetError();
                }

                float wavMax = samples.Max(s => Math.Abs(s));

                int trimStartFrame = Math.Max(0, srcStartFrame - 2);
                int trimEndFrame = Math.Min(f0Src.Length, srcEndFrame + 2);
                srcStartFrame -= trimStartFrame;
                srcEndFrame -= trimStartFrame;
                f0Src = f0Src[trimStartFrame..trimEndFrame];
                int trimStartSample = trimStartFrame * cfg.hop_size;
                int trimEndSample = Math.Min(samples.Length, trimEndFrame * cfg.hop_size);
                var untrimmedSamples = samples;
                samples = new float[(trimEndFrame - trimStartFrame) * cfg.hop_size];
                Array.Copy(untrimmedSamples, trimStartSample, samples, 0, trimEndSample - trimStartSample);

                // Gain control
                float gain = item.volume * 0.01f;
                int flag_P = 86;
                var itemFlag = item.flags.FirstOrDefault(f => f.Item1 == "P");
                if (itemFlag != null && itemFlag.Item2.HasValue) {
                    flag_P = itemFlag.Item2.Value;
                }
                float autoGain = GetAutoGain(samples, f0Src, wavMax, flag_P);
                gain *= autoGain;
                for (int i = 0; i < samples.Length; ++i) {
                    samples[i] = samples[i] * gain;
                }

                // Console.WriteLine($"[SynthSegment] ⏱️ F0+Prep: {sw.ElapsedMilliseconds}ms");
                sw.Restart();
                WorldAnalysisF0In(ref cfg, samples, f0Src, out var spEnvSrc, out var apSrc);
                // Console.WriteLine($"[SynthSegment] ⏱️ WorldAnalysisF0In: {sw.ElapsedMilliseconds}ms");
                sw.Restart();


                double[] tSrc = new double[srcEndFrame - srcStartFrame];
                for (int i = 0; i < tSrc.Length; ++i) {
                    tSrc[i] = i * cfg.frame_ms;
                }
                double[] tDst = new double[(int)Math.Ceiling(item.durRequired / cfg.frame_ms)];
                {
                    double srcLengthMs = tSrc.Length * cfg.frame_ms;
                    double consonantSpeed = Math.Pow(0.5, 1.0 - item.velocity / 100.0);
                    double srcConsonantMs = item.consonant;
                    double srcVowelMs = srcLengthMs - srcConsonantMs;
                    double dstLengthMs = tDst.Length * cfg.frame_ms;
                    double dstConsonantMs = srcConsonantMs / consonantSpeed;
                    double dstVowelMs = dstLengthMs - dstConsonantMs;
                    double vowelSpeed = dstVowelMs > 0 ? srcVowelMs / dstVowelMs : 1.0;

                    for (int i = 0; i < tDst.Length; ++i) {
                        double dstMs = i * cfg.frame_ms;
                        if (dstMs < dstConsonantMs) {
                            double srcMs = dstMs * consonantSpeed;
                            tDst[i] = srcMs / cfg.frame_ms + srcStartFrame;
                        } else {
                            double vowelMs = dstMs - dstConsonantMs;
                            double srcMs = srcConsonantMs + vowelMs * vowelSpeed;
                            tDst[i] = srcMs / cfg.frame_ms + srcStartFrame;
                        }
                    }
                }

                var f0Dst = np.ndarray(new Shape(tDst.Length), typeof(double));
                var spEnvDst = np.ndarray(new Shape(tDst.Length, spEnvSrc.shape[1]), typeof(double));
                var apDst = np.ndarray(new Shape(tDst.Length, apSrc.shape[1]), typeof(double));
                int spSize = (int)spEnvSrc.shape[1];


                Log.Information($"[SynthSegment] Interpolation: tDst.Length={tDst.Length}, f0Src.Length={f0Src.Length}, srcStartFrame={srcStartFrame}, srcEndFrame={srcEndFrame}");

                // Use unsafe pointers for fast memory access instead of slow NDArray indexers
                unsafe {
                    double* f0DstPtr = (double*)f0Dst.Data<double>().Address;
                    double* spEnvSrcPtr = (double*)spEnvSrc.Data<double>().Address;
                    double* spEnvDstPtr = (double*)spEnvDst.Data<double>().Address;
                    double* apSrcPtr = (double*)apSrc.Data<double>().Address;
                    double* apDstPtr = (double*)apDst.Data<double>().Address;

                    for (int i = 0; i < tDst.Length; ++i) {
                        double pos = tDst[i];
                        // Clamp pos to valid range [0, f0Src.Length - 1]
                        pos = Math.Max(0, Math.Min(f0Src.Length - 1, pos));
                        int index = (int)Math.Floor(pos);
                        double frac = pos - index;

                        if (index < 0 || index >= f0Src.Length) {
                            Log.Error($"[SynthSegment] ❌ Index out of range at i={i}: pos={pos}, index={index}, f0Src.Length={f0Src.Length}");
                            throw new IndexOutOfRangeException($"Index {index} out of range for f0Src (length {f0Src.Length}) at i={i}, pos={pos}");
                        }

                        if (index + 1 < f0Src.Length) {
                            f0DstPtr[i] = f0Src[index] * (1.0 - frac) + f0Src[index + 1] * frac;
                            // Interpolate 2D array rows using pointer arithmetic
                            int srcRow0 = index * spSize;
                            int srcRow1 = (index + 1) * spSize;
                            int dstRow = i * spSize;
                            for (int j = 0; j < spSize; ++j) {
                                spEnvDstPtr[dstRow + j] = spEnvSrcPtr[srcRow0 + j] * (1.0 - frac) + spEnvSrcPtr[srcRow1 + j] * frac;
                                apDstPtr[dstRow + j] = apSrcPtr[srcRow0 + j] * (1.0 - frac) + apSrcPtr[srcRow1 + j] * frac;
                            }
                        } else {
                            f0DstPtr[i] = f0Src[index];
                            // Copy 2D array rows using pointer arithmetic
                            int srcRow = index * spSize;
                            int dstRow = i * spSize;
                            for (int j = 0; j < spSize; ++j) {
                                spEnvDstPtr[dstRow + j] = spEnvSrcPtr[srcRow + j];
                                apDstPtr[dstRow + j] = apSrcPtr[srcRow + j];
                            }
                        }
                    }
                }
                // Console.WriteLine($"[SynthSegment] ⏱️ Interpolation: {sw.ElapsedMilliseconds}ms");

                f0 = f0Dst;
                spEnv = spEnvDst;
                ap = apDst;

                skipFrames = (int)Math.Round(skipMs / cfg.frame_ms);
                p0 = (int)Math.Round(posMs / cfg.frame_ms);
                p1 = (int)Math.Round((posMs + fadeInMs) / cfg.frame_ms);
                p3 = (int)Math.Round((posMs + lengthMs - fadeOutMs) / cfg.frame_ms);
                p4 = (int)Math.Round((posMs + lengthMs) / cfg.frame_ms);
                p0 = Math.Max(0, p0);
                p1 = Math.Max(p0 + 1, p1);
                p3 = Math.Min(p4 - 1, p3);

                totalSw.Stop();
                // Console.WriteLine($"[SynthSegment] ✅ Total: {totalSw.ElapsedMilliseconds}ms");
            }

            float GetAutoGain(float[] samples, double[] f0, float wavMax, int peakComp) {
                float segMax = samples.Max(s => Math.Abs(s));
                double voicedRatio = f0.Count(f => f > config.f0_floor) / (double)f0.Length;
                double weight = 1.0 / (1.0 + Math.Exp(5.0 - 10.0 * voicedRatio));
                float max = segMax * (float)weight + wavMax * (1.0f - (float)weight);
                float autoGain = (max < 1e-3f) ? 1.0f : (float)Math.Pow(0.5 / max, peakComp * 0.01);
                return autoGain;
            }
        }

        public class PhraseSynthV2 {
            readonly AnalysisConfig config;
            readonly List<SynthSegment> segments = new List<SynthSegment>();

            double[]? f0Curve;
            double[]? genderCurve;
            double[]? tensionCurve;
            double[]? breathinessCurve;
            double[]? voicingCurve;

            public PhraseSynthV2(int fs, int hopSize, int fftSize) {
                config = InitAnalysisConfig(fs, hopSize, fftSize);
            }

            public void AddRequest(ResamplerItem item,
                double posMs, double skipMs, double lengthMs,
                double fadeInMs, double fadeOutMs) {
                segments.Add(new SynthSegment(config, item,
                    posMs, skipMs, lengthMs, fadeInMs, fadeOutMs));
            }

            public void SetCurves(
                double[] f0, double[] gender,
                double[] tension, double[] breathiness,
                double[] voicing) {
                f0Curve = f0;
                genderCurve = gender;
                tensionCurve = tension;
                breathinessCurve = breathiness;
                voicingCurve = voicing;
            }

            public (int, NDArray, NDArray, NDArray) SynthFeatures() {
                int spSize = config.fft_size / 2 + 1;
                int totalFrames = segments.Max(s => s.p4) + 1;
                NDArray f0Out = np.zeros<double>(totalFrames);
                NDArray spEnvOut = np.full<double>(1e-12, new int[] { totalFrames, spSize });
                NDArray apOut = np.full<double>(1.0, new int[] { totalFrames, spSize });
                NDArray dirty = np.zeros<int>(totalFrames);

                for (int i = 0; i < segments.Count; ++i) {
                    var segment = segments[i];
                    for (int j = segment.p0; j < segment.p4; ++j) {
                        double weight = 1.0;
                        if (j < segment.p1) {
                            weight = (double)(j - segment.p0) / (segment.p1 - segment.p0);
                        } else if (j >= segment.p3) {
                            weight = (double)(segment.p4 - j) / (segment.p4 - segment.p3);
                        }
                        int segIdx = segment.skipFrames + j - segment.p0;
                        if (dirty.GetAtIndex<int>(j) == 0 || weight > 0.5) {
                            f0Out[j] = segment.f0[segIdx];
                        }
                        spEnvOut[j] = spEnvOut[j] + segment.spEnv[segIdx] * weight;
                        double wa = dirty.GetAtIndex<int>(j) == 0 ? 0.0 : (1.0 - weight);
                        double wb = dirty.GetAtIndex<int>(j) == 0 ? 1.0 : weight;
                        apOut[j] = apOut[j] * wa + segment.ap[segIdx] * wb;
                        dirty[j] = 1;
                    }
                }

                if (f0Curve != null) {
                    for (int i = 0; i < totalFrames; ++i) {
                        if (f0Out.GetAtIndex<double>(i) > config.f0_floor) {
                            f0Out[i] = f0Curve[i];
                        }
                    }
                }

                return (totalFrames, f0Out, spEnvOut, apOut);
            }

            public float[] Synth() {
                // Console.WriteLine($"[PhraseSynthV2] Synth() called. Segments count: {segments.Count}");
                if (segments.Count == 0) {
                    return new float[0];
                }
                var (totalFrames, f0Out, spEnvOut, apOut) = SynthFeatures();
                int spSize = config.fft_size / 2 + 1;
                double[] f0Array = f0Out.ToArray<double>();
                double[] spEnvArray = spEnvOut.ToArray<double>();
                double[] apArray = apOut.ToArray<double>();
                // Console.WriteLine($"[PhraseSynthV2] Calling WorldSynthesis with f0Array len: {f0Array.Length}");
                double[] samples = WorldSynthesis(
                    f0Array,
                    spEnvArray, false, spSize,
                    apArray, false, config.fft_size,
                    config.frame_ms, config.fs,
                    genderCurve ?? Enumerable.Repeat(0.5, totalFrames).ToArray(),
                    tensionCurve ?? Enumerable.Repeat(0.5, totalFrames).ToArray(),
                    breathinessCurve ?? Enumerable.Repeat(0.5, totalFrames).ToArray(),
                    voicingCurve ?? Enumerable.Repeat(1.0, totalFrames).ToArray());
                // Console.WriteLine($"[PhraseSynthV2] WorldSynthesis returned {samples.Length} samples.");
                return samples.Select(s => (float)s).ToArray();
            }
        }
    }
}
