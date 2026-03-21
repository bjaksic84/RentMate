// ==========================================
// Layout Global Utilities
// ==========================================

// Restore scroll position and form data after language/currency change reload
(function() {
    const savedScrollPos = sessionStorage.getItem('rentmate_scroll_position');
    if (savedScrollPos) {
        sessionStorage.removeItem('rentmate_scroll_position');
        window.scrollTo(0, parseInt(savedScrollPos, 10));
    }

    // Restore form data
    const savedFormData = sessionStorage.getItem('rentmate_form_data');
    if (savedFormData) {
        sessionStorage.removeItem('rentmate_form_data');
        try {
            const formData = JSON.parse(savedFormData);
            // Wait for DOM to be ready
            const restoreForms = () => {
                formData.forEach(form => {
                    const formEl = document.querySelector(form.selector);
                    if (formEl) {
                        form.fields.forEach(field => {
                            const input = formEl.querySelector(`[name="${field.name}"]`);
                            if (input) {
                                if (input.type === 'checkbox' || input.type === 'radio') {
                                    input.checked = field.checked;
                                } else if (input.type === 'file') {
                                    // Can't restore file inputs for security reasons
                                } else {
                                    input.value = field.value;
                                    // Trigger change event for any dependent JS
                                    input.dispatchEvent(new Event('input', { bubbles: true }));
                                }
                            }
                        });
                    }
                });
            };
            if (document.readyState === 'loading') {
                document.addEventListener('DOMContentLoaded', restoreForms);
            } else {
                restoreForms();
            }
        } catch (e) {
            console.error('Failed to restore form data:', e);
        }
    }
})();

// Click outside to close dropdown menus
document.addEventListener('click', function(e) {
    // Close language menu
    const languageDropdown = document.getElementById('languageDropdown');
    const languageMenu = document.getElementById('languageMenu');
    if (languageDropdown && languageMenu && !languageDropdown.contains(e.target)) {
        languageMenu.classList.add('hidden');
    }
    // Close currency menu
    const currencyDropdown = document.getElementById('currencyDropdown');
    const currencyMenu = document.getElementById('currencyMenu');
    if (currencyDropdown && currencyMenu && !currencyDropdown.contains(e.target)) {
        currencyMenu.classList.add('hidden');
    }
    // Close theme menu
    const themeDropdown = document.getElementById('themeDropdown');
    const themeMenu = document.getElementById('themeMenu');
    if (themeDropdown && themeMenu && !themeDropdown.contains(e.target)) {
        themeMenu.classList.add('hidden');
    }
    // Close auth menu
    const authDropdown = document.getElementById('authDropdown');
    const authMenu = document.getElementById('authMenu');
    if (authDropdown && authMenu && !authDropdown.contains(e.target)) {
        authMenu.classList.add('hidden');
    }
    // Close mobile menu
    const mobileMenu = document.getElementById('mobileMenu');
    if (mobileMenu && !mobileMenu.classList.contains('hidden') && !mobileMenu.parentElement.contains(e.target)) {
        mobileMenu.classList.add('hidden');
    }
});

// Set a cookie with 1 year expiry
function setCookie(name, value) {
    const expires = new Date(Date.now() + 365 * 24 * 60 * 60 * 1000).toUTCString();
    document.cookie = `${name}=${value};expires=${expires};path=/`;
}

// Save all form data on the page
function saveFormData() {
    const forms = document.querySelectorAll('form');
    const formData = [];
    forms.forEach((form, index) => {
        const formId = form.id || form.getAttribute('action') || `form-${index}`;
        const selector = form.id ? `#${form.id}` : `form[action="${form.getAttribute('action')}"]`;
        const fields = [];
        form.querySelectorAll('input, textarea, select').forEach(input => {
            if (input.name && input.type !== 'hidden' && input.type !== 'file') {
                fields.push({
                    name: input.name,
                    value: input.value,
                    checked: input.checked
                });
            }
        });
        if (fields.length > 0) {
            formData.push({ selector, fields });
        }
    });
    sessionStorage.setItem('rentmate_form_data', JSON.stringify(formData));
}

// Seamless language switching - preserves scroll position and form data
function setLanguage(culture) {
    // Set the ASP.NET Core culture cookie
    setCookie('.AspNetCore.Culture', `c=${culture}|uic=${culture}`);

    // Clear cached translations
    sessionStorage.removeItem('rentmate_translations');
    sessionStorage.removeItem('rentmate_translations_version');

    // Save scroll position and form data, then reload
    sessionStorage.setItem('rentmate_scroll_position', window.scrollY.toString());
    saveFormData();
    window.location.reload();
}

