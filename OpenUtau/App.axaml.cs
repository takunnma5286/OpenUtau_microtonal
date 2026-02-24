using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using OpenUtau.App.Views;
using OpenUtau.Colors;
using Serilog;
using OpenUtau.Core;
using OpenUtau.Classic;
using System.Threading.Tasks;

namespace OpenUtau.App {
    public class App : Application {
        public override void Initialize() {
            Log.Information("Initializing application.");
            AvaloniaXamlLoader.Load(this);
            InitializeCulture();
            InitializeTheme();
            Log.Information("Initialized application.");
        }

        public override void OnFrameworkInitializationCompleted() {
            Log.Information("Framework initialization completed.");
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
                desktop.MainWindow = new SplashWindow();
            } else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView) {
                try {
                    ToolsManager.Inst.Initialize();
                } catch (System.Exception ex) {
                    Log.Error(ex, "Failed to initialize ToolsManager");
                }
                try {
                    SingerManager.Inst.Initialize();
                } catch (System.Exception ex) {
                    Log.Error(ex, "Failed to initialize SingerManager");
                }
                DocManager.Inst.Initialize(Thread.CurrentThread, TaskScheduler.FromCurrentSynchronizationContext());
                DocManager.Inst.PostOnUIThread = action => Avalonia.Threading.Dispatcher.UIThread.Post(action);

                DocManager.Inst.PostOnUIThread = action => Avalonia.Threading.Dispatcher.UIThread.Post(action);


                try {
                    singleView.MainView = new MainView();
                    (singleView.MainView as MainView)?.InitProject();
                } catch (System.Exception ex) {
                    Log.Error(ex, "Failed to initialize MainView");
                    System.Console.WriteLine("CRITICAL ERROR: " + ex.ToString());
                }

                Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (sender, e) => {
                    Log.Error(e.Exception, "Global Unhandled Exception (UI Thread)");
                    System.Console.WriteLine("GLOBAL UNHANDLED EXCEPTION (UI): " + e.Exception.ToString());
                    e.Handled = true;
                };

                TaskScheduler.UnobservedTaskException += (sender, e) => {
                    Log.Error(e.Exception, "Global Unobserved Task Exception");
                    System.Console.WriteLine("GLOBAL IDLE TASK EXCEPTION: " + e.Exception.ToString());
                    e.SetObserved();
                };
            }

            base.OnFrameworkInitializationCompleted();
        }

        public void InitializeCulture() {
            Log.Information("Initializing culture.");
            string sysLang = CultureInfo.InstalledUICulture.Name;
            string prefLang = Core.Util.Preferences.Default.Language;
            var languages = GetLanguages();
            if (languages.ContainsKey(prefLang)) {
                SetLanguage(prefLang);
            } else if (languages.ContainsKey(sysLang)) {
                SetLanguage(sysLang);
                Core.Util.Preferences.Default.Language = sysLang;
                Core.Util.Preferences.Save();
            } else {
                SetLanguage("en-US");
            }

            // Force using InvariantCulture to prevent issues caused by culture dependent string conversion, especially for floating point numbers.
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
            Log.Information("Initialized culture.");
        }

        public static Dictionary<string, IResourceProvider> GetLanguages() {
            if (Current == null) {
                return new();
            }
            var result = new Dictionary<string, IResourceProvider>();
            foreach (string key in Current.Resources.Keys.OfType<string>()) {
                if (key.StartsWith("strings-") &&
                    Current.Resources.TryGetResource(key, ThemeVariant.Default, out var res) &&
                    res is IResourceProvider rp) {
                    result.Add(key.Replace("strings-", ""), rp);
                }
            }
            return result;
        }

        public static void SetLanguage(string language) {
            if (Current == null) {
                return;
            }
            var languages = GetLanguages();
            foreach (var res in languages.Values) {
                Current.Resources.MergedDictionaries.Remove(res);
            }
            if (language != "en-US") {
                Current.Resources.MergedDictionaries.Add(languages["en-US"]);
            }
            if (languages.TryGetValue(language, out var res1)) {
                Current.Resources.MergedDictionaries.Add(res1);
            }
        }

        static void InitializeTheme() {
            Log.Information("Initializing theme.");
            SetTheme();
            Log.Information("Initialized theme.");
        }

        static ResourceDictionary? _themeOverrides;

        public static void SetTheme() {
            if (Current == null) {
                return;
            }
            var light = (IResourceProvider)Current.Resources["themes-light"]!;
            var dark = (IResourceProvider)Current.Resources["themes-dark"]!;
            var custom = (IResourceProvider)Current.Resources["themes-custom"]!;
            Current.Resources.MergedDictionaries.Remove(light);
            Current.Resources.MergedDictionaries.Remove(dark);
            Current.Resources.MergedDictionaries.Remove(custom);
            if (_themeOverrides != null) {
                Current.Resources.MergedDictionaries.Remove(_themeOverrides);
                _themeOverrides = null;
            }

            if (Core.Util.Preferences.Default.Theme == 0) {
                Current.Resources.MergedDictionaries.Add(light);
                Current.RequestedThemeVariant = ThemeVariant.Light;
            }
            if (Core.Util.Preferences.Default.Theme == 1) {
                Current.Resources.MergedDictionaries.Add(dark);
                Current.RequestedThemeVariant = ThemeVariant.Dark;
            }
            if (Core.Util.Preferences.Default.Theme == 2) {
                Current.Resources.MergedDictionaries.Add(custom);
                _themeOverrides = new ResourceDictionary();
                CustomTheme.ApplyTheme(_themeOverrides);
                Current.Resources.MergedDictionaries.Add(_themeOverrides);

                if (CustomTheme.Default.IsDarkMode == true) {
                    Current.RequestedThemeVariant = ThemeVariant.Dark;
                } else {
                    Current.RequestedThemeVariant = ThemeVariant.Light;
                }
            }
            ThemeManager.LoadTheme();
        }
    }
}
