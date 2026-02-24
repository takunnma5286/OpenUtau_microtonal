/**
 * JavaScript Interop Bridge for Worldline WASM Module
 * 
 * This module provides a bridge between .NET C# code (via JSImport)
 * and the native Worldline WebAssembly module compiled from C++.
 * 
 * Key responsibilities:
 * - Load and initialize the Worldline WASM module
 * - Manage memory allocation/deallocation for data passed between .NET and WASM
 * - Convert between JavaScript/C# types and C types
 * - Provide error handling and logging
 */

// Global WASM module instance
let worldline = null;

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
        if (typeof globalThis.WorldlineModule === 'undefined') {
            await loadScript('../worldline.js');
        }

        // Call the factory function
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
// Add helper function before initWorldline()
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
 * Test function - returns 12345
 * @returns {number}
 */
export function WorldlineTest() {
    if (!worldline) throw new Error("Worldline not initialized");
    return worldline._WorldlineTest();
}

/**
 * F0 estimation
 * @param {Float32Array} samples - Input audio samples
 * @param {number} fs - Sample rate
 * @param {number} framePeriod - Frame period in ms
 * @param {number} method - F0 estimation method (0=DIO, 1=Harvest)
 * @returns {Float64Array} - F0 contour
 */
export function F0(samples, fs, framePeriod, method) {
    if (!worldline) throw new Error("Worldline not initialized");

    const length = samples.length;
    const samplesPtr = worldline._malloc(length * 4); // float = 4 bytes
    const f0PtrPtr = worldline._malloc(4); // pointer to pointer

    try {
        // Copy samples to WASM heap
        worldline.HEAPF32.set(samples, samplesPtr / 4);

        // Call C function
        const resultLength = worldline._F0(
            samplesPtr,
            length,
            fs,
            framePeriod,
            method,
            f0PtrPtr
        );

        if (resultLength <= 0) {
            return new Float64Array(0);
        }

        // Get the pointer to f0 array
        const f0Ptr = worldline.getValue(f0PtrPtr, 'i32');

        // Copy result from WASM heap to JavaScript
        const f0Array = new Float64Array(
            worldline.HEAPF64.buffer,
            f0Ptr,
            resultLength
        ).slice(); // slice() creates a copy

        // Free the C++-allocated f0 array
        worldline._free(f0Ptr);

        return f0Array;

    } finally {
        worldline._free(samplesPtr);
        worldline._free(f0PtrPtr);
    }
}

/**
 * F0 estimation wrapper for C# JSImport (accepts regular array)
 * @param {Array} samplesArray - Input audio samples as regular JS array
 * @param {number} fs - Sample rate
 * @param {number} framePeriod - Frame period in ms
 * @param {number} method - F0 estimation method (0=DIO, 1=Harvest)
 * @returns {Array} - F0 contour as regular JS array
 */
export function F0ForCSharp(samplesArray, fs, framePeriod, method) {
    // Convert regular array to Float32Array
    const samples = new Float32Array(samplesArray);

    // Call the original F0 function
    const result = F0(samples, fs, framePeriod, method);

    // Convert Float64Array result to regular array
    return Array.from(result);
}

/**
 * F0 estimation via global object (for C# JSImport without array parameters)
 * C# sets window._worldlineInput, calls this, then reads window._worldlineOutput
 * @param {number} fs - Sample rate
 * @param {number} framePeriod - Frame period in ms
 * @param {number} method - F0 estimation method (0=DIO, 1=Harvest)
 */