// Seamless currency switching - preserves scroll position and form data
function setCurrency(currencyCode) {
    setCookie('RentMateCurrency', currencyCode);

    // Save scroll position and form data, then reload
    sessionStorage.setItem('rentmate_scroll_position', window.scrollY.toString());
    saveFormData();
    window.location.reload();
}

// ==========================================
// Theme Management (Light / Dark / System)
// ==========================================
function getThemePreference() {
    return localStorage.getItem('theme') || 'system';
}

function applyTheme(pref) {
    var isDark = pref === 'dark' || (pref === 'system' && window.matchMedia('(prefers-color-scheme: dark)').matches);
    document.documentElement.classList.toggle('dark', isDark);
    updateThemeUI(pref);
}

function setThemePreference(pref) {
    localStorage.setItem('theme', pref);
    applyTheme(pref);
    // Close the dropdown
    var menu = document.getElementById('themeMenu');
    if (menu) menu.classList.add('hidden');
}

function updateThemeUI(pref) {
    // Update desktop dropdown icons
    var icons = { light: '.theme-icon-light', dark: '.theme-icon-dark', system: '.theme-icon-system' };
    Object.keys(icons).forEach(function(key) {
        var el = document.querySelector('#themeDropdown ' + icons[key]);
        if (el) el.classList.toggle('hidden', key !== pref);
    });

    // Update desktop dropdown active states
    document.querySelectorAll('.theme-btn').forEach(function(btn) {
        var opt = btn.getAttribute('data-theme-option');
        var isActive = opt === pref;
        btn.classList.toggle('bg-blue-50', isActive);
        btn.classList.toggle('text-blue-700', isActive);
        btn.classList.toggle('font-medium', isActive);
        var check = btn.querySelector('.theme-check');
        if (check) check.classList.toggle('hidden', !isActive);
    });

    // Update mobile toggle active states
    document.querySelectorAll('.theme-mobile-btn').forEach(function(btn) {
        var opt = btn.getAttribute('data-theme-mobile');
        var isActive = opt === pref;
        btn.classList.toggle('bg-blue-50', isActive);
        btn.classList.toggle('text-blue-700', isActive);
        btn.classList.toggle('ring-2', isActive);
        btn.classList.toggle('ring-blue-300', isActive);
    });
}

// Listen for OS theme changes when in "system" mode
window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', function() {
    if (getThemePreference() === 'system') {
        applyTheme('system');
    }
});

// Apply theme on page load (after DOM is available for UI updates)
(function() {
    function initThemeUI() {
        applyTheme(getThemePreference());
    }
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initThemeUI);
    } else {
        initThemeUI();
    }
})();

// ==========================================
// Global SignalR Notification Badge
// ==========================================
// Connects to SignalR on all pages (for authenticated users) and
// bumps the navbar badge when a notification event fires.
// On the dashboard page, dashboard.js handles its own connection
// and reloads the page, so this just updates the badge count.
(function() {
    var body = document.body;
    if (body.getAttribute('data-authenticated') !== 'true') return;
    var hubUrl = body.getAttribute('data-signalr-hub');
    if (!hubUrl || typeof signalR === 'undefined') return;

    function bumpBadge() {
        ['navBadgeDesktop', 'navBadgeMobile', 'navBadgeBurger'].forEach(function(id) {
            var el = document.getElementById(id);
            if (!el) return;
            var count = parseInt(el.textContent, 10) || 0;
            el.textContent = ++count;
            el.classList.remove('hidden');
        });
    }

    function bumpAdminBadge() {
        ['navBadgeAdmin', 'navBadgeAdminMobile', 'navBadgeAdminDropdown'].forEach(function(id) {
            var el = document.getElementById(id);
            if (!el) return;
            var count = parseInt(el.textContent, 10) || 0;
            el.textContent = ++count;
            el.classList.remove('hidden');
        });
    }

    var conn = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl)
        .withAutomaticReconnect()
        .build();

    // User-facing events bump the dashboard badge
    ['RentalRequested', 'RentalStatusChanged',
     'ExtensionRequested', 'ExtensionStatusChanged',
     'RentalOverdue'].forEach(function(evt) {
        conn.on(evt, function() { bumpBadge(); });
    });

    // Deposit events bump both user badge and admin badge (on escalation)
    conn.on('DepositStatusChanged', function(data) {
        bumpBadge();
        if (data && data.status === 'Escalated') bumpAdminBadge();
    });

    conn.start().catch(function(err) {
        console.error('Global SignalR error:', err);
    });
})();
