window.voiceRecorder = {
    _mediaRecorder: null,
    _chunks: [],
    _dotNetRef: null,
    _stream: null,
    _blobUrl: null,
    _blob: null,
    _audioEl: null,
    _mimeType: 'audio/webm',

    start: async function (dotNetRef) {
        this._dotNetRef = dotNetRef;
        this._chunks = [];
        this._revokePreview();

        this._stream = await navigator.mediaDevices.getUserMedia({ audio: true });
        var mimeType = 'audio/webm;codecs=opus';
        if (typeof window.MediaRecorder.isTypeSupported === 'function') {
            if (!window.MediaRecorder.isTypeSupported(mimeType)) {
                if (window.MediaRecorder.isTypeSupported('audio/ogg;codecs=opus')) mimeType = 'audio/ogg;codecs=opus';
                else if (window.MediaRecorder.isTypeSupported('audio/mp4')) mimeType = 'audio/mp4';
                else mimeType = '';
            }
        }
        // Store the actual mime type used for recording
        this._mimeType = mimeType || 'audio/webm';
        this._mediaRecorder = mimeType
            ? new window.MediaRecorder(this._stream, { mimeType: mimeType })
            : new window.MediaRecorder(this._stream);

        this._mediaRecorder.ondataavailable = (e) => {
            if (e.data.size > 0) {
                this._chunks.push(e.data);
            }
        };

        this._mediaRecorder.onstop = () => {
            // Stop mic tracks immediately
            if (this._stream) {
                this._stream.getTracks().forEach(track => track.stop());
                this._stream = null;
            }

            if (this._chunks.length === 0) {
                this._cleanup();
                return;
            }

            // Create blob using the actual recording mime type
            var blobType = this._mimeType.split(';')[0]; // strip codecs param
            this._blob = new Blob(this._chunks, { type: blobType });
            this._blobUrl = URL.createObjectURL(this._blob);

            if (this._dotNetRef) {
                try {
                    this._dotNetRef.invokeMethodAsync('OnRecordingStopped');
                } catch (e) {
                    // Component may have been disposed
                }
            }
        };

        // Use timeslice to get periodic data chunks (every 1s)
        this._mediaRecorder.start(1000);
    },

    stop: function () {
        if (this._mediaRecorder && this._mediaRecorder.state === 'recording') {
            this._mediaRecorder.stop();
        }
    },

    cancel: function () {
        this._chunks = [];
        if (this._mediaRecorder && this._mediaRecorder.state === 'recording') {
            this._mediaRecorder.stop();
        } else {
            this._cleanup();
        }
    },

    getPreviewUrl: function () {
        return this._blobUrl;
    },

    getRecordedBytes: async function () {
        if (!this._blob) return null;
        const arrayBuffer = await this._blob.arrayBuffer();
        return new Uint8Array(arrayBuffer);
    },

    getRecordedMimeType: function () {
        return this._mimeType ? this._mimeType.split(';')[0] : 'audio/webm';
    },

    initPreviewAudio: function (audioElement) {
        this._audioEl = audioElement;
        if (this._blobUrl && audioElement) {
            audioElement.src = this._blobUrl;
        }
    },

    playPreview: function () {
        if (this._audioEl) {
            this._audioEl.play();
        }
    },

    pausePreview: function () {
        if (this._audioEl) {
            this._audioEl.pause();
        }
    },

    seekPreview: function (time) {
        if (this._audioEl) {
            this._audioEl.currentTime = time;
        }
    },

    getPreviewCurrentTime: function () {
        return this._audioEl ? this._audioEl.currentTime : 0;
    },

    getPreviewDuration: function () {
        if (!this._audioEl) return 0;
        var d = this._audioEl.duration;
        return isFinite(d) ? d : 0;
    },

    _revokePreview: function () {
        if (this._audioEl) {
            this._audioEl.pause();
            this._audioEl.src = '';
            this._audioEl = null;
        }
        if (this._blobUrl) {
            URL.revokeObjectURL(this._blobUrl);
            this._blobUrl = null;
        }
        this._blob = null;
    },

    revokePreview: function () {
        this._revokePreview();
    },

    _cleanup: function () {
        this._revokePreview();
        if (this._stream) {
            this._stream.getTracks().forEach(track => track.stop());
            this._stream = null;
        }
        this._mediaRecorder = null;
        this._chunks = [];
    }
};
