// SignalR client for the IRETP notification hub (RFP Section 6.2 — in-platform
// push). Optional: if the SignalR library fails to load or the connection
// cannot be established, the app silently falls back to the existing polling
// behaviour. No errors are surfaced to the user.
(function () {
    const state = { connection: null, callback: null, token: null };

    function loadLibrary(callback) {
        if (window.signalR) { callback(); return; }
        const script = document.createElement('script');
        script.src = 'https://cdn.jsdelivr.net/npm/@microsoft/signalr@8/dist/browser/signalr.min.js';
        script.integrity = '';  // set by DLD during deployment hardening
        script.crossOrigin = 'anonymous';
        script.onload = callback;
        script.onerror = function () { console.warn('[IRETP] SignalR library failed to load — polling only'); };
        document.head.appendChild(script);
    }

    window.iretpNotifications = {
        // Start the hub connection. hubUrl is the absolute URL of the WebAPI
        // hub; accessToken is the current JWT so the hub can authenticate.
        // onPush is a JS callback invoked with the payload object on each push.
        start: function (hubUrl, accessToken, onPush) {
            if (!hubUrl) return;
            state.token = accessToken;
            state.callback = onPush;

            loadLibrary(function () {
                try {
                    if (state.connection) { try { state.connection.stop(); } catch {} }

                    const builder = new signalR.HubConnectionBuilder()
                        .withUrl(hubUrl, { accessTokenFactory: () => state.token || '' })
                        .withAutomaticReconnect();

                    state.connection = builder.build();

                    state.connection.on('notification', function (payload) {
                        try { if (state.callback) state.callback.invokeMethodAsync('OnPush', payload); }
                        catch (e) { console.warn('[IRETP] notification callback failed', e); }
                    });

                    state.connection.start().catch(function (err) {
                        console.warn('[IRETP] SignalR connect failed — polling only', err);
                    });
                } catch (e) {
                    console.warn('[IRETP] SignalR start failed', e);
                }
            });
        },

        stop: function () {
            if (state.connection) {
                try { state.connection.stop(); } catch { /* ignore */ }
                state.connection = null;
            }
        },

        updateToken: function (accessToken) { state.token = accessToken; }
    };
})();