export function F0ViaGlobal(fs, framePeriod, method) {
    console.log("[F0ViaGlobal] Called with fs=%d, framePeriod=%f, method=%d", fs, framePeriod, method);

    // Parse JSON input
    const jsonStr = globalThis._worldlineInputJson;
    console.log("[F0ViaGlobal] JSON string length:", jsonStr ? jsonStr.length : "null");

    if (!jsonStr) {
        console.error("[F0ViaGlobal] ❌ Input JSON not found");
        throw new Error("Input JSON not found");
    }

    console.log("[F0ViaGlobal] Parsing JSON...");
    const samplesArray = JSON.parse(jsonStr);
    console.log("[F0ViaGlobal] ✅ Parsed array length:", samplesArray.length);

    console.log("[F0ViaGlobal] Converting to Float32Array...");
    const samples = new Float32Array(samplesArray);
    console.log("[F0ViaGlobal] Calling F0...");
    const result = F0(samples, fs, framePeriod, method);
    console.log("[F0ViaGlobal] ✅ F0 result length:", result.length);

    console.log("[F0ViaGlobal] Setting output...");
    globalThis._worldlineOutput = Array.from(result);
    console.log("[F0ViaGlobal] ✅ Complete");
}

/**
 * WorldAnalysisF0In - Analyze audio with provided F0
 * Wrapper for C# JSImport (uses global object for array transfer)
 * @param {number} fs - Sample rate
 * @param {number} hopSize - Hop size
 * @param {number} fftSize - FFT size
 * @param {number} f0Floor - F0 floor
 * @param {number} framems - Frame period in ms
 */
