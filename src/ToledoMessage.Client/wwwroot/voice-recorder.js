window.voiceRecorder = {
    _mediaRecorder: null,
    _chunks: [],
    _dotNetRef: null,
    _stream: null,

    start: async function (dotNetRef) {
        this._dotNetRef = dotNetRef;
        this._chunks = [];

        this._stream = await navigator.mediaDevices.getUserMedia({ audio: true });
        var mimeType = 'audio/webm;codecs=opus';
        if (typeof MediaRecorder.isTypeSupported === 'function') {
            if (!MediaRecorder.isTypeSupported(mimeType)) {
                if (MediaRecorder.isTypeSupported('audio/ogg;codecs=opus')) mimeType = 'audio/ogg;codecs=opus';
                else if (MediaRecorder.isTypeSupported('audio/mp4')) mimeType = 'audio/mp4';
                else mimeType = '';
            }
        }
        this._mediaRecorder = mimeType
            ? new window.MediaRecorder(this._stream, { mimeType: mimeType })
            : new window.MediaRecorder(this._stream);

        this._mediaRecorder.ondataavailable = (e) => {
            if (e.data.size > 0) {
                this._chunks.push(e.data);
            }
        };

        this._mediaRecorder.onstop = async () => {
            if (this._chunks.length === 0 || !this._dotNetRef) {
                this._cleanup();
                return;
            }

            const blob = new Blob(this._chunks, { type: 'audio/webm' });
            const arrayBuffer = await blob.arrayBuffer();
            const byteArray = new Uint8Array(arrayBuffer);

            try {
                await this._dotNetRef.invokeMethodAsync('OnRecordingComplete', Array.from(byteArray));
            } catch (e) {
                // Component may have been disposed
            }

            this._cleanup();
        };

        this._mediaRecorder.start();
    },

    stop: function () {
        if (this._mediaRecorder && this._mediaRecorder.state === 'recording') {
            this._mediaRecorder.stop();
        }
    },

    cancel: function () {
        this._chunks = [];
        // Don't nullify _dotNetRef here — let onstop/cleanup handle it
        // so the onstop handler sees empty chunks and skips the callback
        if (this._mediaRecorder && this._mediaRecorder.state === 'recording') {
            this._mediaRecorder.stop();
        } else {
            this._cleanup();
        }
    },

    _cleanup: function () {
        if (this._stream) {
            this._stream.getTracks().forEach(track => track.stop());
            this._stream = null;
        }
        this._mediaRecorder = null;
    }
};
