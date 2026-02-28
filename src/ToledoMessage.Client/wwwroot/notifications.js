window.toledoNotifications = {
    initialize: function () {
        if (!('Notification' in window)) {
            return false;
        }
        return Notification.permission === 'granted';
    },

    requestPermission: async function () {
        if (!('Notification' in window)) {
            return false;
        }
        var result = await Notification.requestPermission();
        return result === 'granted';
    },

    isTabFocused: function () {
        return document.hasFocus();
    },

    show: function (title, body) {
        if (Notification.permission !== 'granted') return;

        var notification = new Notification(title, {
            body: body,
            icon: '/favicon.ico',
            tag: 'toledo-message'
        });

        notification.onclick = function () {
            window.focus();
            notification.close();
        };

        // Auto-close after 5 seconds
        setTimeout(function () {
            notification.close();
        }, 5000);
    }
};
