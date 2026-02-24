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
    console.log("[WorldAnalysisF0InViaGlobal] Called");

    try {
        // Parse JSON inputs
        const inputJson = globalThis._worldlineInput;
        if (!inputJson) {
            throw new Error("Input JSON not found");
        }

        const input = JSON.parse(inputJson);
        const samplesArray = input.samples;
        const f0InArray = input.f0In;

        console.log(`[WorldAnalysisF0InViaGlobal] samples: ${samplesArray.length}, f0: ${f0InArray.length}`);

        // Convert to typed arrays
        const samples = new Float32Array(samplesArray);
        const f0In = new Float64Array(f0InArray);

        const numSamples = samples.length;
        const numFrames = f0In.length;
        const spSize = Math.floor(fftSize / 2) + 1;

        // Allocate memory
        const samplesPtr = worldline._malloc(numSamples * 4);
        const f0InPtr = worldline._malloc(numFrames * 8);
        const spOutPtr = worldline._malloc(numFrames * spSize * 8);
        const apOutPtr = worldline._malloc(numFrames * spSize * 8);
        const configPtr = worldline._malloc(32);

        try {
            // Copy input data to WASM heap
            worldline.HEAPF32.set(samples, samplesPtr / 4);
            worldline.HEAPF64.set(f0In, f0InPtr / 8);

            // Initialize config
            worldline.setValue(configPtr + 0, fs, 'i32');
            worldline.setValue(configPtr + 4, hopSize, 'i32');
            worldline.setValue(configPtr + 8, fftSize, 'i32');
            worldline.setValue(configPtr + 12, f0Floor, 'float');
            worldline.setValue(configPtr + 16, frameMs, 'double');

            // Call WorldAnalysisF0In
            console.log("[WorldAnalysisF0InViaGlobal] Calling _WorldAnalysisF0In...");
            worldline._WorldAnalysisF0In(
                configPtr,
                samplesPtr,
                numSamples,
                f0InPtr,
                numFrames,
                spOutPtr,
                apOutPtr
            );
            console.log("[WorldAnalysisF0InViaGlobal] _WorldAnalysisF0In completed");

            // Read output arrays
            const spOut = new Float64Array(
                worldline.HEAPF64.buffer,
                spOutPtr,
                numFrames * spSize
            ).slice();

            const apOut = new Float64Array(
                worldline.HEAPF64.buffer,
                apOutPtr,
                numFrames * spSize
            ).slice();

            // Convert to regular arrays and store in global
            globalThis._worldlineOutput = JSON.stringify({
                spEnv: Array.from(spOut),
                ap: Array.from(apOut),
                numFrames: numFrames,
                spSize: spSize
            });

            console.log("[WorldAnalysisF0InViaGlobal] ✅ Complete");
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
