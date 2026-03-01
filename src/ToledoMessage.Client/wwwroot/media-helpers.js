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

    // Long press registration
    registerLongPress: function (element, dotNetRef, methodName, delay) {
        if (!element) return;
        var timer = null;
        var triggered = false;
        element.addEventListener('pointerdown', function (e) {
            triggered = false;
            timer = setTimeout(function () {
                triggered = true;
                dotNetRef.invokeMethodAsync(methodName, e.clientX, e.clientY);
            }, delay || 500);
        });
        element.addEventListener('pointerup', function () { clearTimeout(timer); });
        element.addEventListener('pointercancel', function () { clearTimeout(timer); });
        element.addEventListener('pointermove', function (e) {
            if (timer && (Math.abs(e.movementX) > 5 || Math.abs(e.movementY) > 5)) clearTimeout(timer);
        });
        element.addEventListener('contextmenu', function (e) {
            if (triggered) e.preventDefault();
        });
    }
};
