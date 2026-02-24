using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using OpenUtau.Core;
using OpenUtau.Core.Util;

namespace OpenUtau.App {
    public partial class FilePicker {
        public static FilePickerFileType ProjectFiles { get; } = new("Project Files") {
            Patterns = new[] { "*.ustx", "*.vsqx", "*.ust", "*.mid", "*.midi", "*.ufdata", "*.musicxml" },
        };
        public static FilePickerFileType USTX { get; } = new("USTX") {
            Patterns = new[] { "*.ustx" },
        };
        public static FilePickerFileType VSQX { get; } = new("VSQX") {
            Patterns = new[] { "*.vsqx" },
        };
        public static FilePickerFileType UST { get; } = new("UST") {
            Patterns = new[] { "*.ust" },
        };
        public static FilePickerFileType MIDI { get; } = new("MIDI") {
            Patterns = new[] { "*.mid", "*.midi" },
        };
        public static FilePickerFileType UFDATA { get; } = new("UFDATA") {
            Patterns = new[] { "*.ufdata" },
        };
        public static FilePickerFileType MUSICXML { get; } = new("MUSICXML") {
            Patterns = new[] { "*.musicxml" },
        };
        public static FilePickerFileType AudioFiles { get; } = new("Audio Files") {
            Patterns = new[] { "*.wav", "*.mp3", "*.ogg", "*.opus", "*.flac" },
        };
        public static FilePickerFileType WAV { get; } = new("WAV") {
            Patterns = new[] { "*.wav" },
        };
        public static FilePickerFileType ArchiveFiles { get; } = new("Archive File") {
            Patterns = new[] { "*.zip", "*.rar", "*.uar", "*.vogeon", "*.oudep" },
        };
        public static FilePickerFileType ZIP { get; } = new("ZIP") {
            Patterns = new[] { "*.zip" },
        };
        public static FilePickerFileType EXE { get; } = new("EXE") {
            Patterns = new[] { "*.exe" },
        };
        public static FilePickerFileType APP { get; } = new("APP") {
            Patterns = new[] { "*.app" },
        };
        public static FilePickerFileType PrefixMap { get; } = new("Prefix Map") {
            Patterns = new[] { "*.map" },
        };
        public static FilePickerFileType DS { get; } = new("DS") {
            Patterns = new[] { "*.ds" },
        };
        public static FilePickerFileType OUDEP { get; } = new("OpenUtau dependency") {
            Patterns = new[] { "*.oudep" },
        };
        public static FilePickerFileType UnixExecutable { get; } = new("Executable") {
            MimeTypes = new[] { "application/x-executable" },
            AppleUniformTypeIdentifiers = new[] { "public.unix-executable" },
        };
        public static FilePickerFileType TUN { get; } = new("AnaMark Tuning File") {
            Patterns = new[] { "*.tun" },
        };

        // Delegate to be implemented by OpenUtau.Browser for WASM file picking
        public static Func<string, bool, Task<string[]>>? WasmFilePickerImplementation;

        public async static Task<string?> OpenFile(
            TopLevel window, string titleKey, params FilePickerFileType[] types) {
            return await OpenFile(window, titleKey, null, types);
        }

        public async static Task<string?> OpenFileAboutSinger(
            TopLevel window, string titleKey, params FilePickerFileType[] types) {
            var path = await OpenFile(window, titleKey, Preferences.Default.RecentOpenSingerDirectory, types);
            var dir = Path.GetDirectoryName(path);
            if (dir != null) {
                Preferences.Default.RecentOpenSingerDirectory = dir;
                Preferences.Save();
            }
            return path;
        }

        public async static Task<string?> OpenFileAboutProject(
            TopLevel window, string titleKey, params FilePickerFileType[] types) {
            var path = await OpenFile(window, titleKey, Preferences.Default.RecentOpenProjectDirectory, types);
            var dir = Path.GetDirectoryName(path);
            if (dir != null) {
                Preferences.Default.RecentOpenProjectDirectory = dir;
                Preferences.Save();
            }
            return path;
        }

        public async static Task<string?> OpenFile(
            TopLevel window, string titleKey, string? startLocation, params FilePickerFileType[] types) {
            if (OS.IsWasm()) {
                if (WasmFilePickerImplementation != null) {
                    // Use custom JS file picker logic
                    var accept = string.Join(",", types.SelectMany(t => t.Patterns ?? Array.Empty<string>()));
                    // Patterns are like "*.ustx", inputs accept ".ustx"
                    accept = accept.Replace("*", "");

                    System.Console.WriteLine($"[FilePicker] calling WasmFilePickerImplementation (Single), accept={accept}");
                    var results = await WasmFilePickerImplementation(accept, false);
                    var result = results?.FirstOrDefault();
                    System.Console.WriteLine($"[FilePicker] WasmFilePickerImplementation (Single) returned: {result ?? "null"}");
                    return result;
                }
            }

            var location = startLocation == null
                ? null
                : await window.StorageProvider.TryGetFolderFromPathAsync(startLocation);
            var files = await window.StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions() {
                    Title = ThemeManager.GetString(titleKey),
                    AllowMultiple = false,
                    FileTypeFilter = types,
                    SuggestedStartLocation = location,
                });
            if (files == null || files.Count == 0) {
                return null;
            }
            var file = files[0];
            var pathArg = file.TryGetLocalPath();
            if (pathArg == null) {
                pathArg = Path.Combine(PathManager.Inst.CachePath, file.Name);
                using (var source = await file.OpenReadAsync())
                using (var dest = File.Create(pathArg)) {
                    await source.CopyToAsync(dest);
                }
            }
            return pathArg;
        }

