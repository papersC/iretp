// Persist language in the ASP.NET culture cookie and trigger a soft reload so
// every component (including the static layout) re-renders in the new locale.
// The server's RequestLocalization middleware reads .AspNetCore.Culture and
// applies the matching CultureInfo for the next request.
window.iretpUi = {
    setSessionCookie: function (value) {
        try {
            var expires = new Date();
            expires.setDate(expires.getDate() + 7);
            // Value is encoded (base64 of token\u0001email\u0001name).
            document.cookie = 'iretp.session=' + encodeURIComponent(value)
                + ';expires=' + expires.toUTCString() + ';path=/;SameSite=Lax';
        } catch (e) { console.error('setSessionCookie failed', e); }
    },
    clearSessionCookie: function () {
        document.cookie = 'iretp.session=;expires=Thu, 01 Jan 1970 00:00:00 GMT;path=/;SameSite=Lax';
    },
    applyCurrency: function (code) {
        try {
            var expires = new Date();
            expires.setFullYear(expires.getFullYear() + 1);
            document.cookie = 'iretp.currency=' + encodeURIComponent(code)
                + ';expires=' + expires.toUTCString() + ';path=/;SameSite=Lax';
            window.location.reload();
        } catch (e) { console.error('applyCurrency failed', e); }
    },
    applyLanguage: function (lang, isRtl) {
        try {
            var cookieValue = 'c=' + lang + '|uic=' + lang;
            // Encode exactly as CookieRequestCultureProvider expects.
            var encoded = encodeURIComponent(cookieValue);
            var expires = new Date();
            expires.setFullYear(expires.getFullYear() + 1);
            document.cookie = '.AspNetCore.Culture=' + encoded
                + ';expires=' + expires.toUTCString() + ';path=/;SameSite=Lax';
            // Update DOM immediately so the reload looks snappy.
            document.documentElement.lang = lang || 'en';
            document.documentElement.dir = isRtl ? 'rtl' : 'ltr';
            // Reload to pick up culture-aware server rendering.
            window.location.reload();
        } catch (e) { console.error('applyLanguage failed', e); }
    }
};

window.iretpDownload = {
    // Trigger a browser download for a base64-encoded payload.
    fromBase64: function (fileName, contentType, base64) {
        try {
            const byteChars = atob(base64);
            const byteNumbers = new Array(byteChars.length);
            for (let i = 0; i < byteChars.length; i++) {
                byteNumbers[i] = byteChars.charCodeAt(i);
            }
            const blob = new Blob([new Uint8Array(byteNumbers)], { type: contentType });
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = fileName;
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            setTimeout(() => URL.revokeObjectURL(url), 1000);
        } catch (e) {
            console.error('iretpDownload failed', e);
        }
    },

    // Copy text to clipboard (used by the "Share link" button).
    copyToClipboard: function (text) {
        if (!navigator.clipboard) {
            const ta = document.createElement('textarea');
            ta.value = text; document.body.appendChild(ta); ta.select();
            try { document.execCommand('copy'); } catch {}
            document.body.removeChild(ta);
            return;
        }
        navigator.clipboard.writeText(text).catch(e => console.error(e));
    }
};
