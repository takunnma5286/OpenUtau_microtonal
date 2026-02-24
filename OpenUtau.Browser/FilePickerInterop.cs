using System;
using System.IO;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;
using OpenUtau.Core;
using OpenUtau.Core.Util;

namespace OpenUtau.Browser {
    public partial class FilePickerInterop {
        [JSImport("openFile", "AppBundle/file_picker.js")]
        internal static partial Task<int> JsOpenFile(string accept, bool multiple);

        [JSImport("getFileName", "AppBundle/file_picker.js")]
        internal static partial string JsGetFileName(int index);

        [JSImport("loadFile", "AppBundle/file_picker.js")]
        internal static partial Task<int> JsLoadFile(int index);

        [JSImport("getLoadedFileData", "AppBundle/file_picker.js")]
        internal static partial byte[] JsGetLoadedFileData();

        [JSImport("freeLoadedFileData", "AppBundle/file_picker.js")]
        internal static partial void JsFreeLoadedFileData();

        public static async Task<string[]> PickFiles(string accept, bool multiple) {
            System.Console.WriteLine($"[FilePickerInterop] PickFiles called, accept={accept}, multiple={multiple}");
            int count = await JsOpenFile(accept, multiple);
            System.Console.WriteLine($"[FilePickerInterop] JsOpenFile returned {count} files.");
            if (count == 0) return Array.Empty<string>();

            var paths = new System.Collections.Generic.List<string>();
            for (int i = 0; i < count; i++) {
                var fileName = JsGetFileName(i);
                System.Console.WriteLine($"[FilePickerInterop] Processing file {i}: {fileName}");

                // Load data into JS memory first (async)
                int size = await JsLoadFile(i);
                System.Console.WriteLine($"[FilePickerInterop] JsLoadFile returned size {size}");
                if (size > 0) {
                    // Fetch data synchronously
                    var fileData = JsGetLoadedFileData();
                    System.Console.WriteLine($"[FilePickerInterop] JsGetLoadedFileData returned {fileData?.Length ?? -1} bytes");
                    JsFreeLoadedFileData(); // Clean up JS memory

                    if (fileData != null) {
                        try {
                            var cachePath = PathManager.Inst.CachePath;
                            System.Console.WriteLine($"[FilePickerInterop] CachePath: {cachePath}");
                            if (!Directory.Exists(cachePath)) {
                                System.Console.WriteLine($"[FilePickerInterop] Creating directory: {cachePath}");
                                Directory.CreateDirectory(cachePath);
                            }
                            var path = Path.Combine(cachePath, fileName);
                            System.Console.WriteLine($"[FilePickerInterop] Writing to {path} (Sync)...");
                            File.WriteAllBytes(path, fileData);
                            paths.Add(path);
                            System.Console.WriteLine($"[FilePickerInterop] Successfully wrote {fileName} to cache. Size: {fileData.Length}");
                        } catch (Exception ex) {
                            System.Console.WriteLine($"[FilePickerInterop] Error writing file: {ex}");
                        }
                    }
                }
            }
            return paths.ToArray();
        }
    }
}
