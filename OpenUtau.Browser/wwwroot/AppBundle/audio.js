
let audioCtx = null;
let nextStartTime = 0;

export function initAudio() {
    console.log("Initializing AudioContext...");
    console.time("AudioContextCreation");
    if (!audioCtx) {
        audioCtx = new (window.AudioContext || window.webkitAudioContext)();
        nextStartTime = audioCtx.currentTime;
    }
    if (audioCtx.state === 'suspended') {
        audioCtx.resume();
    }
    console.timeEnd("AudioContextCreation");
    console.log("AudioContext state:", audioCtx.state);
}

export function playAudio(data, sampleRate, channels) {
    if (!audioCtx) initAudio();
    if (audioCtx.state === 'suspended') audioCtx.resume();

    // data comes as Uint8Array from C#
    const samples = new Float32Array(data.buffer, data.byteOffset, data.byteLength / 4);

    if (samples.length === 0) {
        console.warn('[playAudio] Empty samples');
        return;
    }

    // Safety check for channels
    if (!channels || channels < 1) channels = 1;

    const buffer = audioCtx.createBuffer(channels, samples.length / channels, sampleRate);

    //De-interleave
    for (let c = 0; c < channels; c++) {
        const chData = buffer.getChannelData(c);
        for (let i = 0; i < chData.length; i++) {
            chData[i] = samples[i * channels + c];
        }
    }

    const source = audioCtx.createBufferSource();
    source.buffer = buffer;
    source.connect(audioCtx.destination);

    const now = audioCtx.currentTime;

    // Initialize nextStartTime on first call or if it's way behind
    if (nextStartTime === 0 || nextStartTime < now - 1.0) {
        nextStartTime = now + 0.05; // Small 50ms initial lookahead
        console.log(`[playAudio] Initializing nextStartTime to ${nextStartTime.toFixed(3)}s`);
    }

    // Schedule this chunk to start at nextStartTime
    const startTime = nextStartTime;
    source.start(startTime);

    // Update nextStartTime for next chunk
    nextStartTime = startTime + buffer.duration;
}
// Heartbeat control
let heartbeatInterval = null;
let currentBufferSize = 4096;
let currentSampleRate = 44100;

export function startHeartbeat(onTick, bufferSize, sampleRate) {
    if (heartbeatInterval) clearInterval(heartbeatInterval);

    currentBufferSize = bufferSize || 4096;
    currentSampleRate = sampleRate || 44100;

    // Calculate interval in milliseconds
    // Use 50% of buffer duration (faster requests for smoother playback)
    const intervalMs = Math.floor((currentBufferSize / currentSampleRate) * 1000 * 0.5);

    console.log(`Starting heartbeat with interval ${intervalMs}ms (buffer=${currentBufferSize}, rate=${currentSampleRate})`);

    heartbeatInterval = setInterval(() => {
        try {
            onTick();
        } catch (e) {
            console.error("Heartbeat error:", e);
            stopHeartbeat();
        }
    }, intervalMs);
}

export function stopHeartbeat() {
    if (heartbeatInterval) {
        console.log("Stopping heartbeat...");
        clearInterval(heartbeatInterval);
        heartbeatInterval = null;
        nextStartTime = 0; // Reset for next playback
    }
}
