window.toledoStorage = {
    // FR-008: Cookie management without eval()
    setCookie: function (name, value, days, path = '/', sameSite = 'Lax') {
        var expires = '';
        if (days) {
            var date = new Date();
            date.setTime(date.getTime() + (days * 24 * 60 * 60 * 1000));
            expires = '; expires=' + date.toUTCString();
        }
        document.cookie = name + '=' + (value || '') + expires + '; path=' + path + '; sameSite=' + sameSite + (location.protocol === 'https:' ? '; secure' : '');
    },
    setItem: function (key, value) {
        localStorage.setItem(key, value);
    },
    getItem: function (key) {
        return localStorage.getItem(key);
    },
    removeItem: function (key) {
        localStorage.removeItem(key);
    },
    containsKey: function (key) {
        return localStorage.getItem(key) !== null;
    },
    setTheme: function (name) {
        if (name) {
            localStorage.setItem('app.theme', name);
            if (name === 'default') {
                document.documentElement.removeAttribute('data-theme');
            } else {
                document.documentElement.setAttribute('data-theme', name);
            }
        } else {
            localStorage.removeItem('app.theme');
            document.documentElement.removeAttribute('data-theme');
        }
    },
    getTheme: function () {
        return localStorage.getItem('app.theme');
    },
    setFontSize: function (size) {
        // Accept numeric px value (12–20) or legacy string ('small'/'medium'/'large')
        var px = parseInt(size, 10);
        if (isNaN(px)) {
            // Legacy migration: convert old string values to px
            px = size === 'small' ? 13 : size === 'large' ? 17 : 15;
        }
        localStorage.setItem('app.fontSize', String(px));
        document.documentElement.style.setProperty('--app-font-size', px + 'px');
    },
    getFontSize: function () {
        var stored = localStorage.getItem('app.fontSize');
        if (!stored) return '15';
        // Legacy migration
        if (stored === 'small') return '13';
        if (stored === 'medium') return '15';
        if (stored === 'large') return '17';
        return stored;
    },
    setWallpaper: function (id) {
        if (id && id !== 'default') {
            localStorage.setItem('app.wallpaper', id);
        } else {
            localStorage.removeItem('app.wallpaper');
        }
    },
    getWallpaper: function () {
        return localStorage.getItem('app.wallpaper') || 'default';
    },
    clearAuthData: function () {
        // Only clear auth tokens — preserve device identity, crypto keys, sessions,
        // and preferences so the same device is reused on re-login and old messages
        // remain decryptable (Signal Protocol sessions are forward-secret).
        localStorage.removeItem('auth.token');
        localStorage.removeItem('auth.refreshToken');
    },
    clearAllData: function () {
        // Full wipe for account deletion — clear everything except UI preferences
        var preserve = ['app.theme', 'app.fontSize', 'app.wallpaper'];
        var saved = {};
        for (var i = 0; i < preserve.length; i++) {
            var v = localStorage.getItem(preserve[i]);
            if (v !== null) saved[preserve[i]] = v;
        }
        localStorage.clear();
        for (var key in saved) {
            if (Object.prototype.hasOwnProperty.call(saved, key)) {
                localStorage.setItem(key, saved[key]);
            }
        }
    }
};

