/**
 * JavaScript Interop Bridge for Worldline WASM Module
 * 
 * This module provides a bridge between .NET C# code (via JSImport)
 * and the native Worldline WebAssembly module compiled from C++.
 * 
 * Key responsibilities:
 * - Load and initialize the Worldline WASM module
 *  - Manage memory allocation/deallocation for data passed between .NET and WASM
 * - Convert between JavaScript/C# types and C types
 * - Provide error handling and logging
 */

// Global WASM module instance
let worldline = null;

/**
 * Helper function to load a script dynamically
 * @param {string} src - Script source URL
 * @returns {Promise<void>}
 */
function loadScript(src) {
    return new Promise((resolve, reject) => {
        const script = document.createElement('script');
        script.src = src;
        script.onload = () => resolve();
        script.onerror = () => reject(new Error(`Failed to load script: ${src}`));
        document.head.appendChild(script);
    });
}

/**
 * Initialize the Worldline WASM module
 * Must be called before any other worldline functions
 * @returns {Promise<void>}
 */
export async function initWorldline() {
    if (worldline) {
        console.log("[Worldline Bridge] Already initialized");
        return;
    }

    console.log("[Worldline Bridge] Loading WASM module...");

    try {
        // Load worldline.js as a script (it's built as CommonJS, not ES6 module)
        // It will create a global WorldlineModule variable
        if (typeof globalThis.WorldlineModule === 'undefined') {
            console.log("[Worldline Bridge] Loading worldline.js as script...");
            await loadScript('../worldline.js');
        }

        console.log("[Worldline Bridge] WorldlineModule type:", typeof globalThis.WorldlineModule);

        // Call the factory function to initialize the WASM module
        worldline = await globalThis.WorldlineModule({
            locateFile: (path) => {
                if (path.endsWith('.wasm')) {
                    return '/worldline.wasm';
                }
                return path;
            },
            print: (text) => console.log("[Worldline] " + text),
            printErr: (text) => console.error("[Worldline] " + text),
        });

        console.log("[Worldline Bridge] ✅ WASM module loaded successfully");
    } catch (error) {
        console.error("[Worldline Bridge] ❌ Failed to load WASM module:", error);
        throw error;
    }
}

// Rest of the file continues with export functions...
// (保持既存のWorldlineTest, F0, DecodeMgc等の関数)