export function WorldAnalysisF0InViaGlobal(fs, hopSize, fftSize, f0Floor, frameMs) {
    const totalStart = performance.now();
    console.log("[WorldAnalysisF0InViaGlobal] Called");

    try {
        // Parse JSON with Base64 encoded binary data
        let t0 = performance.now();
        const inputJson = globalThis._worldlineInput;
        if (!inputJson) {
            throw new Error("Input JSON not found");
        }

        const input = JSON.parse(inputJson);
        console.log(`[WorldAnalysisF0InViaGlobal] ⏱️ JSON.parse: ${(performance.now() - t0).toFixed(1)}ms`);

        // Fast Base64 decoding using Uint8Array.from
        t0 = performance.now();
        const fromBase64Fast = (base64) => {
            const binary = atob(base64);
            return Uint8Array.from(binary, c => c.charCodeAt(0));
        };

        const samplesBytes = fromBase64Fast(input.samplesBase64);
        const f0InBytes = fromBase64Fast(input.f0InBase64);
        console.log(`[WorldAnalysisF0InViaGlobal] ⏱️ Base64 decode: ${(performance.now() - t0).toFixed(1)}ms`);

        // Create typed arrays from byte buffers
        const samples = new Float32Array(samplesBytes.buffer);
        const f0In = new Float64Array(f0InBytes.buffer);

        console.log(`[WorldAnalysisF0InViaGlobal] samples: ${samples.length}, f0: ${f0In.length}`);

        const numSamples = samples.length;
        const numFrames = f0In.length;
        const spSize = Math.floor(fftSize / 2) + 1;

        // Allocate memory
        t0 = performance.now();
        const samplesPtr = worldline._malloc(numSamples * 4);
        const f0InPtr = worldline._malloc(numFrames * 8);
        const spOutPtr = worldline._malloc(numFrames * spSize * 8);
        const apOutPtr = worldline._malloc(numFrames * spSize * 8);
        const configPtr = worldline._malloc(32);
        console.log(`[WorldAnalysisF0InViaGlobal] ⏱️ malloc: ${(performance.now() - t0).toFixed(1)}ms`);

        // Helper functions for memory access (fallback if HEAP views are missing)
        const setFloat32 = (ptr, arr) => {
            if (worldline.HEAPF32) {
                worldline.HEAPF32.set(arr, ptr >> 2);
            } else {
                // console.warn("[Worldline] HEAPF32 missing, using setValue fallback");
                for (let i = 0; i < arr.length; i++) {
                    worldline.setValue(ptr + i * 4, arr[i], 'float');
                }
            }
        };

        const setFloat64 = (ptr, arr) => {
            if (worldline.HEAPF64) {
                worldline.HEAPF64.set(arr, ptr >> 3);
            } else {
                // console.warn("[Worldline] HEAPF64 missing, using setValue fallback");
                for (let i = 0; i < arr.length; i++) {
                    worldline.setValue(ptr + i * 8, arr[i], 'double');
                }
            }
        };

        const getFloat64 = (ptr, len) => {
            if (worldline.HEAPF64) {
                const start = ptr >> 3;
                return worldline.HEAPF64.slice(start, start + len);
            } else {
                // console.warn("[Worldline] HEAPF64 missing, using getValue fallback");
                const res = new Float64Array(len);
                for (let i = 0; i < len; i++) {
                    res[i] = worldline.getValue(ptr + i * 8, 'double');
                }
                return res;
            }
        };

        try {
            // Copy input data to WASM heap
            t0 = performance.now();
            setFloat32(samplesPtr, samples);
            setFloat64(f0InPtr, f0In);

            // Initialize config
            worldline.setValue(configPtr + 0, fs, 'i32');
            worldline.setValue(configPtr + 4, hopSize, 'i32');
            worldline.setValue(configPtr + 8, fftSize, 'i32');
            worldline.setValue(configPtr + 12, f0Floor, 'float');
            worldline.setValue(configPtr + 16, frameMs, 'double');
            console.log(`[WorldAnalysisF0InViaGlobal] ⏱️ Copy to WASM heap: ${(performance.now() - t0).toFixed(1)}ms`);

            // Call WorldAnalysisF0In
            t0 = performance.now();
            worldline._WorldAnalysisF0In(
                configPtr,
                samplesPtr,
                numSamples,
                f0InPtr,
                numFrames,
                spOutPtr,
                apOutPtr
            );
            console.log(`[WorldAnalysisF0InViaGlobal] ⏱️ _WorldAnalysisF0In: ${(performance.now() - t0).toFixed(1)}ms`);

            // Read output arrays
            t0 = performance.now();
            const spOut = getFloat64(spOutPtr, numFrames * spSize);
            const apOut = getFloat64(apOutPtr, numFrames * spSize);
            console.log(`[WorldAnalysisF0InViaGlobal] ⏱️ Read from WASM heap: ${(performance.now() - t0).toFixed(1)}ms`);

            // Convert Float64Arrays to Base64 for efficient transfer
            t0 = performance.now();
            const spBytes = new Uint8Array(spOut.buffer, spOut.byteOffset, spOut.byteLength);
            const apBytes = new Uint8Array(apOut.buffer, apOut.byteOffset, apOut.byteLength);

            // Fast Base64 encoding using btoa with chunked processing
            const toBase64 = (bytes) => {
                let binary = '';
                const chunkSize = 32768; // Process in chunks to avoid stack overflow
                for (let i = 0; i < bytes.length; i += chunkSize) {
                    const chunk = bytes.subarray(i, Math.min(i + chunkSize, bytes.length));
                    binary += String.fromCharCode.apply(null, chunk);
                }
                return btoa(binary);
            };

            const spBase64 = toBase64(spBytes);
            const apBase64 = toBase64(apBytes);
            console.log(`[WorldAnalysisF0InViaGlobal] ⏱️ Base64 encode: ${(performance.now() - t0).toFixed(1)}ms`);

            // Store as simple object with Base64 encoded binary data
            t0 = performance.now();
            globalThis._worldlineOutput = JSON.stringify({
                spEnvBase64: spBase64,
                apBase64: apBase64,
                numFrames: numFrames,
                spSize: spSize
            });
            console.log(`[WorldAnalysisF0InViaGlobal] ⏱️ JSON.stringify: ${(performance.now() - t0).toFixed(1)}ms`);

            console.log(`[WorldAnalysisF0InViaGlobal] ✅ Complete - Total: ${(performance.now() - totalStart).toFixed(1)}ms`);
        } finally {
            worldline._free(samplesPtr);
            worldline._free(f0InPtr);
            worldline._free(spOutPtr);
            worldline._free(apOutPtr);
            worldline._free(configPtr);
        }
    } catch (error) {
        console.error("[WorldAnalysisF0InViaGlobal] ❌ Error:", error);
        console.error("[WorldAnalysisF0InViaGlobal] ❌ Stack:", error.stack);
        throw error;
    }
}

