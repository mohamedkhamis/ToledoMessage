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
    preventEnterNewline: function (element) {
        if (!element || element._enterHandlerSet) return;
        element._enterHandlerSet = true;
        element.addEventListener('keydown', function (e) {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
            }
        });
    },
    autoResize: function (element) {
        if (!element) return;
        element.style.height = 'auto';
        element.style.height = Math.min(element.scrollHeight, 150) + 'px';
    },
    resetHeight: function (element) {
        if (!element) return;
        element.style.height = '';
    },

    // Lightbox zoom/pan
    _lightbox: { scale: 1, translateX: 0, translateY: 0, wrapper: null, img: null, isDragging: false, startX: 0, startY: 0, pinchStartDist: 0, pinchStartScale: 1 },

    _lightboxApplyTransform: function () {
        var lb = this._lightbox;
        if (lb.img) {
            lb.img.style.transform = 'translate(' + lb.translateX + 'px, ' + lb.translateY + 'px) scale(' + lb.scale + ')';
        }
    },

    initLightboxZoom: function (wrapper) {
        var self = this;
        var lb = self._lightbox;
        lb.wrapper = wrapper;
        lb.img = wrapper ? wrapper.querySelector('img') : null;
        lb.scale = 1; lb.translateX = 0; lb.translateY = 0;
        if (!lb.img) return;
        lb.img.style.transform = '';
        lb.img.style.transition = 'transform 0.1s ease-out';

        // Wheel zoom
        lb._onWheel = function (e) {
            e.preventDefault();
            var delta = e.deltaY > 0 ? -0.15 : 0.15;
            lb.scale = Math.min(Math.max(lb.scale + delta, 0.5), 5);
            if (lb.scale <= 1) { lb.translateX = 0; lb.translateY = 0; }
            self._lightboxApplyTransform();
        };
        // ReSharper disable once Html.EventNotResolved
        wrapper.addEventListener('wheel', lb._onWheel, { passive: false });

        // Mouse drag for panning
        lb._onMouseDown = function (e) {
            if (lb.scale <= 1) return;
            lb.isDragging = true; lb.startX = e.clientX - lb.translateX; lb.startY = e.clientY - lb.translateY;
            lb.img.style.transition = 'none'; lb.img.style.cursor = 'grabbing';
        };
        lb._onMouseMove = function (e) {
            if (!lb.isDragging) return;
            lb.translateX = e.clientX - lb.startX; lb.translateY = e.clientY - lb.startY;
            self._lightboxApplyTransform();
        };
        lb._onMouseUp = function () {
            lb.isDragging = false;
            if (lb.img) { lb.img.style.transition = 'transform 0.1s ease-out'; lb.img.style.cursor = ''; }
        };
        wrapper.addEventListener('mousedown', lb._onMouseDown);
        document.addEventListener('mousemove', lb._onMouseMove);
        document.addEventListener('mouseup', lb._onMouseUp);

        // Touch pinch zoom
        lb._onTouchStart = function (e) {
            if (e.touches.length === 2) {
                e.preventDefault();
                var dx = e.touches[0].clientX - e.touches[1].clientX;
                var dy = e.touches[0].clientY - e.touches[1].clientY;
                lb.pinchStartDist = Math.hypot(dx, dy);
                lb.pinchStartScale = lb.scale;
            } else if (e.touches.length === 1 && lb.scale > 1) {
                lb.isDragging = true;
                lb.startX = e.touches[0].clientX - lb.translateX;
                lb.startY = e.touches[0].clientY - lb.translateY;
                lb.img.style.transition = 'none';
            }
        };
        lb._onTouchMove = function (e) {
            if (e.touches.length === 2) {
                e.preventDefault();
                var dx = e.touches[0].clientX - e.touches[1].clientX;
                var dy = e.touches[0].clientY - e.touches[1].clientY;
                var dist = Math.hypot(dx, dy);
                lb.scale = Math.min(Math.max(lb.pinchStartScale * (dist / lb.pinchStartDist), 0.5), 5);
                if (lb.scale <= 1) { lb.translateX = 0; lb.translateY = 0; }
                self._lightboxApplyTransform();
            } else if (e.touches.length === 1 && lb.isDragging) {
                lb.translateX = e.touches[0].clientX - lb.startX;
                lb.translateY = e.touches[0].clientY - lb.startY;
                self._lightboxApplyTransform();
            }
        };
        lb._onTouchEnd = function () {
            lb.isDragging = false;
            if (lb.img) lb.img.style.transition = 'transform 0.1s ease-out';
        };
        // ReSharper disable Html.EventNotResolved
        wrapper.addEventListener('touchstart', lb._onTouchStart, { passive: false });
        wrapper.addEventListener('touchmove', lb._onTouchMove, { passive: false });
        wrapper.addEventListener('touchend', lb._onTouchEnd);
        // ReSharper restore Html.EventNotResolved
    },

    resetLightboxZoom: function () {
        var lb = this._lightbox;
        lb.scale = 1; lb.translateX = 0; lb.translateY = 0;
        if (lb.img) lb.img.style.transform = '';
    },

    destroyLightboxZoom: function () {
        var lb = this._lightbox;
        if (lb.wrapper) {
            // ReSharper disable  Html.EventNotResolved
            if (lb._onWheel) lb.wrapper.removeEventListener('wheel', lb._onWheel);
            if (lb._onMouseDown) lb.wrapper.removeEventListener('mousedown', lb._onMouseDown);
            if (lb._onTouchStart) lb.wrapper.removeEventListener('touchstart', lb._onTouchStart);
            if (lb._onTouchMove) lb.wrapper.removeEventListener('touchmove', lb._onTouchMove);
            if (lb._onTouchEnd) lb.wrapper.removeEventListener('touchend', lb._onTouchEnd);
        }
        if (lb._onMouseMove) document.removeEventListener('mousemove', lb._onMouseMove);
        if (lb._onMouseUp) document.removeEventListener('mouseup', lb._onMouseUp);
        lb.wrapper = null; lb.img = null; lb.scale = 1; lb.translateX = 0; lb.translateY = 0;
    },

    // Clipboard copy
    copyToClipboard: async function (text) {
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch (e) {
            return false;
        }
    },

    // Insert text at cursor position in textarea
    insertTextAtCursor: function (textarea, text) {
        if (!textarea) return;
        var start = textarea.selectionStart;
        var end = textarea.selectionEnd;
        var value = textarea.value;
        textarea.value = value.substring(0, start) + text + value.substring(end);
        var newPos = start + text.length;
        textarea.selectionStart = newPos;
        textarea.selectionEnd = newPos;
        textarea.focus();
        textarea.dispatchEvent(new Event('input', { bubbles: true }));
    },

    // Scroll chat container to bottom (instant on load, smooth for user actions)
    scrollToBottom: function (selector, instant) {
        var el = document.querySelector(selector);
        if (!el) return;
        if (instant) {
            el.scrollTop = el.scrollHeight;
            // Retry after media may have loaded (images/videos change height)
            setTimeout(function () { el.scrollTop = el.scrollHeight; }, 100);
            setTimeout(function () { el.scrollTop = el.scrollHeight; }, 400);
        } else {
            el.scrollTo({ top: el.scrollHeight, behavior: 'smooth' });
        }
    },

    // Observe scroll position: notify Blazor when scrolled, debounced for read tracking
    observeScroll: function (selector, dotNetRef) {
        var el = document.querySelector(selector);
        if (!el) return;

        // Remove old listener if re-binding with a new dotNetRef (component was recreated)
        if (el._scrollHandler) {
            el.removeEventListener('scroll', el._scrollHandler);
        }

        var scrollTimeout = null;

        var notifyVisible = function () {
            var lastVisibleId = mediaHelpers.getLastVisibleMessageId(selector);
            if (lastVisibleId) {
                try { dotNetRef.invokeMethodAsync('OnVisibleMessageChanged', lastVisibleId); } catch (e) { /* disposed */ }
            }
        };

        el._scrollHandler = function () {
            var nearBottom = el.scrollHeight - el.scrollTop - el.clientHeight < 80;
            try { dotNetRef.invokeMethodAsync('OnScrollPositionChanged', nearBottom); } catch (e) { /* disposed */ }

            // Debounced: notify about visible messages for read tracking (after 300ms pause)
            clearTimeout(scrollTimeout);
            scrollTimeout = setTimeout(notifyVisible, 300);
        };
        el.addEventListener('scroll', el._scrollHandler);

        // Fire an initial check after a short delay (messages may be visible without scrolling)
        setTimeout(notifyVisible, 500);
    },

    // Get the data-msg-id of the last message bubble visible in the scroll container
    getLastVisibleMessageId: function (containerSelector) {
        var container = document.querySelector(containerSelector);
        if (!container) return null;
        var messages = container.querySelectorAll('[data-msg-id]');
        var containerRect = container.getBoundingClientRect();
        var lastVisibleId = null;
        for (var i = 0; i < messages.length; i++) {
            var rect = messages[i].getBoundingClientRect();
            // Message is visible if its top is within the container viewport
            if (rect.top < containerRect.bottom && rect.bottom > containerRect.top) {
                lastVisibleId = messages[i].getAttribute('data-msg-id');
            }
        }
        return lastVisibleId;
    },

    // Scroll to a specific element by data attribute
    scrollToElement: function (selector) {
        var el = document.querySelector(selector);
        if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center' });
    },

    // Get textarea value (avoids eval)
    getTextareaValue: function (selector) {
        var el = document.querySelector(selector);
        return el ? el.value : '';
    },

    // Fetch blob URL contents as byte array (for forwarding media)
    fetchBlobAsBytes: async function (blobUrl) {
        var response = await fetch(blobUrl);
        var buffer = await response.arrayBuffer();
        return new Uint8Array(buffer);
    },

    // Audio playback helpers (replaces eval() calls)
    _currentlyPlayingId: null,

    playAudio: function (messageId) {
        // Pause any currently playing audio first
        if (this._currentlyPlayingId && this._currentlyPlayingId !== messageId) {
            var prev = document.querySelector('[data-msg-id="' + this._currentlyPlayingId + '"] audio');
            if (prev) {
                prev.pause();
                prev.currentTime = 0;
                // Dispatch a custom event so Blazor can update the old bubble's state
                prev.dispatchEvent(new Event('ended'));
            }
        }
        var audio = document.querySelector('[data-msg-id="' + messageId + '"] audio');
        if (audio) {
            this._currentlyPlayingId = messageId;
            return audio.play();
        }
        return false;
    },
    pauseAudio: function (messageId) {
        var audio = document.querySelector('[data-msg-id="' + messageId + '"] audio');
        if (audio) audio.pause();
        if (this._currentlyPlayingId === messageId) this._currentlyPlayingId = null;
    },
    getAudioCurrentTime: function (messageId) {
        var audio = document.querySelector('[data-msg-id="' + messageId + '"] audio');
        return audio ? audio.currentTime : 0;
    },
    getAudioDuration: function (messageId) {
        var audio = document.querySelector('[data-msg-id="' + messageId + '"] audio');
        return (audio && isFinite(audio.duration)) ? audio.duration : 0;
    },

    // Analyze audio bytes and return waveform samples (array of ints, 0-28 range)
    // byteArray may be a Uint8Array (from JS) or a base64 string (from Blazor byte[] interop)
    analyzeAudioWaveform: async function (byteArray, mimeType, sampleCount) {
        sampleCount = sampleCount || 64;
        var data;
        if (typeof byteArray === 'string') {
            // Blazor sends byte[] as base64 string
            var binary = atob(byteArray);
            var bytes = new Uint8Array(binary.length);
            for (var b = 0; b < binary.length; b++) bytes[b] = binary.charCodeAt(b);
            data = bytes;
        } else {
            data = new Uint8Array(byteArray);
        }
        var blob = new Blob([data], { type: mimeType || 'audio/webm' });
        var arrayBuffer = await blob.arrayBuffer();
        var ctx = new (window.AudioContext || window.webkitAudioContext)();
        try {
            var audioBuffer = await ctx.decodeAudioData(arrayBuffer);
            var rawData = audioBuffer.getChannelData(0);
            var actualSamples = Math.min(sampleCount, rawData.length);
            var blockSize = Math.max(1, Math.floor(rawData.length / actualSamples));
            var samples = [];
            for (var i = 0; i < actualSamples; i++) {
                var start = i * blockSize;
                var count = Math.min(blockSize, rawData.length - start);
                var sum = 0;
                for (var j = 0; j < count; j++) {
                    sum += Math.abs(rawData[start + j]);
                }
                samples.push(count > 0 ? sum / count : 0);
            }
            var max = 0;
            for (var k = 0; k < samples.length; k++) {
                if (samples[k] > max) max = samples[k];
            }
            if (max === 0) max = 1;
            var result = [];
            for (var m = 0; m < samples.length; m++) {
                result.push(Math.max(2, Math.round((samples[m] / max) * 28)));
            }
            ctx.close();
            return result;
        } catch (e) {
            try { ctx.close(); } catch (ex) { /* ignored */ }
            return null;
        }
    },

    // Compress an image using canvas API
    compressImage: async function (byteArray, mimeType, maxDimension, quality) {
        return new Promise(function (resolve, reject) {
            try {
                var blob = new Blob([new Uint8Array(byteArray)], { type: mimeType });
                var img = new Image();
                img.onload = function () {
                    URL.revokeObjectURL(img.src);
                    var width = img.width;
                    var height = img.height;

                    // Calculate new dimensions while maintaining aspect ratio
                    if (width > height) {
                        if (width > maxDimension) {
                            height = Math.round((height * maxDimension) / width);
                            width = maxDimension;
                        }
                    } else {
                        if (height > maxDimension) {
                            width = Math.round((width * maxDimension) / height);
                            height = maxDimension;
                        }
                    }

                    var canvas = document.createElement('canvas');
                    canvas.width = width;
                    canvas.height = height;
                    var ctx = canvas.getContext('2d');
                    ctx.drawImage(img, 0, 0, width, height);

                    // Determine output MIME type (default to JPEG for compression)
                    var outputMime = mimeType === 'image/png' ? 'image/png' : 'image/jpeg';
                    canvas.toBlob(function (resultBlob) {
                        if (!resultBlob) {
                            reject(new Error('Failed to create compressed image'));
                            return;
                        }
                        resultBlob.arrayBuffer().then(function (buffer) {
                            resolve({
                                bytes: new Uint8Array(buffer),
                                width: width,
                                height: height
                            });
                        });
                    }, outputMime, quality || 0.8);
                };
                img.onerror = function () { reject(new Error('Failed to load image for compression')); };
                img.src = URL.createObjectURL(blob);
            } catch (e) { reject(e); }
        });
    },

    // Generate a small thumbnail from an image
    generateThumbnail: async function (byteArray, mimeType, maxDimension, quality) {
        return new Promise(function (resolve, reject) {
            try {
                var blob = new Blob([new Uint8Array(byteArray)], { type: mimeType });
                var img = new Image();
                img.onload = function () {
                    URL.revokeObjectURL(img.src);
                    var width = img.width;
                    var height = img.height;

                    // Calculate thumbnail dimensions (maintain aspect ratio, max maxDimension)
                    if (width > height) {
                        if (width > maxDimension) {
                            height = Math.round((height * maxDimension) / width);
                            width = maxDimension;
                        }
                    } else {
                        if (height > maxDimension) {
                            width = Math.round((width * maxDimension) / height);
                            height = maxDimension;
                        }
                    }

                    var canvas = document.createElement('canvas');
                    canvas.width = width;
                    canvas.height = height;
                    var ctx = canvas.getContext('2d');
                    ctx.drawImage(img, 0, 0, width, height);

                    // Always output as JPEG for thumbnails
                    canvas.toBlob(function (resultBlob) {
                        if (!resultBlob) {
                            reject(new Error('Failed to create thumbnail'));
                            return;
                        }
                        var reader = new FileReader();
                        reader.onloadend = function () {
                            // Remove "data:image/jpeg;base64," prefix
                            var base64 = reader.result;
                            var commaIndex = base64.indexOf(',');
                            if (commaIndex > -1) {
                                base64 = base64.substring(commaIndex + 1);
                            }
                            resolve(base64);
                        };
                        reader.onerror = function () { reject(new Error('Failed to read thumbnail')); };
                        reader.readAsDataURL(resultBlob);
                    }, 'image/jpeg', quality || 0.6);
                };
                img.onerror = function () { reject(new Error('Failed to load image for thumbnail')); };
                img.src = URL.createObjectURL(blob);
            } catch (e) { reject(e); }
        });
    },

    // Generate thumbnail from video (capture frame at 1 second, resize to maxDim)
    captureVideoFrame: async function (byteArray, mimeType, maxDimension, quality) {
        maxDimension = maxDimension || 320;
        quality = quality || 0.7;
        return new Promise(function (resolve, reject) {
            var blobUrl = null;
            var timeout = null;
            try {
                var blob = new Blob([new Uint8Array(byteArray)], { type: mimeType });
                blobUrl = URL.createObjectURL(blob);
                var video = document.createElement('video');
                video.preload = 'auto';
                video.muted = true;
                video.playsInline = true;

                // Timeout after 10s in case video never loads
                timeout = setTimeout(function () {
                    if (blobUrl) URL.revokeObjectURL(blobUrl);
                    reject(new Error('Video frame capture timed out'));
                }, 10000);

                video.onloadeddata = function () {
                    // Seek to 1 second or 10% into the video
                    var seekTime = Math.min(1, video.duration * 0.1);
                    if (isNaN(seekTime) || seekTime < 0) seekTime = 0;
                    video.currentTime = seekTime;
                };

                video.onseeked = function () {
                    clearTimeout(timeout);
                    var w = video.videoWidth;
                    var h = video.videoHeight;

                    // Resize to maxDimension maintaining aspect ratio
                    if (w > h) {
                        if (w > maxDimension) {
                            h = Math.round((h * maxDimension) / w);
                            w = maxDimension;
                        }
                    } else {
                        if (h > maxDimension) {
                            w = Math.round((w * maxDimension) / h);
                            h = maxDimension;
                        }
                    }

                    var canvas = document.createElement('canvas');
                    canvas.width = w;
                    canvas.height = h;
                    var ctx = canvas.getContext('2d');
                    ctx.drawImage(video, 0, 0, w, h);

                    // Clean up video blob URL
                    if (blobUrl) { URL.revokeObjectURL(blobUrl); blobUrl = null; }

                    canvas.toBlob(function (resultBlob) {
                        if (!resultBlob) {
                            reject(new Error('Failed to capture video frame'));
                            return;
                        }
                        var reader = new FileReader();
                        reader.onloadend = function () {
                            var base64 = reader.result;
                            var commaIndex = base64.indexOf(',');
                            if (commaIndex > -1) {
                                base64 = base64.substring(commaIndex + 1);
                            }
                            resolve(base64);
                        };
                        reader.onerror = function () { reject(new Error('Failed to read video frame')); };
                        reader.readAsDataURL(resultBlob);
                    }, 'image/jpeg', quality);
                };

                video.onerror = function () {
                    clearTimeout(timeout);
                    if (blobUrl) URL.revokeObjectURL(blobUrl);
                    reject(new Error('Failed to load video for frame capture'));
                };
                video.src = blobUrl;
                video.load();
            } catch (e) {
                clearTimeout(timeout);
                if (blobUrl) URL.revokeObjectURL(blobUrl);
                reject(e);
            }
        });
    },

    // Download a data URL (base64 or blob) as a file
    downloadDataUrl: function (dataUrl, fileName) {
        var a = document.createElement('a');
        a.href = dataUrl;
        a.download = fileName || 'download';
        a.style.display = 'none';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
    },

    // PDF thumbnail: lazy-load pdf.js, render page 1 to canvas, return { thumbnail, pageCount }
    _pdfJsLoaded: false,
    _pdfJsLoading: null,

    _loadPdfJs: function () {
        if (this._pdfJsLoaded) return Promise.resolve();
        if (this._pdfJsLoading) return this._pdfJsLoading;
        var self = this;
        this._pdfJsLoading = new Promise(function (resolve, reject) {
            var script = document.createElement('script');
            script.src = '/lib/pdfjs/pdf.min.js';
            script.onload = function () {
                if (window.pdfjsLib) {
                    window.pdfjsLib.GlobalWorkerOptions.workerSrc =
                        '/lib/pdfjs/pdf.worker.min.js';
                }
                self._pdfJsLoaded = true;
                resolve();
            };
            script.onerror = function () { reject(new Error('Failed to load pdf.js')); };
            document.head.appendChild(script);
        });
        return this._pdfJsLoading;
    },

    generatePdfThumbnail: async function (dataUrl, maxWidth) {
        maxWidth = maxWidth || 320;
        await this._loadPdfJs();
        if (!window.pdfjsLib) return null;

        try {
            // Convert blob/data URL to ArrayBuffer so pdf.js worker can access it
            // (blob URLs are origin-scoped and inaccessible from CDN-hosted workers)
            var pdfSource = dataUrl;
            if (typeof dataUrl === 'string' && (dataUrl.startsWith('blob:') || dataUrl.startsWith('data:'))) {
                var resp = await fetch(dataUrl);
                var arrayBuffer = await resp.arrayBuffer();
                pdfSource = { data: new Uint8Array(arrayBuffer) };
            }

            var pdf = await window.pdfjsLib.getDocument(pdfSource).promise;
            var page = await pdf.getPage(1);
            var unscaledViewport = page.getViewport({ scale: 1 });
            var scale = maxWidth / unscaledViewport.width;
            var viewport = page.getViewport({ scale: scale });

            var canvas = document.createElement('canvas');
            canvas.width = viewport.width;
            canvas.height = viewport.height;
            var ctx = canvas.getContext('2d');

            await page.render({ canvasContext: ctx, viewport: viewport }).promise;

            var thumbnail = canvas.toDataURL('image/jpeg', 0.85);
            var pageCount = pdf.numPages;
            pdf.destroy();

            return { thumbnail: thumbnail, pageCount: pageCount };
        } catch (e) {
            console.warn('PDF thumbnail generation failed:', e);
            return null;
        }
    }

};
