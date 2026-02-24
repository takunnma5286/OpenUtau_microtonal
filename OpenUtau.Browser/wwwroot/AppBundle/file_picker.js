let selectedFiles = [];
let loadedBuffer = null;

export function openFile(accept, multiple) {
    return new Promise((resolve, reject) => {
        const input = document.createElement('input');
        input.type = 'file';

        if (accept) {
            input.accept = accept + ",*/*";
        }

        input.multiple = multiple;

        const cleanup = () => {
            input.remove();
        };

        input.onchange = (e) => {
            selectedFiles = Array.from(e.target.files);
            resolve(selectedFiles.length);
            cleanup();
        };

        input.oncancel = () => {
            selectedFiles = [];
            resolve(0);
            cleanup();
        };

        input.click();
    });
}

export function getFileName(index) {
    if (index >= 0 && index < selectedFiles.length) {
        return selectedFiles[index].name;
    }
    return null;
}

// Load file content into temporary JS memory
export async function loadFile(index) {
    if (index >= 0 && index < selectedFiles.length) {
        const file = selectedFiles[index];
        const arrayBuffer = await file.arrayBuffer();
        loadedBuffer = new Uint8Array(arrayBuffer);
        return loadedBuffer.length;
    }
    return 0;
}

// Synchronously retrieve the loaded buffer
export function getLoadedFileData() {
    return loadedBuffer;
}

// Free the loaded buffer
export function freeLoadedFileData() {
    loadedBuffer = null;
}
