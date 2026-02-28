// Creates an efficient blob URL from a byte array, avoiding massive data: URI strings in the DOM.
window.mediaHelpers = {
    createObjectUrl: function (byteArray, mimeType) {
        const blob = new Blob([new Uint8Array(byteArray)], { type: mimeType });
        return URL.createObjectURL(blob);
    },
    revokeObjectUrl: function (url) {
        if (url && url.startsWith('blob:')) {
            URL.revokeObjectURL(url);
        }
    },
    autoResize: function (element) {
        if (!element) return;
        element.style.height = 'auto';
        element.style.height = Math.min(element.scrollHeight, 150) + 'px';
    },
    resetHeight: function (element) {
        if (!element) return;
        element.style.height = '';
    }
};
