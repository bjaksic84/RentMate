/**
 * RentMate Cookie Consent
 * Manages the consent banner, preferences modal, localStorage persistence,
 * and backend recording of consent choices.
 */
(function () {
    'use strict';

    var STORAGE_KEY = 'rentmate_consent';
    var COOKIE_NAME = 'rentmate_consent';
    var COOKIE_DAYS = 365;

    // ── Helpers ──────────────────────────────────────────────────────────

    function getStoredConsent() {
        try {
            var raw = localStorage.getItem(STORAGE_KEY);
            return raw ? JSON.parse(raw) : null;
        } catch (_) {
            return null;
        }
    }

    function storeConsent(analytics, marketing) {
        var data = { analytics: analytics, marketing: marketing, ts: Date.now() };
        try {
            localStorage.setItem(STORAGE_KEY, JSON.stringify(data));
        } catch (_) { /* storage full or blocked */ }

        // Also set a plain cookie so server-side code can read it if needed
        var expires = new Date(Date.now() + COOKIE_DAYS * 864e5).toUTCString();
        document.cookie = COOKIE_NAME + '=1; expires=' + expires + '; path=/; SameSite=Lax; Secure';
    }

    function getAntiforgeryToken() {
        var el = document.querySelector('input[name="__RequestVerificationToken"]');
        return el ? el.value : '';
    }

    function postConsent(analytics, marketing) {
        var body = new URLSearchParams();
        body.append('analytics', analytics);
        body.append('marketing', marketing);
        body.append('__RequestVerificationToken', getAntiforgeryToken());

        fetch('/api/cookie-consent', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: body.toString()
        }).catch(function () { /* non-fatal */ });
    }

    // ── Toggle state ─────────────────────────────────────────────────────

    var _pending = { analytics: false, marketing: false };

    function setToggleState(key, value) {
        if (key !== 'analytics' && key !== 'marketing') return;
        _pending[key] = value;
        var btn = document.getElementById('toggle-' + key);
        if (!btn) return;
        var knob = btn.querySelector('.toggle-knob');
        if (value) {
            btn.classList.remove('bg-slate-200', 'dark:bg-slate-600');
            btn.classList.add('bg-trust-blue-600');
            btn.setAttribute('aria-checked', 'true');
            if (knob) knob.style.transform = 'translateX(16px)';
        } else {
            btn.classList.remove('bg-trust-blue-600');
            btn.classList.add('bg-slate-200', 'dark:bg-slate-600');
            btn.setAttribute('aria-checked', 'false');
            if (knob) knob.style.transform = '';
        }
    }

    // ── Public API ───────────────────────────────────────────────────────

    window.toggleCookie = function (key) {
        setToggleState(key, !_pending[key]);
    };

    window.openCookieModal = function () {
        var stored = getStoredConsent();
        setToggleState('analytics', stored ? !!stored.analytics : false);
        setToggleState('marketing', stored ? !!stored.marketing : false);

        var modal = document.getElementById('cookie-modal');
        var banner = document.getElementById('cookie-banner');
        if (modal) modal.classList.remove('hidden');
        if (banner) banner.classList.add('hidden');
    };

    window.closeCookieModal = function () {
        var modal = document.getElementById('cookie-modal');
        if (modal) modal.classList.add('hidden');

        // Re-show banner if no consent recorded yet
        if (!getStoredConsent()) {
            var banner = document.getElementById('cookie-banner');
            if (banner) banner.classList.remove('hidden');
        }
    };

    window.acceptCookies = function (analytics, marketing) {
        storeConsent(analytics, marketing);
        postConsent(analytics, marketing);

        var banner = document.getElementById('cookie-banner');
        var modal = document.getElementById('cookie-modal');
        if (banner) banner.classList.add('hidden');
        if (modal) modal.classList.add('hidden');

        // Notify settings page (if open) to refresh status badges
        document.dispatchEvent(new CustomEvent('cookieConsentChanged', {
            detail: { analytics: analytics, marketing: marketing }
        }));
    };

    window.saveModalPreferences = function () {
        acceptCookies(_pending.analytics, _pending.marketing);
    };

    // ── Init ─────────────────────────────────────────────────────────────

    function init() {
        if (getStoredConsent()) return; // already decided

        var banner = document.getElementById('cookie-banner');
        if (banner) {
            // Small delay so the page layout settles before the banner slides in
            setTimeout(function () { banner.classList.remove('hidden'); }, 400);
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
}());
