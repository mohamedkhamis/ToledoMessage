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
    }
};
