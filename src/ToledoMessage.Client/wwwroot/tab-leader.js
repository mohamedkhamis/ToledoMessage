window.toledoTabLeader = (function () {
    let channel = null;
    let tabId = null;
    let dotNetRef = null;
    let heartbeatInterval = null;
    let leaderTabId = null;
    let isLeader = false;

    const channelName = 'toledo-tab-leader';
    const heartbeatMs = 3000;
    const leaderTimeoutMs = 6000;
    let lastLeaderHeartbeat = 0;

    function initialize(id, ref) {
        tabId = id;
        dotNetRef = ref;

        if (!('BroadcastChannel' in window)) {
            // Fallback: if BroadcastChannel not supported, this tab is always leader
            becomeLeader();
            return;
        }

        channel = new BroadcastChannel(channelName);
        channel.onmessage = handleMessage;

        // Announce presence and request leader status
        channel.postMessage({ type: 'election-request', tabId: tabId });

        // If no leader responds within timeout, become leader
        setTimeout(function () {
            if (!leaderTabId) {
                becomeLeader();
            }
        }, 1500);

        // Periodic check: if leader hasn't sent heartbeat, start election
        heartbeatInterval = setInterval(function () {
            if (isLeader) {
                channel.postMessage({ type: 'heartbeat', tabId: tabId });
            } else if (Date.now() - lastLeaderHeartbeat > leaderTimeoutMs) {
                // Leader seems dead, start election
                becomeLeader();
            }
        }, heartbeatMs);

        // When this tab closes, notify others
        window.addEventListener('beforeunload', function () {
            if (isLeader && channel) {
                channel.postMessage({ type: 'leader-leaving', tabId: tabId });
            }
        });
    }

    function handleMessage(event) {
        var msg = event.data;

        switch (msg.type) {
            case 'election-request':
                if (isLeader) {
                    channel.postMessage({ type: 'leader-announce', tabId: tabId });
                }
                break;

            case 'leader-announce':
                leaderTabId = msg.tabId;
                lastLeaderHeartbeat = Date.now();
                if (isLeader && msg.tabId !== tabId) {
                    // Another tab claimed leadership with lower/different id - defer
                    isLeader = false;
                    try { dotNetRef.invokeMethodAsync('OnLeaderElected', false); } catch (e) { /* .NET ref disposed */ }
                }
                break;

            case 'heartbeat':
                if (msg.tabId === leaderTabId) {
                    lastLeaderHeartbeat = Date.now();
                }
                break;

            case 'leader-leaving':
                if (msg.tabId === leaderTabId) {
                    leaderTabId = null;
                    // Try to become the new leader
                    setTimeout(function () {
                        if (!leaderTabId) {
                            becomeLeader();
                        }
                    }, Math.random() * 500);
                }
                break;
        }
    }

    function becomeLeader() {
        isLeader = true;
        leaderTabId = tabId;
        lastLeaderHeartbeat = Date.now();
        if (channel) {
            channel.postMessage({ type: 'leader-announce', tabId: tabId });
        }
        if (dotNetRef) {
            try { dotNetRef.invokeMethodAsync('OnLeaderElected', true); } catch (e) { /* .NET ref disposed */ }
        }
    }

    function dispose() {
        if (heartbeatInterval) {
            clearInterval(heartbeatInterval);
            heartbeatInterval = null;
        }
        if (channel) {
            if (isLeader) {
                channel.postMessage({ type: 'leader-leaving', tabId: tabId });
            }
            channel.close();
            channel = null;
        }
    }

    return { initialize: initialize, dispose: dispose };
})();