// ─── IndexedDB Message Store ───
window.toledoMessageStore = {
    _db: null,
    _dbName: 'ToledoMessages',
    _version: 2, // Bumped for offlineQueue store

    open: async function () {
        if (this._db) return this._db;
        return new Promise((resolve, reject) => {
            const request = indexedDB.open(this._dbName, this._version);
            request.onupgradeneeded = function (e) {
                const db = e.target.result;
                if (!db.objectStoreNames.contains('messages')) {
                    const store = db.createObjectStore('messages', { keyPath: 'messageId' });
                    store.createIndex('conversationId', 'conversationId', { unique: false });
                    store.createIndex('conversationTimestamp', ['conversationId', 'timestamp'], { unique: false });
                }
                if (!db.objectStoreNames.contains('meta')) {
                    db.createObjectStore('meta', { keyPath: 'key' });
                }
                // FR-032: Offline queue for messages sent while disconnected
                if (!db.objectStoreNames.contains('offlineQueue')) {
                    const queueStore = db.createObjectStore('offlineQueue', { keyPath: 'id', autoIncrement: true });
                    queueStore.createIndex('conversationId', 'conversationId', { unique: false });
                    queueStore.createIndex('status', 'status', { unique: false });
                    queueStore.createIndex('createdAt', 'createdAt', { unique: false });
                }
            };
            request.onsuccess = (e) => { this._db = e.target.result; resolve(this._db); };
            request.onerror = (e) => reject(e.target.error);
        });
    },

    storeMessage: async function (msg) {
        const db = await this.open();
        return new Promise((resolve, reject) => {
            const tx = db.transaction('messages', 'readwrite');
            tx.objectStore('messages').put(msg);
            tx.oncomplete = () => resolve();
            tx.onerror = (e) => reject(e.target.error);
        });
    },
    storeMessages: async function (msgs) {
// ReSharper disable once InconsistentFunctionReturns
        if (!msgs || msgs.length === 0) return;
        const db = await this.open();
        return new Promise((resolve, reject) => {
            const tx = db.transaction('messages', 'readwrite');
            const store = tx.objectStore('messages');
            for (const msg of msgs) {
                store.put(msg);
            }
            tx.oncomplete = () => resolve();
            tx.onerror = (e) => reject(e.target.error);
        });
    },

    getMessages: async function (conversationId, limit, beforeTimestamp) {
        const db = await this.open();
        return new Promise((resolve, reject) => {
            const tx = db.transaction('messages', 'readonly');
            const store = tx.objectStore('messages');
            const index = store.index('conversationId');
            const request = index.getAll(conversationId);
            request.onsuccess = function () {
                let results = request.result || [];
                if (beforeTimestamp) {
                    results = results.filter(m => m.timestamp < beforeTimestamp);
                }
                results.sort((a, b) => a.timestamp.localeCompare(b.timestamp));
                if (limit && limit > 0) {
                    results = results.slice(-limit);
                }
                resolve(results);
            };
            request.onerror = (e) => reject(e.target.error);
        });
    },

    getMessageCount: async function (conversationId) {
        const db = await this.open();
        return new Promise((resolve, reject) => {
            const tx = db.transaction('messages', 'readonly');
            const index = tx.objectStore('messages').index('conversationId');
            const countReq = index.count(conversationId);
            countReq.onsuccess = () => resolve(countReq.result);
            countReq.onerror = (e) => reject(e.target.error);
        });
    },

    getMessagesPaged: async function (conversationId, offset, count) {
        const db = await this.open();
        return new Promise((resolve, reject) => {
            const tx = db.transaction('messages', 'readonly');
            const store = tx.objectStore('messages');
            const index = store.index('conversationTimestamp');
            const range = IDBKeyRange.bound([conversationId], [conversationId, []]);
            const results = [];
            let skipped = 0;
            const request = index.openCursor(range, 'next');
            request.onsuccess = function (e) {
                const cursor = e.target.result;
                if (!cursor) { resolve(results); return; }
                if (skipped < offset) {
                    skipped++;
                    cursor.continue();
                    return;
                }
                if (results.length < count) {
                    results.push(cursor.value);
                    cursor.continue();
                } else {
                    resolve(results);
                }
            };
            request.onerror = (e) => reject(e.target.error);
        });
    },

    getLastMessageTimestamp: async function (conversationId) {
        const db = await this.open();
        return new Promise((resolve, reject) => {
            const tx = db.transaction('messages', 'readonly');
            const index = tx.objectStore('messages').index('conversationId');
            const request = index.getAll(conversationId);
            request.onsuccess = function () {
                const results = request.result || [];
                if (results.length === 0) { resolve(null); return; }
                results.sort((a, b) => b.timestamp.localeCompare(a.timestamp));
                resolve(results[0].timestamp);
            };
            request.onerror = (e) => reject(e.target.error);
        });
    },

    deleteConversationMessages: async function (conversationId, fromTimestamp) {
        const db = await this.open();
        return new Promise((resolve, reject) => {
            const tx = db.transaction('messages', 'readwrite');
            const store = tx.objectStore('messages');
            const index = store.index('conversationId');
            const request = index.openCursor(conversationId);
            request.onsuccess = function (e) {
                const cursor = e.target.result;
                if (cursor) {
                    // If fromTimestamp provided, only delete messages with timestamp >= fromTimestamp
                    // If not provided, delete all
                    if (!fromTimestamp || cursor.value.timestamp >= fromTimestamp) {
                        cursor.delete();
                    }
                    cursor.continue();
                }
            };
            tx.oncomplete = () => resolve();
            tx.onerror = (e) => reject(e.target.error);
        });
    },

    deleteMessage: async function (messageId) {
        const db = await this.open();
        return new Promise((resolve, reject) => {
            const tx = db.transaction('messages', 'readwrite');
            const store = tx.objectStore('messages');
            store.delete(messageId);
            tx.oncomplete = () => resolve();
            tx.onerror = (e) => reject(e.target.error);
        });
    },

    updateMessageStatus: async function (messageId, status) {
        const db = await this.open();
        return new Promise((resolve, reject) => {
            const tx = db.transaction('messages', 'readwrite');
            const store = tx.objectStore('messages');
            const getReq = store.get(messageId);
            getReq.onsuccess = function () {
                if (getReq.result) {
                    getReq.result.status = status;
                    store.put(getReq.result);
                }
            };
            tx.oncomplete = () => resolve();
            tx.onerror = (e) => reject(e.target.error);
        });
    },

    setMeta: async function (key, value) {
        const db = await this.open();
        return new Promise((resolve, reject) => {
            const tx = db.transaction('meta', 'readwrite');
            tx.objectStore('meta').put({ key: key, value: value });
            tx.oncomplete = () => resolve();
            tx.onerror = (e) => reject(e.target.error);
        });
    },

    getMeta: async function (key) {
        const db = await this.open();
        return new Promise((resolve, reject) => {
            const tx = db.transaction('meta', 'readonly');
            const req = tx.objectStore('meta').get(key);
            req.onsuccess = () => resolve(req.result ? req.result.value : null);
            req.onerror = (e) => reject(e.target.error);
        });
    },

    clearAll: async function () {
        const db = await this.open();
        return new Promise((resolve, reject) => {
            const tx = db.transaction(['messages', 'meta', 'offlineQueue'], 'readwrite');
            tx.objectStore('messages').clear();
            tx.objectStore('meta').clear();
            tx.objectStore('offlineQueue').clear();
            tx.oncomplete = () => resolve();
            tx.onerror = (e) => reject(e.target.error);
        });
    },

    // FR-032: Offline queue functions
    _maxOfflineQueueSize: 50,

    getOfflineQueueCount: async function () {
        const db = await this.open();
        return new Promise((resolve, reject) => {
            const tx = db.transaction('offlineQueue', 'readonly');
            const store = tx.objectStore('offlineQueue');
            const countReq = store.count();
            countReq.onsuccess = () => resolve(countReq.result);
            countReq.onerror = (e) => reject(e.target.error);
        });
    },

    addToOfflineQueue: async function (entry) {
        // FR-032: Check queue capacity (max 50 messages)
        const count = await this.getOfflineQueueCount();
        if (count >= this._maxOfflineQueueSize) {
            throw new Error('OFFLINE_QUEUE_FULL');
        }
        const db = await this.open();
        return new Promise((resolve, reject) => {
            const tx = db.transaction('offlineQueue', 'readwrite');
            const store = tx.objectStore('offlineQueue');
            entry.createdAt = entry.createdAt || new Date().toISOString();
            entry.status = entry.status || 'pending';
            entry.retryCount = entry.retryCount || 0;
            const req = store.add(entry);
            req.onsuccess = () => resolve(req.result); // returns the auto-generated id
            req.onerror = (e) => reject(e.target.error);
        });
    },

    getOfflineQueue: async function (conversationId, status) {
        const db = await this.open();
        return new Promise((resolve, reject) => {
            const tx = db.transaction('offlineQueue', 'readonly');
            const store = tx.objectStore('offlineQueue');
            let results = [];
            let request;
            if (conversationId) {
                const index = store.index('conversationId');
                request = index.getAll(conversationId);
            } else {
                request = store.getAll();
            }
            request.onsuccess = () => {
                results = request.result || [];
                if (status) {
                    results = results.filter(r => r.status === status);
                }
                // Sort by createdAt
                results.sort((a, b) => new Date(a.createdAt) - new Date(b.createdAt));
                resolve(results);
            };
            request.onerror = (e) => reject(e.target.error);
        });
    },

    updateOfflineQueueStatus: async function (id, status) {
        const db = await this.open();
        return new Promise((resolve, reject) => {
            const tx = db.transaction('offlineQueue', 'readwrite');
            const store = tx.objectStore('offlineQueue');
            const getReq = store.get(id);
            getReq.onsuccess = () => {
                const entry = getReq.result;
                if (entry) {
                    entry.status = status;
                    if (status === 'pending') entry.retryCount = (entry.retryCount || 0) + 1;
                    store.put(entry);
                }
                tx.oncomplete = () => resolve();
                tx.onerror = (e) => reject(e.target.error);
            };
            getReq.onerror = (e) => reject(e.target.error);
        });
    },

    removeFromOfflineQueue: async function (id) {
        const db = await this.open();
        return new Promise((resolve, reject) => {
            const tx = db.transaction('offlineQueue', 'readwrite');
            const store = tx.objectStore('offlineQueue');
            store.delete(id);
            tx.oncomplete = () => resolve();
            tx.onerror = (e) => reject(e.target.error);
        });
    }
};
