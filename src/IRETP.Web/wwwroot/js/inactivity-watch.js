// Client-side inactivity watcher for internal DLD users (RFP 10.2.1 Access
// Control — 30-minute inactivity timeout). Triggers a warning 5 minutes
// before logout and signs the user out when the countdown hits zero.
// Public/investor users bypass this entirely — the watcher is only started
// when the Blazor layout decides the logged-in user is internal.
(function () {
    const IDLE_MS = 30 * 60 * 1000;       // 30 minutes — mandatory RFP ceiling
    const WARN_MS = 5 * 60 * 1000;        // last 5 minutes

    let lastActivity = Date.now();
    let tickHandle = null;
    let warningVisible = false;
    let dotnetRef = null;
    let overlayEl = null;

    function onActivity() {
        lastActivity = Date.now();
        if (warningVisible) hideWarning();
    }

    function showWarning(remainingSec) {
        if (!overlayEl) {
            overlayEl = document.createElement('div');
            overlayEl.setAttribute('role', 'alertdialog');
            overlayEl.setAttribute('aria-live', 'assertive');
            overlayEl.style.cssText =
                'position:fixed;bottom:24px;right:24px;background:#8a1e1e;color:#fff;' +
                'padding:14px 18px;border-radius:10px;box-shadow:0 8px 24px rgba(0,0,0,.25);' +
                'z-index:10000;max-width:320px;font:14px/1.4 system-ui,sans-serif;';
            document.body.appendChild(overlayEl);
        }
        overlayEl.innerHTML =
            '<div style="font-weight:600;margin-bottom:4px;">Session ending soon</div>' +
            '<div style="font-size:13px;">You will be signed out in ' +
            Math.ceil(remainingSec) + ' seconds due to inactivity. ' +
            'Click anywhere to stay signed in.</div>';
        warningVisible = true;
    }

    function hideWarning() {
        if (overlayEl) overlayEl.remove();
        overlayEl = null;
        warningVisible = false;
    }

    function tick() {
        const idleFor = Date.now() - lastActivity;
        if (idleFor >= IDLE_MS) {
            stop();
            if (dotnetRef) {
                try { dotnetRef.invokeMethodAsync('OnInactivityTimeout'); } catch {}
            } else {
                // Fallback — clear the cookie and bounce home.
                document.cookie = 'iretp.session=;expires=Thu, 01 Jan 1970 00:00:00 GMT;path=/;SameSite=Lax';
                window.location.href = '/auth/login?reason=inactivity';
            }
            return;
        }

        const remaining = IDLE_MS - idleFor;
        if (remaining <= WARN_MS) {
            showWarning(remaining / 1000);
        }
    }

    function start(ref) {
        stop();
        dotnetRef = ref;
        lastActivity = Date.now();
        ['mousemove', 'keydown', 'scroll', 'touchstart', 'click'].forEach(e =>
            document.addEventListener(e, onActivity, { passive: true }));
        tickHandle = setInterval(tick, 15_000);
    }

    function stop() {
        ['mousemove', 'keydown', 'scroll', 'touchstart', 'click'].forEach(e =>
            document.removeEventListener(e, onActivity));
        if (tickHandle) { clearInterval(tickHandle); tickHandle = null; }
        hideWarning();
        dotnetRef = null;
    }

    window.iretpInactivityWatch = { start, stop };
})();
