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
