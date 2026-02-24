import { dotnet } from '../_framework/dotnet.js'

const is_browser = typeof window != "undefined";
if (!is_browser) throw new Error(`Expected to running in a browser`);

console.log("Starting dotnet runtime initialization...");
try {
    const dotnetRuntime = await dotnet
        .withDiagnosticTracing(true)
        .withApplicationArgumentsFromQuery()
        .create();

    console.log("Dotnet runtime instantiated. Getting config...");
    const config = dotnetRuntime.getConfig();
    console.log("Config retrieved:", config);

    // --- IDBFS Persistence Setup ---
    try {
        const Module = dotnetRuntime.Module;
        console.log("Mounting /data to IDBFS...");

        // Ensure /data exists
        try {
            if (!Module.FS.analyzePath('/data').exists) {
                Module.FS.mkdir('/data');
            }
        } catch (e) {
            console.warn("Error checking/creating /data: " + e);
            Module.FS.mkdir('/data');
        }

        // Mount IDBFS (needs -lidbfs.js in build flags)
        Module.FS.mount(Module.FS.filesystems.IDBFS, {}, '/data');

        // Initial Sync (Read from IndexedDB to Memory)
        console.log("Syncing /data from IDBFS...");
        await new Promise((resolve) => {
            Module.FS.syncfs(true, (err) => {
                if (err) {
                    console.error("IDBFS Sync Read Error: " + err);
                } else {
                    console.log("IDBFS Sync Read Done.");
                }
                resolve();
            });
        });

        // Periodic Sync (Write from Memory to IndexedDB)
        setInterval(() => {
            // console.log("Auto-syncing /data to IDBFS...");
            Module.FS.syncfs(false, (err) => {
                if (err) console.error("Auto-Sync Error: " + err);
            });
        }, 5000); // Sync every 5 seconds

    } catch (e) {
        console.error("Failed to setup IDBFS persistence: " + e);
    }
    // --------------------------------

    console.log("Running main assembly...");

    await dotnetRuntime.runMainAndExit(config.mainAssemblyName, [window.location.search]);
} catch (err) {
    console.error("CRITICAL ERROR during dotnet execution:", err);
    document.getElementById("out").innerHTML = `<h2 style="color:red">An error occurred: ${err}</h2>`;
}