/**
 * WorldSynthesis - Synthesize audio from F0 and spectral parameters
 * Wrapper for C# JSImport (uses global object for array transfer)
 */
export function WorldSynthesisViaGlobal(isMgc, mgcSize, isBap, fftSize, framePeriod, fs) {
    console.log("[WorldSynthesisViaGlobal] Called");

    try {
        // Parse JSON inputs
        const inputJson = globalThis._worldlineInput;
        if (!inputJson) {
            throw new Error("Input JSON not found");
        }

        const input = JSON.parse(inputJson);
        const f0Array = input.f0;
        const mgcOrSpArray = input.mgcOrSp;
        const bapOrApArray = input.bapOrAp;
        const genderArray = input.gender;
        const tensionArray = input.tension;
        const breathinessArray = input.breathiness;
        const voicingArray = input.voicing;

        console.log(`[WorldSynthesisViaGlobal] f0: ${f0Array.length}, mgcOrSp: ${mgcOrSpArray.length}, params: ${genderArray.length}`);

        // Convert to typed arrays
        const f0 = new Float64Array(f0Array);
        const mgcOrSp = new Float64Array(mgcOrSpArray);
        const bapOrAp = new Float64Array(bapOrApArray);
        const gender = new Float64Array(genderArray);
        const tension = new Float64Array(tensionArray);
        const breathiness = new Float64Array(breathinessArray);
        const voicing = new Float64Array(voicingArray);

        const f0Length = f0.length;

        // Helper functions for memory access
        const setFloat64 = (ptr, arr) => {
            if (worldline.HEAPF64) {
                worldline.HEAPF64.set(arr, ptr >> 3);
            } else {
                for (let i = 0; i < arr.length; i++) {
                    worldline.setValue(ptr + i * 8, arr[i], 'double');
                }
            }
        };

        const getFloat64 = (ptr, len) => {
            if (worldline.HEAPF64) {
                const start = ptr >> 3;
                return worldline.HEAPF64.slice(start, start + len);
            } else {
                const res = new Float64Array(len);
                for (let i = 0; i < len; i++) {
                    res[i] = worldline.getValue(ptr + i * 8, 'double');
                }
                return res;
            }
        };

        // Allocate memory
        const f0Ptr = worldline._malloc(f0Length * 8);
        const mgcOrSpPtr = worldline._malloc(mgcOrSp.length * 8);
        const bapOrApPtr = worldline._malloc(bapOrAp.length * 8);
        const genderPtr = worldline._malloc(gender.length * 8);
        const tensionPtr = worldline._malloc(tension.length * 8);
        const breathinessPtr = worldline._malloc(breathiness.length * 8);
        const voicingPtr = worldline._malloc(voicing.length * 8);
        const yPtrPtr = worldline._malloc(4);

        try {
            // Copy input data
            setFloat64(f0Ptr, f0);
            setFloat64(mgcOrSpPtr, mgcOrSp);
            setFloat64(bapOrApPtr, bapOrAp);
            setFloat64(genderPtr, gender);
            setFloat64(tensionPtr, tension);
            setFloat64(breathinessPtr, breathiness);
            setFloat64(voicingPtr, voicing);

            // Call WorldSynthesis
            console.log("[WorldSynthesisViaGlobal] Calling _WorldSynthesis...");
            const resultLength = worldline._WorldSynthesis(
                f0Ptr, f0Length,
                mgcOrSpPtr, isMgc ? 1 : 0, mgcSize,
                bapOrApPtr, isBap ? 1 : 0, fftSize,
                framePeriod, fs, yPtrPtr,
                genderPtr, tensionPtr, breathinessPtr, voicingPtr
            );
            console.log("[WorldSynthesisViaGlobal] _WorldSynthesis completed, resultLength:", resultLength);

            if (resultLength <= 0) {
                globalThis._worldlineOutput = JSON.stringify([]);
                console.log("[WorldSynthesisViaGlobal] ✅ Complete (empty result)");
                return;
            }

            // Read output
            const yPtr = worldline.getValue(yPtrPtr, 'i32');
            const audio = getFloat64(yPtr, resultLength);

            // Free the C++-allocated audio array
            worldline._free(yPtr);

            // Store result as JSON
            globalThis._worldlineOutput = JSON.stringify(Array.from(audio));
            console.log("[WorldSynthesisViaGlobal] ✅ Complete, audio length:", resultLength);
        } finally {
            worldline._free(f0Ptr);
            worldline._free(mgcOrSpPtr);
            worldline._free(bapOrApPtr);
            worldline._free(genderPtr);
            worldline._free(tensionPtr);
            worldline._free(breathinessPtr);
            worldline._free(voicingPtr);
            worldline._free(yPtrPtr);
        }
    } catch (error) {
        console.error("[WorldSynthesisViaGlobal] ❌ Error:", error);
        console.error("[WorldSynthesisViaGlobal] ❌ Stack:", error.stack);
        throw error;
    }
}