        public async static Task<string[]?> OpenFilesAboutProject(
                TopLevel window, string titleKey, params FilePickerFileType[] types) {
            var result = await OpenFiles(window, titleKey, Preferences.Default.RecentOpenProjectDirectory, types);
            if (result != null) {
                var dir = Path.GetDirectoryName(result.FirstOrDefault());
                if (dir != null) {
                    Preferences.Default.RecentOpenProjectDirectory = dir;
                    Preferences.Save();
                }
            }
            return result;
        }

        public async static Task<string[]?> OpenFiles(
            TopLevel window, string titleKey, string? startLocation, params FilePickerFileType[] types) {
            if (OS.IsWasm()) {
                if (WasmFilePickerImplementation != null) {
                    // Use custom JS file picker logic
                    var accept = string.Join(",", types.SelectMany(t => t.Patterns ?? Array.Empty<string>()));
                    accept = accept.Replace("*", "");

                    System.Console.WriteLine($"[FilePicker] calling WasmFilePickerImplementation, accept={accept}");
                    var results = await WasmFilePickerImplementation(accept, true);
                    System.Console.WriteLine($"[FilePicker] WasmFilePickerImplementation returned {results?.Length ?? 0} results");
                    if (results != null) {
                        foreach (var res in results) {
                            System.Console.WriteLine($"[FilePicker] Result: {res}");
                        }
                    }
                    return results;
                }
            }

            var location = startLocation == null
                ? null
                : await window.StorageProvider.TryGetFolderFromPathAsync(startLocation);
            var files = await window.StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions() {
                    Title = ThemeManager.GetString(titleKey),
                    AllowMultiple = true,
                    FileTypeFilter = types,
                    SuggestedStartLocation = location
                });
            if (files == null || files.Count == 0) {
                return null;
            }
            var pathsArg = new System.Collections.Generic.List<string>();
            foreach (var file in files) {
                var path = file.TryGetLocalPath();
                if (path == null) {
                    path = Path.Combine(PathManager.Inst.CachePath, file.Name);
                    using (var source = await file.OpenReadAsync())
                    using (var dest = File.Create(path)) {
                        await source.CopyToAsync(dest);
                    }
                }
                pathsArg.Add(path);
            }
            return pathsArg.ToArray();
        }

        public async static Task<string?> OpenFolderAboutSinger(TopLevel window, string titleKey, Action<string>? logger = null) {
            var dir = await OpenFolder(window, titleKey, Preferences.Default.RecentOpenSingerDirectory, logger);
            if (dir != null) {
                Preferences.Default.RecentOpenSingerDirectory = dir;
                Preferences.Save();
            }
            return dir;
        }

        public async static Task<string?> OpenFolder(TopLevel window, string titleKey, string? startLocation, Action<string>? logger = null) {
            if (OS.IsWasm()) {
                startLocation = null;
            }
            var location = startLocation == null
                ? null
                : await window.StorageProvider.TryGetFolderFromPathAsync(startLocation);
            var dirs = await window.StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions {
                    Title = ThemeManager.GetString(titleKey),
                    AllowMultiple = false,
                    SuggestedStartLocation = location
                });
            if (dirs == null || dirs.Count == 0) {
                return null;
            }
            var item = dirs[0];
            var path = item.TryGetLocalPath();

            if (path == null) {
                // WASM Fallback: Copy folder to internal storage
                try {
                    var destBasePath = Path.Combine(PathManager.Inst.DataPath, "ImportedSingers");
                    var destFolder = Path.Combine(destBasePath, item.Name);

                    if (item is IStorageFolder storageFolder) {
                        // Console.WriteLine($"FilePicker.OpenFolder: Importing folder '{item.Name}' to '{destFolder}'...");
                        logger?.Invoke($"Importing folder '{item.Name}'...");

                        OpenUtau.App.Views.MessageBox.ProgressDialogController? dialog = null;
                        if (logger == null) {
                            dialog = OpenUtau.App.Views.MessageBox.ShowProgress(window, "Importing...", $"Importing {item.Name}...");
                        }

                        try {
                            await CopyFolderAsync(storageFolder, destFolder, logger, dialog);
                        } finally {
                            dialog?.Close();
                        }

                        // Console.WriteLine("FilePicker.OpenFolder: Import complete.");
                        logger?.Invoke("Import complete.");
                        path = destFolder;
                    }
                } catch (Exception e) {
                    // Console.WriteLine($"FilePicker.OpenFolder: Import failed. {e}");
                    logger?.Invoke($"Import failed: {e.Message}");
                }
            }

            return path;
        }

        // ... SaveFile ...

        // Recursive copy function
        async static Task CopyFolderAsync(IStorageFolder source, string destDir, Action<string>? logger = null, OpenUtau.App.Views.MessageBox.ProgressDialogController? progress = null) {
            Directory.CreateDirectory(destDir);
            await foreach (var subItem in source.GetItemsAsync()) {
                // Console.WriteLine($"Found item (recursive): '{subItem.Name}'");
                if (subItem is IStorageFile file) {
                    // Console.WriteLine($"Importing: {subItem.Name}");
                    progress?.SetText($"Importing {subItem.Name}...");
                    logger?.Invoke($"Importing: {subItem.Name}");
                    try {
                        using (var src = await file.OpenReadAsync())
                        using (var ms = new MemoryStream()) {
                            await src.CopyToAsync(ms);
                            ms.Position = 0;
                            using (var dst = File.Create(Path.Combine(destDir, subItem.Name))) {
                                ms.CopyTo(dst);
                            }
                        }
                        // Console.WriteLine($"  > Imported {subItem.Name}");
                    } catch (Exception ex) {
                        // Console.WriteLine($"  > FAILED to import {subItem.Name}: {ex}");
                        progress?.SetText($"Failed to import {subItem.Name}");
                        logger?.Invoke($"Failed to import {subItem.Name}: {ex.Message}");
                    }
                } else if (subItem is IStorageFolder folder) {
                    await CopyFolderAsync(folder, Path.Combine(destDir, subItem.Name), logger, progress);
                }
            }
        }

        public async static Task<string?> SaveFile
            (TopLevel window, string titleKey, params FilePickerFileType[] types) {
            return await SaveFile(window, titleKey, null, null, types);
        }

        public async static Task<string?> SaveFileAboutProject
            (TopLevel window, string titleKey, params FilePickerFileType[] types) {
            var path = await SaveFile(window, titleKey, Preferences.Default.RecentOpenProjectDirectory,
                Path.GetFileName(Path.ChangeExtension(DocManager.Inst.Project.FilePath, null)), types);
            var dir = Path.GetDirectoryName(path);
            if (dir != null) {
                Preferences.Default.RecentOpenProjectDirectory = dir;
                Preferences.Save();
            }
            return path;
        }

        public async static Task<string?> SaveFile
            (TopLevel window, string titleKey, string? startLocation, string? filename, params FilePickerFileType[] types) {
            if (OS.IsWasm()) {
                startLocation = null;
            }
            var location = startLocation == null
                ? null
                : await window.StorageProvider.TryGetFolderFromPathAsync(startLocation);

            var file = await window.StorageProvider.SaveFilePickerAsync(
                 new FilePickerSaveOptions {
                     Title = ThemeManager.GetString(titleKey),
                     FileTypeChoices = types,
                     ShowOverwritePrompt = true,
                     SuggestedStartLocation = location,
                     SuggestedFileName = filename,
                 });
            return file?.TryGetLocalPath();
        }

        // Recursive copy function (MemoryStream buffer to avoid partial file locks/deadlocks)
        async static Task CopyFolderAsync(IStorageFolder source, string destDir, OpenUtau.App.Views.MessageBox.ProgressDialogController? progress = null) {
            Directory.CreateDirectory(destDir);
            await foreach (var subItem in source.GetItemsAsync()) {
                // Console.WriteLine($"Found item: '{subItem.Name}'");
                if (subItem is IStorageFile file) {
                    // Console.WriteLine($"Importing: {subItem.Name}");
                    progress?.SetText($"Importing {subItem.Name}...");
                    try {
                        using (var src = await file.OpenReadAsync())
                        using (var ms = new MemoryStream()) {
                            await src.CopyToAsync(ms);
                            ms.Position = 0;
                            using (var dst = File.Create(Path.Combine(destDir, subItem.Name))) {
                                ms.CopyTo(dst);
                            }
                        }
                        // Console.WriteLine($"  > Imported {subItem.Name}");
                    } catch {
                        // Console.WriteLine($"  > FAILED to import {subItem.Name}: {ex}");
                        progress?.SetText($"Failed to import {subItem.Name}");
                    }
                } else if (subItem is IStorageFolder folder) {
                    await CopyFolderAsync(folder, Path.Combine(destDir, subItem.Name), progress);
                }
            }
        }
    }
}
