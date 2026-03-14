window.voiceRecorder = {
    _mediaRecorder: null,
    _chunks: [],
    _dotNetRef: null,
    _stream: null,
    _blobUrl: null,
    _blob: null,
    _audioEl: null,
    _mimeType: 'audio/webm',
    _maxDurationMs: 5 * 60 * 1000, // 5 minutes
    _recordingStartTime: null,
    _durationTimer: null,

    // Web Audio API for real waveform
    _audioContext: null,
    _analyser: null,
    _sourceNode: null,
    _amplitudeTimer: null,
    _liveAmplitudes: [],    // Real-time amplitude samples during recording
    _waveformSamples: null, // Analyzed waveform after recording (for preview & transmission)

    start: async function (dotNetRef) {
        this._dotNetRef = dotNetRef;
        this._chunks = [];
        this._liveAmplitudes = [];
        this._waveformSamples = null;
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
            // Clear duration timer
            if (this._durationTimer) {
                clearInterval(this._durationTimer);
                this._durationTimer = null;
            }
            // Stop amplitude capture
            this._stopAmplitudeCapture();

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

            // Analyze recorded audio to generate waveform samples
            var self = this;
            this._analyzeBlob(this._blob).then(function (samples) {
                self._waveformSamples = samples;
                if (self._dotNetRef) {
                    try {
                        self._dotNetRef.invokeMethodAsync('OnRecordingStopped');
                    } catch (e) {
                        // Component may have been disposed
                    }
                }
            }).catch(function () {
                // Fallback: use live amplitudes downsampled
                self._waveformSamples = self._downsampleAmplitudes(self._liveAmplitudes, 64);
                if (self._dotNetRef) {
                    try {
                        self._dotNetRef.invokeMethodAsync('OnRecordingStopped');
                    } catch (e) {
                        // Component may have been disposed
                    }
                }
            });
        };

        // Set up Web Audio API for real-time amplitude capture
        this._setupAnalyser(this._stream);

        // FR-028: Start duration timer for 5-minute max limit
        this._recordingStartTime = Date.now();
        this._durationTimer = setInterval(() => {
            if (Date.now() - this._recordingStartTime >= this._maxDurationMs) {
                this.stop();
            }
        }, 1000);

        // Use times lice to get periodic data chunks (every 1s)
        this._mediaRecorder.start(1000);
    },

    // Set up AnalyserNode for real-time amplitude visualization
    _setupAnalyser: function (stream) {
        try {
            this._audioContext = new (window.AudioContext || window.webkitAudioContext)();
            this._analyser = this._audioContext.createAnalyser();
            this._analyser.fftSize = 256;
            this._sourceNode = this._audioContext.createMediaStreamSource(stream);
            this._sourceNode.connect(this._analyser);

            var dataArray = new Uint8Array(this._analyser.frequencyBinCount);
            var self = this;

            // Capture amplitude ~20 times per second
            this._amplitudeTimer = setInterval(function () {
                if (!self._analyser) return;
                self._analyser.getByteFrequencyData(dataArray);
                // Compute RMS amplitude from frequency data
                var sum = 0;
                for (var i = 0; i < dataArray.length; i++) {
                    sum += dataArray[i];
                }
                var avg = sum / dataArray.length;
                // Normalize to 0-28 range (for bar height in px)
                var normalized = Math.round((avg / 255) * 28);
                self._liveAmplitudes.push(Math.max(2, normalized));
            }, 50);
        } catch (e) {
            // Web Audio API not available, fall back to fake
        }
    },

    _stopAmplitudeCapture: function () {
        if (this._amplitudeTimer) {
            clearInterval(this._amplitudeTimer);
            this._amplitudeTimer = null;
        }
        if (this._sourceNode) {
            try { this._sourceNode.disconnect(); } catch (e) { /* ignored */ }
            this._sourceNode = null;
        }
        if (this._audioContext) {
            try { this._audioContext.close(); } catch (e) { /* ignored */ }
            this._audioContext = null;
        }
        this._analyser = null;
    },

    // Analyze a recorded blob to extract waveform samples using decodeAudioData
    _analyzeBlobCtx: null, // Track AudioContext for cleanup on cancel
    _analyzeBlob: function (blob) {
        var self = this;
        return new Promise(function (resolve, reject) {
            var reader = new FileReader();
            reader.onload = function () {
                var ctx = new (window.AudioContext || window.webkitAudioContext)();
                self._analyzeBlobCtx = ctx;
                ctx.decodeAudioData(reader.result).then(function (audioBuffer) {
                    var samples = voiceRecorder._extractWaveform(audioBuffer, 64);
                    try { ctx.close(); } catch (e) { /* ignored */ }
                    self._analyzeBlobCtx = null;
                    resolve(samples);
                }).catch(function (err) {
                    try { ctx.close(); } catch (e) { /* ignored */ }
                    self._analyzeBlobCtx = null;
                    reject(err);
                });
            };
            reader.onerror = function () { reject(new Error('Failed to read blob')); };
            reader.readAsArrayBuffer(blob);
        });
    },

    // Extract normalized waveform from AudioBuffer (0-28 range for bar heights)
    _extractWaveform: function (audioBuffer, sampleCount) {
        var rawData = audioBuffer.getChannelData(0); // Use first channel
        var actualSamples = Math.min(sampleCount, rawData.length);
        var blockSize = Math.max(1, Math.floor(rawData.length / actualSamples));
        var samples = [];
        for (var i = 0; i < actualSamples; i++) {
            var start = i * blockSize;
            var sum = 0;
            var count = Math.min(blockSize, rawData.length - start);
            for (var j = 0; j < count; j++) {
                sum += Math.abs(rawData[start + j]);
            }
            samples.push(count > 0 ? sum / count : 0);
        }

        // Normalize to 0-28 range
        var max = 0;
        for (var k = 0; k < samples.length; k++) {
            if (samples[k] > max) max = samples[k];
        }
        if (max === 0) max = 1;

        var result = [];
        for (var m = 0; m < samples.length; m++) {
            result.push(Math.max(2, Math.round((samples[m] / max) * 28)));
        }
        return result;
    },

    // Downsample live amplitude array to target count
    _downsampleAmplitudes: function (amplitudes, targetCount) {
        if (!amplitudes || amplitudes.length === 0) {
            var fallback = [];
            for (var f = 0; f < targetCount; f++) fallback.push(2);
            return fallback;
        }
        if (amplitudes.length <= targetCount) {
            // Pad with 2s if too short
            var padded = amplitudes.slice();
            while (padded.length < targetCount) padded.push(2);
            return padded;
        }
        var blockSize = amplitudes.length / targetCount;
        var result = [];
        for (var i = 0; i < targetCount; i++) {
            var start = Math.floor(i * blockSize);
            var end = Math.floor((i + 1) * blockSize);
            var sum = 0;
            for (var j = start; j < end; j++) {
                sum += amplitudes[j];
            }
            result.push(Math.max(2, Math.round(sum / (end - start))));
        }
        return result;
    },

    // Get current live amplitude (called from Blazor timer for recording visualization)
    getLiveAmplitude: function () {
        if (!this._liveAmplitudes || this._liveAmplitudes.length === 0) return 2;
        return this._liveAmplitudes[this._liveAmplitudes.length - 1];
    },

    // Get the 64-sample waveform for the recorded audio (for preview and transmission)
    getWaveformSamples: function () {
        return this._waveformSamples || [];
    },

    stop: function () {
        // Clear duration timer
        if (this._durationTimer) {
            clearInterval(this._durationTimer);
            this._durationTimer = null;
        }
        if (this._mediaRecorder && this._mediaRecorder.state === 'recording') {
            this._mediaRecorder.stop();
        }
    },

    cancel: function () {
        // Clear duration timer
        if (this._durationTimer) {
            clearInterval(this._durationTimer);
            this._durationTimer = null;
        }
        this._stopAmplitudeCapture();
        this._chunks = [];
        if (this._mediaRecorder && this._mediaRecorder.state === 'recording') {
            this._mediaRecorder.stop();
        } else {
            this._cleanup();
        }
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
        this._stopAmplitudeCapture();
        if (this._analyzeBlobCtx) {
            try { this._analyzeBlobCtx.close(); } catch (e) { /* ignored */ }
            this._analyzeBlobCtx = null;
        }
        if (this._stream) {
            this._stream.getTracks().forEach(track => track.stop());
            this._stream = null;
        }
        this._mediaRecorder = null;
        this._chunks = [];
        this._liveAmplitudes = [];
        this._waveformSamples = null;
    }
};