/**
 * Decode MGC (Mel-Generalized Cepstrum) to spectrogram
 * @param {number} f0Length - Number of frames
 * @param {Float64Array} mgc - MGC coefficients (flattened 2D array: f0Length * mgcSize)
 * @param {number} mgcSize - MGC order
 * @param {number} fftSize - FFT size
 * @param {number} fs - Sample rate
 * @returns {Float64Array} - Spectrogram (flattened 2D array: f0Length * (fftSize/2+1))
 */
export function DecodeMgc(f0Length, mgc, mgcSize, fftSize, fs) {
    if (!worldline) throw new Error("Worldline not initialized");

    const mgcPtr = worldline._malloc(mgc.length * 8); // double = 8 bytes
    const spPtrPtr = worldline._malloc(4);

    try {
        worldline.HEAPF64.set(mgc, mgcPtr / 8);

        const resultLength = worldline._DecodeMgc(
            f0Length,
            mgcPtr,
            mgcSize,
            fftSize,
            fs,
            spPtrPtr
        );

        if (resultLength <= 0) {
            return new Float64Array(0);
        }

        const spPtr = worldline.getValue(spPtrPtr, 'i32');
        const spectrogram = new Float64Array(
            worldline.HEAPF64.buffer,
            spPtr,
            resultLength
        ).slice();

        return spectrogram;

    } finally {
        worldline._free(mgcPtr);
        worldline._free(spPtrPtr);
    }
}

/**
 * Decode BAP (Band Aperiodicity) to aperiodicity
 * @param {number} f0Length - Number of frames
 * @param {Float64Array} bap - BAP coefficients (flattened)
 * @param {number} fftSize - FFT size
 * @param {number} fs - Sample rate
 * @returns {Float64Array} - Aperiodicity (flattened)
 */
export function DecodeBap(f0Length, bap, fftSize, fs) {
    if (!worldline) throw new Error("Worldline not initialized");

    const bapPtr = worldline._malloc(bap.length * 8);
    const apPtrPtr = worldline._malloc(4);

    try {
        worldline.HEAPF64.set(bap, bapPtr / 8);

        const resultLength = worldline._DecodeBap(
            f0Length,
            bapPtr,
            fftSize,
            fs,
            apPtrPtr
        );

        if (resultLength <= 0) {
            return new Float64Array(0);
        }

        const apPtr = worldline.getValue(apPtrPtr, 'i32');
        const aperiodicity = new Float64Array(
            worldline.HEAPF64.buffer,
            apPtr,
            resultLength
        ).slice();

        return aperiodicity;

    } finally {
        worldline._free(bapPtr);
        worldline._free(apPtrPtr);
    }
}

