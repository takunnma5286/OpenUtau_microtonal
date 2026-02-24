using System.Runtime.Versioning;
using System.Threading.Tasks;
using Serilog;
using Avalonia;
using Avalonia.Browser;
using Avalonia.ReactiveUI;
using Avalonia.Media;
using OpenUtau.App;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.InteropServices;

[assembly: SupportedOSPlatform("browser")]

internal partial class Program {

    private static string GetConsoleSinkAndTheme(out Serilog.Core.LoggingLevelSwitch levelSwitch) {
        levelSwitch = new Serilog.Core.LoggingLevelSwitch();
        return "console";
    }

    private static async Task Main(string[] args) {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        var sb = new System.Text.StringBuilder();

        try {
            if (System.OperatingSystem.IsBrowser()) {
                await System.Runtime.InteropServices.JavaScript.JSHost.ImportAsync("AppBundle/audio.js", "../AppBundle/audio.js?v=" + System.DateTime.Now.Ticks);

                // Import and initialize Worldline WASM bridge
                System.Console.WriteLine("[Program] Importing worldline_bridge module...");
                await System.Runtime.InteropServices.JavaScript.JSHost.ImportAsync("worldline_bridge", "../AppBundle/worldline_bridge.js?v=" + System.DateTime.Now.Ticks);
                System.Console.WriteLine("[Program] Initializing Worldline WASM...");
                await OpenUtau.Core.Render.Worldline.InitWorldlineAsync();
                System.Console.WriteLine("[Program] Worldline WASM initialized successfully!");

                // Import file picker JS
                System.Console.WriteLine("[Program] Importing file_picker module...");
                await System.Runtime.InteropServices.JavaScript.JSHost.ImportAsync("AppBundle/file_picker.js", "../AppBundle/file_picker.js?v=" + System.DateTime.Now.Ticks);
                OpenUtau.App.FilePicker.WasmFilePickerImplementation = OpenUtau.Browser.FilePickerInterop.PickFiles;
                System.Console.WriteLine("[Program] Hooked up WASM file picker.");

                sb.AppendLine("Testing WorldlineTest (via JavaScript Interop Bridge)...");
                try {
                    int val = OpenUtau.Core.Render.Worldline.WorldlineTest();
                    sb.AppendLine($"WorldlineTest: SUCCESS {val}");
                    System.Console.WriteLine($"[Program] WorldlineTest: SUCCESS {val}");
                } catch (System.Exception ex) {
                    sb.AppendLine($"WorldlineTest: FAIL {ex.GetType().Name} : {ex.Message}");
                    System.Console.WriteLine($"[Program] WorldlineTest: FAIL {ex}");
                }

                var logs = sb.ToString();
                try {
                    var globalThis = System.Runtime.InteropServices.JavaScript.JSHost.GlobalThis;
                    globalThis.SetProperty("_mallocTestResults", logs);
                    System.Console.WriteLine("[ANTIGRAVITY] Results written to window._mallocTestResults");
                } catch (System.Exception ex) {
                    System.Console.WriteLine($"[ANTIGRAVITY] Failed to write to global: {ex}");
                }
            }


            OpenUtau.Core.PlaybackManager.Inst.AudioOutput = new OpenUtau.Audio.WebAudioOutput();

            // Force load the built-in plugins assembly BEFORE DocManager initialization
            System.Console.WriteLine("[Program] Force loading OpenUtau.Plugin.Builtin assembly...");
            try {
                // Method 1: Reference a type to force assembly load
                var forceLoad = typeof(OpenUtau.Plugin.Builtin.JapaneseVCVPhonemizer);
                System.Console.WriteLine($"[Program] Type loaded: {forceLoad.FullName}");

                // Method 2: Explicitly load the assembly
                var builtinAsm = System.Reflection.Assembly.Load("OpenUtau.Plugin.Builtin");
                System.Console.WriteLine($"[Program] ✅ Assembly loaded: {builtinAsm.FullName}");
            } catch (System.Exception ex) {
                System.Console.WriteLine($"[Program] ⚠️ Failed to force-load builtin: {ex.Message}");
            }

            // Initialize DocManager to load phonemizers
            System.Console.WriteLine("[Program] Initializing DocManager...");
            OpenUtau.Core.DocManager.Inst.Initialize(
                System.Threading.Thread.CurrentThread,
                System.Threading.Tasks.TaskScheduler.Default
            );
            System.Console.WriteLine("[Program] DocManager initialized.");

            // Initialize Serilog
            Serilog.Log.Logger = new Serilog.LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console()
                .CreateLogger();
        } catch (System.Exception ex) {
            System.Console.WriteLine($"Audio init error: {ex}");
        }

        System.Console.WriteLine("Program.Main: Starting Avalonia...");
        try {
            await BuildAvaloniaApp().StartBrowserAppAsync("out");
        } catch (System.Exception ex) {
            System.Console.WriteLine($"Program.Main: Critical Exception: {ex}");
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UseReactiveUI()
            .With(new FontManagerOptions {
                DefaultFamilyName = "avares://OpenUtau.Browser/Assets/Fonts/NotoSansJP-Regular.otf#Noto Sans JP",
                FontFallbacks = new[] {
                    new FontFallback { FontFamily = new FontFamily("avares://OpenUtau.Browser/Assets/Fonts/NotoSansJP-Regular.otf#Noto Sans JP") }
                }
            });
}
