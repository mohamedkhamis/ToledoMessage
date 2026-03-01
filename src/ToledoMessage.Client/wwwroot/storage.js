window.toledoStorage = {
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
    prefersDarkMode: function () {
        return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
    },
    setFontSize: function (size) {
        if (size && size !== 'medium') {
            localStorage.setItem('app.fontSize', size);
            document.documentElement.setAttribute('data-font-size', size);
        } else {
            localStorage.removeItem('app.fontSize');
            document.documentElement.removeAttribute('data-font-size');
        }
    },
    getFontSize: function () {
        return localStorage.getItem('app.fontSize') || 'medium';
    },
    clearAuthData: function () {
        // Only clear the auth token — preserve device identity, crypto keys, sessions,
        // and preferences so the same device is reused on re-login and old messages
        // remain decryptable (Signal Protocol sessions are forward-secret).
        localStorage.removeItem('auth.token');
    },
    clearAllData: function () {
        // Full wipe for account deletion — clear everything except UI preferences
        var preserve = ['app.theme', 'app.fontSize'];
        var saved = {};
        for (var i = 0; i < preserve.length; i++) {
            var v = localStorage.getItem(preserve[i]);
            if (v !== null) saved[preserve[i]] = v;
        }
        localStorage.clear();
        for (var key in saved) {
            localStorage.setItem(key, saved[key]);
        }
    }
};

// ─── IndexedDB Message Store ───
window.toledoMessageStore = {
    _db: null,
    _dbName: 'ToledoMessages',
    _version: 1,

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
    // ReSharper disable  VariableUsedInInnerScopeBeforeDeclared
    storeMessages: async function (msgs) {
        if (!msgs || msgs.length === 0) return new Promise((resolve, reject) => {
            const tx = db.transaction('messages', 'readwrite');
            const store = tx.objectStore('messages');
            for (const msg of msgs) {
                store.put(msg);
            }
            tx.oncomplete = () => resolve();
            tx.onerror = (e) => reject(e.target.error);
        });
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

    deleteConversationMessages: async function (conversationId) {
        const db = await this.open();
        return new Promise((resolve, reject) => {
            const tx = db.transaction('messages', 'readwrite');
            const store = tx.objectStore('messages');
            const index = store.index('conversationId');
            const request = index.openCursor(conversationId);
            request.onsuccess = function (e) {
                const cursor = e.target.result;
                if (cursor) { cursor.delete(); cursor.continue(); }
            };
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
            const tx = db.transaction(['messages', 'meta'], 'readwrite');
            tx.objectStore('messages').clear();
            tx.objectStore('meta').clear();
            tx.oncomplete = () => resolve();
            tx.onerror = (e) => reject(e.target.error);
        });
    }
};