/**
 * Initialize AnalysisConfig structure
 * @param {number} fs - Sample rate
 * @param {number} hopSize - Hop size in samples
 * @param {number} fftSize - FFT size
 * @returns {object} - Config object {fs, hop_size, fft_size, f0_floor, frame_ms}
 */
export function InitAnalysisConfig(fs, hopSize, fftSize) {
    if (!worldline) throw new Error("Worldline not initialized");

    // AnalysisConfig struct size: 5 fields * different sizes
    // int fs (4), int hop_size (4), int fft_size (4), float f0_floor (4), double frame_ms (8)
    // Total: 24 bytes (with potential padding)
    const configSize = 32; // Use safe size with padding
    const configPtr = worldline._malloc(configSize);

    try {
        worldline._InitAnalysisConfig(configPtr, fs, hopSize, fftSize);

        // Read struct fields (adjust offsets based on actual struct layout)
        const config = {
            fs: worldline.getValue(configPtr + 0, 'i32'),
            hop_size: worldline.getValue(configPtr + 4, 'i32'),
            fft_size: worldline.getValue(configPtr + 8, 'i32'),
            f0_floor: worldline.getValue(configPtr + 12, 'float'),
            frame_ms: worldline.getValue(configPtr + 16, 'double')
        };

        return config;

    } finally {
        worldline._free(configPtr);
    }
}

/**
 * World Analysis (full F0, spectral envelope, aperiodicity extraction)
 * @param {object} config - AnalysisConfig object
 * @param {Float32Array} samples - Input audio samples
 * @returns {object} - {f0, spEnv, ap, length}
 */
export function WorldAnalysis(config, samples) {
    if (!worldline) throw new Error("Worldline not initialized");

    const configSize = 32;
    const configPtr = worldline._malloc(configSize);
    const samplesPtr = worldline._malloc(samples.length * 4);
    const f0PtrPtr = worldline._malloc(4);
    const spEnvPtrPtr = worldline._malloc(4);
    const apPtrPtr = worldline._malloc(4);
    const lengthPtr = worldline._malloc(4);

    try {
        // Write config struct
        worldline.setValue(configPtr + 0, config.fs, 'i32');
        worldline.setValue(configPtr + 4, config.hop_size, 'i32');
        worldline.setValue(configPtr + 8, config.fft_size, 'i32');
        worldline.setValue(configPtr + 12, config.f0_floor, 'float');
        worldline.setValue(configPtr + 16, config.frame_ms, 'double');

        // Copy samples
        worldline.HEAPF32.set(samples, samplesPtr / 4);

        // Call analysis
        worldline._WorldAnalysis(
            configPtr,
            samplesPtr,
            samples.length,
            f0PtrPtr,
            spEnvPtrPtr,
            apPtrPtr,
            lengthPtr
        );

        const length = worldline.getValue(lengthPtr, 'i32');

        if (length <= 0) {
            return { f0: new Float64Array(0), spEnv: new Float64Array(0), ap: new Float64Array(0), length: 0 };
        }

        const f0Ptr = worldline.getValue(f0PtrPtr, 'i32');
        const spEnvPtr = worldline.getValue(spEnvPtrPtr, 'i32');
        const apPtr = worldline.getValue(apPtrPtr, 'i32');

        const fftSize = config.fft_size;
        const specDim = fftSize / 2 + 1;

        const f0 = new Float64Array(worldline.HEAPF64.buffer, f0Ptr, length).slice();
        const spEnv = new Float64Array(worldline.HEAPF64.buffer, spEnvPtr, length * specDim).slice();
        const ap = new Float64Array(worldline.HEAPF64.buffer, apPtr, length * specDim).slice();

        return { f0, spEnv, ap, length };

    } finally {
        worldline._free(configPtr);
        worldline._free(samplesPtr);
        worldline._free(f0PtrPtr);
        worldline._free(spEnvPtrPtr);
        worldline._free(apPtrPtr);
        worldline._free(lengthPtr);
    }
}

