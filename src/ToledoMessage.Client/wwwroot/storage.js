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
        if (name && name !== 'default') {
            localStorage.setItem('app.theme', name);
            document.documentElement.setAttribute('data-theme', name);
        } else {
            localStorage.removeItem('app.theme');
            document.documentElement.removeAttribute('data-theme');
        }
    },
    getTheme: function () {
        return localStorage.getItem('app.theme') || 'default';
    }
};