/**
 * World Synthesis
 * @param {Float64Array} f0 - F0 contour
 * @param {Float64Array} mgcOrSp - MGC or spectrogram (flattened 2D)
 * @param {boolean} isMgc - true if MGC, false if spectrogram
 * @param {number} mgcSize - MGC order (if isMgc=true)
 * @param {Float64Array} bapOrAp - BAP or aperiodicity (flattened 2D)
 * @param {boolean} isBap - true if BAP, false if aperiodicity
 * @param {number} fftSize - FFT size
 * @param {number} framePeriod - Frame period in ms
 * @param {number} fs - Sample rate
 * @param {Float64Array} gender - Gender parameter curve
 * @param {Float64Array} tension - Tension parameter curve
 * @param {Float64Array} breathiness - Breathiness parameter curve
 * @param {Float64Array} voicing - Voicing parameter curve
 * @returns {Float64Array} - Synthesized audio
 */
export function WorldSynthesis(
    f0, mgcOrSp, isMgc, mgcSize, bapOrAp, isBap, fftSize, framePeriod, fs,
    gender, tension, breathiness, voicing
) {
    if (!worldline) throw new Error("Worldline not initialized");

    const f0Length = f0.length;
    const f0Ptr = worldline._malloc(f0Length * 8);
    const mgcOrSpPtr = worldline._malloc(mgcOrSp.length * 8);
    const bapOrApPtr = worldline._malloc(bapOrAp.length * 8);
    const genderPtr = worldline._malloc(gender.length * 8);
    const tensionPtr = worldline._malloc(tension.length * 8);
    const breathinessPtr = worldline._malloc(breathiness.length * 8);
    const voicingPtr = worldline._malloc(voicing.length * 8);
    const yPtrPtr = worldline._malloc(4);

    try {
        worldline.HEAPF64.set(f0, f0Ptr / 8);
        worldline.HEAPF64.set(mgcOrSp, mgcOrSpPtr / 8);
        worldline.HEAPF64.set(bapOrAp, bapOrApPtr / 8);
        worldline.HEAPF64.set(gender, genderPtr / 8);
        worldline.HEAPF64.set(tension, tensionPtr / 8);
        worldline.HEAPF64.set(breathiness, breathinessPtr / 8);
        worldline.HEAPF64.set(voicing, voicingPtr / 8);

        const resultLength = worldline._WorldSynthesis(
            f0Ptr, f0Length,
            mgcOrSpPtr, isMgc ? 1 : 0, mgcSize,
            bapOrApPtr, isBap ? 1 : 0, fftSize,
            framePeriod, fs, yPtrPtr,
            genderPtr, tensionPtr, breathinessPtr, voicingPtr
        );

        if (resultLength <= 0) {
            return new Float64Array(0);
        }

        const yPtr = worldline.getValue(yPtrPtr, 'i32');
        const audio = new Float64Array(worldline.HEAPF64.buffer, yPtr, resultLength).slice();

        // Free the C++-allocated audio array
        worldline._free(yPtr);

        return audio;

    } finally {
        worldline._free(f0Ptr);
        worldline._free(mgcOrSpPtr);
        worldline._free(bapOrApPtr);
        worldline._free(genderPtr);
        worldline._free(tensionPtr);
        worldline._free(breathinessPtr);
        worldline._free(voicingPtr);
        worldline._free(yPtrPtr);
    }
}

/**
 * Create a new PhraseSynth instance
 * @returns {number} - Pointer to PhraseSynth object
 */
export function PhraseSynthNew() {
    if (!worldline) throw new Error("Worldline not initialized");
    return worldline._PhraseSynthNew();
}

/**
 * Delete a PhraseSynth instance
 * @param {number} phraseSynthPtr - Pointer to PhraseSynth object
 */
export function PhraseSynthDelete(phraseSynthPtr) {
    if (!worldline) throw new Error("Worldline not initialized");
    worldline._PhraseSynthDelete(phraseSynthPtr);
}

// Export initialization status checker
export function isInitialized() {
    return worldline !== null;
}
