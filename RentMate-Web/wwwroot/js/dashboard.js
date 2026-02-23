/**
 * Dashboard Core
 *
 * Tab switching, lazy loading, slide-over panel, attention bar,
 * utilities, extension modal, deposit resolution, early return,
 * review modal, keyboard handlers, and SignalR real-time updates.
 */

// === Global error handler (temporary debug) ===
window.addEventListener('error', function(e) {
    console.error('[Dashboard Error]', e.message, e.filename, e.lineno);
});

// === Tab System State ===
var tabCacheValid = { history: false, favorites: false };
var currentPanelRentalId = null;
var lastClickedRentalDetailsLink = null; // for returning focus after panel close

// === Tab Switching ===
var TAB_NAMES = ['my-rentals', 'renting-out', 'my-items', 'history', 'favorites'];

function switchTab(tabName) {
    if (!TAB_NAMES.includes(tabName)) tabName = 'my-rentals';

    // Update tab buttons
    TAB_NAMES.forEach(function(name) {
        var btn = document.getElementById('tab-' + name);
        var panel = document.getElementById('tabpanel-' + name);
        if (!btn || !panel) return;

        var isActive = (name === tabName);
        btn.setAttribute('aria-selected', isActive ? 'true' : 'false');
        btn.setAttribute('tabindex', isActive ? '0' : '-1');
        btn.classList.toggle('active', isActive);

        if (isActive) {
            panel.classList.remove('hidden');
            // Trigger opacity transition
            panel.style.opacity = '0';
            requestAnimationFrame(function() {
                requestAnimationFrame(function() {
                    panel.style.opacity = '1';
                });
            });
        } else {
            panel.classList.add('hidden');
        }
    });

    // Persist to URL hash and localStorage
    try { localStorage.setItem('dashboard-tab', tabName); } catch(e) {}
    history.replaceState(null, '', window.location.pathname + '#' + tabName + (window.location.search || ''));

    // Lazy-load history/favorites on first visit (or when stale)
    if (tabName === 'history') loadTabContent('history');
    if (tabName === 'favorites') loadTabContent('favorites');
}

// === Lazy Tab Loading ===
function loadTabContent(tabName) {
    var panel = document.getElementById('tabpanel-' + tabName);
    if (!panel) return;

    var skeletonId = tabName + '-skeleton';
    var contentId = tabName + '-content';
    var skeleton = document.getElementById(skeletonId);

    // If already loaded and cache valid, do nothing
    if (tabCacheValid[tabName] && document.getElementById(contentId)) return;

    // If already loaded but stale, remove old content and re-fetch
    var old = document.getElementById(contentId);
    if (old) old.remove();

    // Show skeleton
    if (skeleton) skeleton.classList.remove('hidden');

    var url = tabName === 'history'
        ? '/Dashboard/HistoryPartial?page=1&pageSize=20'
        : '/Dashboard/FavoritesPartial';

    fetch(url)
        .then(function(r) {
            if (!r.ok) throw new Error('HTTP ' + r.status);
            return r.text();
        })
        .then(function(html) {
            if (skeleton) skeleton.classList.add('hidden');

            var container = document.createElement('div');
            container.id = contentId;
            container.innerHTML = html;
            panel.appendChild(container);

            tabCacheValid[tabName] = true;
        })
        .catch(function() {
            if (skeleton) skeleton.classList.add('hidden');

            // Remove any stale cache flag so next click retries
            tabCacheValid[tabName] = false;

            // Show inline error row
            var old2 = document.getElementById(contentId);
            if (old2) old2.remove();

            var errEl = document.createElement('div');
            errEl.id = contentId;
            errEl.className = 'bg-rose-50 dark:bg-rose-900/20 text-rose-700 dark:text-rose-400 rounded-lg px-4 py-3 text-sm';
            errEl.innerHTML = (T && T['Could not load']) ? T['Could not load'] : 'Could not load.'
                + ' <button class="underline font-medium" onclick="tabCacheValid[\'' + tabName + '\']=false;var el=document.getElementById(\'' + contentId + '\');if(el)el.remove();loadTabContent(\'' + tabName + '\')">'
                + ((T && T['Try again']) ? T['Try again'] : 'Try again') + '</button>';
            panel.appendChild(errEl);
        });
}

// === Page Load: determine initial tab ===
document.addEventListener('DOMContentLoaded', function() {
    // 1. Deep-link params (handled below after rentalData is available)
    // 2. URL hash
    // 3. localStorage
    // 4. Default: my-rentals
    var params = new URLSearchParams(window.location.search);
    var deepRentalId = params.get('rentalId');
    var deepAction   = params.get('action');

    var startTab = 'my-rentals';

    if (deepRentalId) {
        // Determine which tab this rental belongs to by checking window.rentalData
        startTab = _getTabForRental(parseInt(deepRentalId, 10));
    } else {
        var hash = window.location.hash.replace('#', '');
        if (TAB_NAMES.includes(hash)) {
            startTab = hash;
        } else {
            try {
                var saved = localStorage.getItem('dashboard-tab');
                if (saved && TAB_NAMES.includes(saved)) startTab = saved;
            } catch(e) {}
        }
    }

    switchTab(startTab);

    // Handle deep link after tab switch
    if (deepRentalId) {
        // Clean up query params from URL
        var cleanUrl = window.location.pathname + '#' + startTab;
        history.replaceState(null, '', cleanUrl);

        // Open the detail panel (will be wired in Step 3)
        if (typeof openRentalDetail === 'function') {
            openRentalDetail(parseInt(deepRentalId, 10), deepAction || null);
        }
    }

    // Wire attention bar chips
    document.querySelectorAll('.attention-chip').forEach(function(chip) {
        chip.addEventListener('click', function() {
            var targetTab = chip.dataset.attentionTab;
            var attType   = chip.dataset.attentionType;
            switchTab(targetTab);
            scrollToAttentionItem(attType);
        });
    });

    // Arrow key roving tabindex on tab bar
    var tablist = document.getElementById('dashboard-tablist');
    if (tablist) {
        tablist.addEventListener('keydown', function(e) {
            var tabs = Array.from(tablist.querySelectorAll('[role="tab"]'));
            var idx = tabs.indexOf(document.activeElement);
            if (idx === -1) return;

            var next = -1;
            if (e.key === 'ArrowRight') next = (idx + 1) % tabs.length;
            if (e.key === 'ArrowLeft')  next = (idx - 1 + tabs.length) % tabs.length;
            if (e.key === 'Home') next = 0;
            if (e.key === 'End')  next = tabs.length - 1;

            if (next !== -1) {
                e.preventDefault();
                tabs[next].focus();
                var name = tabs[next].id.replace('tab-', '');
                switchTab(name);
            }
        });
    }
});

// === Determine which tab a rental belongs to ===
function _getTabForRental(rentalId) {
    if (!window.rentalData) return 'my-rentals';
    var entry = window.rentalData[rentalId];
    if (!entry) return 'my-rentals';
    return entry.role === 'owner' ? 'renting-out' : 'my-rentals';
}

// === Scroll to attention item ===
function scrollToAttentionItem(attType) {
    // Small delay to allow tab panel to become visible
    setTimeout(function() {
        // Scope search to the active tab panel so hidden tab cards are not matched
        var activePanel = document.querySelector('[role="tabpanel"]:not(.hidden)');
        var scope = activePanel || document;
        var card = scope.querySelector('[data-attention-type="' + attType + '"]');
        if (card) {
            card.scrollIntoView({ behavior: 'smooth', block: 'center' });
            card.classList.add('attention-highlight');
            setTimeout(function() { card.classList.remove('attention-highlight'); }, 1500);
        }
    }, 100);
}

// === History Tab Functions ===
// These must live here (not in the lazy-loaded partial) because innerHTML does not execute <script> tags.

function filterHistory(filter) {
    document.querySelectorAll('.history-filter-btn').forEach(function(btn) {
        var isActive = btn.dataset.filter === filter;
        if (isActive) {
            btn.classList.add('bg-white', 'dark:bg-ledger-700', 'text-ledger-900', 'dark:text-white', 'shadow-sm');
            btn.classList.remove('text-ledger-500', 'dark:text-ledger-400', 'hover:bg-white/60', 'dark:hover:bg-ledger-700/50');
        } else {
            btn.classList.remove('bg-white', 'dark:bg-ledger-700', 'text-ledger-900', 'dark:text-white', 'shadow-sm');
            btn.classList.add('text-ledger-500', 'dark:text-ledger-400', 'hover:bg-white/60', 'dark:hover:bg-ledger-700/50');
        }
    });
    document.querySelectorAll('.history-row').forEach(function(row) {
        if (filter === 'all' || row.dataset.role === filter) {
            row.classList.remove('hidden');
        } else {
            row.classList.add('hidden');
        }
    });
}

function loadMoreHistory(page) {
    var btn = document.getElementById('history-load-more');
    if (btn) btn.innerHTML = '<span class="text-sm text-ledger-400">Loading...</span>';

    fetch('/Dashboard/HistoryPartial?page=' + page + '&pageSize=20')
        .then(function(r) {
            if (!r.ok) throw new Error('HTTP ' + r.status);
            return r.text();
        })
        .then(function(html) {
            var tmp = document.createElement('div');
            tmp.innerHTML = html;
            var newRows = tmp.querySelectorAll('.history-row');
            var existingRows = document.getElementById('history-rows');
            if (existingRows) {
                newRows.forEach(function(row) { existingRows.appendChild(row); });
            }
            var newLoadMore = tmp.querySelector('#history-load-more');
            if (btn) {
                if (newLoadMore) { btn.outerHTML = newLoadMore.outerHTML; }
                else { btn.remove(); }
            }
            var activeFilter = document.querySelector('.history-filter-btn.shadow-sm');
            if (activeFilter && activeFilter.dataset.filter !== 'all') {
                filterHistory(activeFilter.dataset.filter);
            }
        })
        .catch(function() {
            if (btn) btn.innerHTML = '<span class="text-sm text-rose-600">' + (T && T['Could not load'] ? T['Could not load'] : 'Could not load.') + ' <button class="underline" onclick="loadMoreHistory(' + page + ')">Try again</button></span>';
        });
}

// === Utility Functions ===
function formatCurrency(amount) {
    var lang = document.documentElement.lang || 'sl';
    var currency = (window.CurrentCurrency && window.CurrentCurrency.Code) || 'EUR';
    return new Intl.NumberFormat(lang, { style: 'currency', currency: currency }).format(amount);
}

function getToken() {
    return document.querySelector('input[name="__RequestVerificationToken"]')?.value;
}

function showToast(message, type) {
    var container = document.getElementById('toastContainer');
    var toast = document.createElement('div');
    var bgClass = type === 'success' ? 'bg-emerald-600' : type === 'error' ? 'bg-rose-600' : 'bg-primary-600';
    toast.className = bgClass + ' text-white px-4 py-3 rounded-lg shadow-lg text-sm font-medium animate-fade-in';
    toast.textContent = message;
    container.appendChild(toast);
    setTimeout(function () { toast.remove(); }, 4000);
}

// === Toggle Listing ===
async function toggleListing(itemId, btn) {
    try {
        var response = await fetch('/Items/ToggleListing/' + itemId, {
            method: 'POST',
            headers: { 'X-CSRF-TOKEN': getToken() }
        });
        if (response.ok) location.reload();
    } catch (error) {
        console.error('Error toggling listing:', error);
    }
}

// === Upload Evidence (shared helper) ===
async function uploadEvidence(rentalId, file, notes) {
    if (!file) return { success: true };

    var formData = new FormData();
    formData.append('rentalId', rentalId);
    formData.append('file', file);
    if (notes) formData.append('notes', notes);
    var token = getToken();
    if (token) formData.append('__RequestVerificationToken', token);

    try {
        var response = await fetch('/Dashboard/UploadDisputeEvidence', {
            method: 'POST',
            headers: { 'X-CSRF-TOKEN': getToken() },
            body: formData
        });
        return await response.json();
    } catch (err) {
        console.error('Evidence upload error:', err);
        return { success: false, message: 'Upload failed' };
    }
}

// === Extension Modal ===
var extState = { rentalId: 0, currentStartDate: null, currentEndDate: null, dailyRate: 0, autoApprove: false };

function openExtensionModal(rentalId, startDate, currentEndDate, dailyRate, itemId, autoApprove) {
    extState.rentalId = rentalId;
    extState.currentStartDate = new Date(startDate + 'T00:00:00');
    extState.currentEndDate = new Date(currentEndDate + 'T00:00:00');
    extState.dailyRate = dailyRate;
    extState.autoApprove = autoApprove;

    document.getElementById('ext-rental-id').value = rentalId;

    var lang = document.documentElement.lang || 'en';
    var startFmt = extState.currentStartDate.toLocaleDateString(lang, { day: 'numeric', month: 'short' });
    var endFmt = extState.currentEndDate.toLocaleDateString(lang, { day: 'numeric', month: 'short', year: 'numeric' });
    document.getElementById('ext-rental-period').textContent = startFmt + ' \u2013 ' + endFmt;
    document.getElementById('ext-daily-rate').textContent = formatCurrency(dailyRate) + '/' + T["day"];
    document.getElementById('ext-cost-preview').classList.add('hidden');
    document.getElementById('ext-submit-btn').disabled = true;

    var notice = document.getElementById('ext-approval-notice');
    if (autoApprove) { notice.classList.add('hidden'); } else { notice.classList.remove('hidden'); }

    var userHighlight = { from: new Date(startDate + 'T00:00:00'), to: new Date(currentEndDate + 'T00:00:00') };
    function toLocalISODate(d) {
        return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0');
    }
    var minDateForSelection = new Date(extState.currentEndDate);
    minDateForSelection.setDate(minDateForSelection.getDate() + 1);
    var minDateStr = toLocalISODate(minDateForSelection);
    var calendarMinStr = startDate;

    fetch('/Items/GetBookedDates?itemId=' + itemId)
        .then(function(r) { return r.json(); })
        .then(function(bookings) {
            var blocked = { dates: [], ranges: [] };
            var nextBookingStart = null;
            bookings.forEach(function(b) {
                if (b.id !== rentalId) {
                    var bFrom = new Date(b.from + 'T00:00:00');
                    var bTo = new Date(b.to + 'T00:00:00');
                    blocked.ranges.push({ from: bFrom, to: bTo });
                    if (bFrom > extState.currentEndDate) {
                        if (!nextBookingStart || bFrom < nextBookingStart) {
                            nextBookingStart = bFrom;
                        }
                    }
                }
            });

            var calendarMaxStr = null;
            var blockedNotice = document.getElementById('ext-blocked-notice');
            if (nextBookingStart) {
                var maxDate = new Date(nextBookingStart);
                maxDate.setDate(maxDate.getDate() - 1);
                calendarMaxStr = toLocalISODate(maxDate);
                var lang = document.documentElement.lang || 'en';
                document.getElementById('ext-blocked-date').textContent = nextBookingStart.toLocaleDateString(lang, { day: 'numeric', month: 'short', year: 'numeric' });
                blockedNotice.classList.remove('hidden');
            } else {
                blockedNotice.classList.add('hidden');
            }

            SmartCalendar.update('extCalendar', {
                minDate: calendarMinStr,
                selectableMinDate: minDateStr,
                maxDate: calendarMaxStr,
                disabled: blocked,
                highlights: [userHighlight]
            });
        })
        .catch(function() {
            document.getElementById('ext-blocked-notice').classList.add('hidden');
            SmartCalendar.update('extCalendar', {
                minDate: calendarMinStr,
                selectableMinDate: minDateStr,
                maxDate: null,
                disabled: { dates: [], ranges: [] },
                highlights: [userHighlight]
            });
        });

    var modal = document.getElementById('extension-modal');
    pushMobileModalContent();
    modal.classList.remove('hidden');
    modal.classList.add('flex');
    document.body.style.overflow = 'hidden';
    setTimeout(function() { trapFocus(modal); }, 50);
}

function closeExtensionModal() {
    popMobileModalContent();
    releaseFocus();
    var modal = document.getElementById('extension-modal');
    modal.classList.add('hidden');
    modal.classList.remove('flex');
    document.body.style.overflow = '';
}

document.getElementById('extCalendar')?.addEventListener('smartcalendar:change', function(e) {
    var selectedDate = e.detail.startDate;
    if (!selectedDate) {
        document.getElementById('ext-cost-preview').classList.add('hidden');
        document.getElementById('ext-submit-btn').disabled = true;
        return;
    }
    var newDate = new Date(selectedDate + 'T00:00:00');
    var extraDays = Math.round((newDate - extState.currentEndDate) / 86400000);
    if (extraDays <= 0) {
        document.getElementById('ext-cost-preview').classList.add('hidden');
        document.getElementById('ext-submit-btn').disabled = true;
        return;
    }
    document.getElementById('ext-extra-days').textContent = extraDays;
    document.getElementById('ext-additional-cost').textContent = formatCurrency(extraDays * extState.dailyRate);
    document.getElementById('ext-cost-preview').classList.remove('hidden');
    document.getElementById('ext-submit-btn').disabled = false;
});

document.querySelectorAll('[data-action="close-extension"]').forEach(function (el) {
    el.addEventListener('click', closeExtensionModal);
});

document.getElementById('extension-form')?.addEventListener('submit', async function (e) {
    e.preventDefault();
    var btn = document.getElementById('ext-submit-btn');
    btn.disabled = true;
    btn.textContent = T["Submitting..."];

    var formData = new FormData(this);
    try {
        var response = await fetch('/Dashboard/RequestExtension', {
            method: 'POST',
            body: formData,
            headers: { 'X-CSRF-TOKEN': formData.get('__RequestVerificationToken') }
        });
        var data = await response.json();
        if (data.success) {
            closeExtensionModal();
            showToast(data.message, 'success');
            setTimeout(function () { location.reload(); }, 1500);
        } else {
            showToast(data.message, 'error');
            btn.disabled = false;
            btn.textContent = T["Request Extension"];
        }
    } catch (err) {
        showToast(T["Something went wrong"], 'error');
        btn.disabled = false;
        btn.textContent = T["Request Extension"];
    }
});

// === Extension Approval/Decline ===
async function approveExtension(extensionId) {
    try {
        var token = getToken();
        var response = await fetch('/Dashboard/ApproveExtension', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'X-CSRF-TOKEN': token },
            body: 'extensionId=' + extensionId + (token ? '&__RequestVerificationToken=' + encodeURIComponent(token) : '')
        });
        var data = await response.json();
        showToast(data.message, data.success ? 'success' : 'error');
        if (data.success) setTimeout(function () { location.reload(); }, 1000);
    } catch (err) { showToast(T["Something went wrong"], 'error'); }
}

async function declineExtension(extensionId) {
    try {
        var token = getToken();
        var response = await fetch('/Dashboard/DeclineExtension', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'X-CSRF-TOKEN': token },
            body: 'extensionId=' + extensionId + (token ? '&__RequestVerificationToken=' + encodeURIComponent(token) : '')
        });
        var data = await response.json();
        showToast(data.message, data.success ? 'success' : 'error');
        if (data.success) setTimeout(function () { location.reload(); }, 1000);
    } catch (err) { showToast(T["Something went wrong"], 'error'); }
}

async function cancelExtension(extensionId) {
    try {
        var token = getToken();
        var response = await fetch('/Dashboard/CancelExtension', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'X-CSRF-TOKEN': token },
            body: 'extensionId=' + extensionId + (token ? '&__RequestVerificationToken=' + encodeURIComponent(token) : '')
        });
        var data = await response.json();
        showToast(data.message, data.success ? 'success' : 'error');
        if (data.success) setTimeout(function () { location.reload(); }, 1000);
    } catch (err) { showToast(T["Something went wrong"], 'error'); }
}

// === Deposit Resolution Modal ===
var depState = { rentalId: 0, amount: 0 };

function openDepositResolution(rentalId, amount, status) {
    depState.rentalId = rentalId;
    depState.amount = amount;
    document.getElementById('dep-rental-id').value = rentalId;
    document.getElementById('dep-amount').textContent = formatCurrency(amount);
    document.getElementById('dep-charge-amount').value = '';
    document.getElementById('dep-charge-amount').max = amount;
    document.getElementById('dep-charge-reason').value = '';

    var modal = document.getElementById('deposit-modal');
    pushMobileModalContent();
    modal.classList.remove('hidden');
    modal.classList.add('flex');
    document.body.style.overflow = 'hidden';
    setTimeout(function() { trapFocus(modal); }, 50);
}

function closeDepositModal() {
    popMobileModalContent();
    releaseFocus();
    var modal = document.getElementById('deposit-modal');
    modal.classList.add('hidden');
    modal.classList.remove('flex');
    document.body.style.overflow = '';
    clearDepChargeEvidence();
}

function updateDepChargeFilename(input) {
    var fileName = input.files[0]?.name;
    if (fileName) {
        document.getElementById('dep-charge-filename').textContent = fileName;
        document.getElementById('dep-charge-preview').classList.remove('hidden');
    }
}

function clearDepChargeEvidence() {
    var input = document.getElementById('dep-charge-evidence');
    if (input) input.value = '';
    var preview = document.getElementById('dep-charge-preview');
    if (preview) preview.classList.add('hidden');
}

document.querySelectorAll('[data-action="close-deposit"]').forEach(function (el) {
    el.addEventListener('click', closeDepositModal);
});

async function releaseDeposit() {
    var btn = document.getElementById('dep-release-btn');
    btn.disabled = true;
    try {
        var token = getToken();
        var response = await fetch('/Dashboard/ReleaseDeposit', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'X-CSRF-TOKEN': token },
            body: 'rentalId=' + depState.rentalId + (token ? '&__RequestVerificationToken=' + encodeURIComponent(token) : '')
        });
        var data = await response.json();
        showToast(data.message, data.success ? 'success' : 'error');
        if (data.success) { closeDepositModal(); setTimeout(function () { location.reload(); }, 1000); }
        else { btn.disabled = false; }
    } catch (err) { showToast(T["Something went wrong"], 'error'); btn.disabled = false; }
}

async function chargeDeposit() {
    var amount = parseFloat(document.getElementById('dep-charge-amount').value);
    var reason = document.getElementById('dep-charge-reason').value.trim();
    var fileInput = document.getElementById('dep-charge-evidence');

    if (!amount || amount <= 0) { showToast(T["Enter a valid amount"], 'error'); return; }
    if (amount > depState.amount) { showToast(T["Amount exceeds deposit"], 'error'); return; }
    if (!reason) { showToast(T["Please provide a reason"], 'error'); return; }
    if (!fileInput || fileInput.files.length === 0) { showToast(T["Please provide a picture for proof"], 'error'); return; }

    var btn = document.getElementById('dep-charge-btn');
    btn.disabled = true;
    try {
        var token = getToken();
        var formData = new FormData();
        formData.append('rentalId', depState.rentalId);
        formData.append('amount', amount);
        formData.append('reason', reason);
        formData.append('evidence', fileInput.files[0]);
        if (token) formData.append('__RequestVerificationToken', token);

        var response = await fetch('/Dashboard/ChargeDeposit', {
            method: 'POST',
            headers: { 'X-CSRF-TOKEN': token },
            body: formData
        });
        var data = await response.json();
        if (data.success) {
            showToast(data.message, 'success');
            closeDepositModal();
            setTimeout(function () { location.reload(); }, 1000);
        } else {
            showToast(data.message, 'error');
            btn.disabled = false;
        }
    } catch (err) { showToast(T["Something went wrong"], 'error'); btn.disabled = false; }
}

// === Charge Full Checkbox (Deposit Modal) ===
function toggleDepChargeFull(checked) {
    var amountInput = document.getElementById('dep-charge-amount');
    if (checked) {
        amountInput.value = depState.amount;
        amountInput.readOnly = true;
        amountInput.classList.add('opacity-60');
    } else {
        amountInput.value = '';
        amountInput.readOnly = false;
        amountInput.classList.remove('opacity-60');
    }
}

// === Early Return + Deposit Modal ===
var erState = { rentalId: 0, amount: 0 };

function openEarlyReturnModal(rentalId, amount) {
    erState.rentalId = rentalId;
    erState.amount = amount;
    document.getElementById('er-rental-id').value = rentalId;
    document.getElementById('er-amount').textContent = formatCurrency(amount);
    document.getElementById('er-charge-amount').value = '';
    document.getElementById('er-charge-amount').max = amount;
    document.getElementById('er-charge-reason').value = '';

    var modal = document.getElementById('early-return-modal');
    pushMobileModalContent();
    modal.classList.remove('hidden');
    modal.classList.add('flex');
    document.body.style.overflow = 'hidden';
    setTimeout(function() { trapFocus(modal); }, 50);
}

function closeEarlyReturnModal() {
    popMobileModalContent();
    releaseFocus();
    var modal = document.getElementById('early-return-modal');
    modal.classList.add('hidden');
    modal.classList.remove('flex');
    document.body.style.overflow = '';
    clearErChargeEvidence();
}

function updateErChargeFilename(input) {
    var fileName = input.files[0]?.name;
    if (fileName) {
        document.getElementById('er-charge-filename').textContent = fileName;
        document.getElementById('er-charge-preview').classList.remove('hidden');
    }
}

function clearErChargeEvidence() {
    var input = document.getElementById('er-charge-evidence');
    if (input) input.value = '';
    var preview = document.getElementById('er-charge-preview');
    if (preview) preview.classList.add('hidden');
}

document.querySelectorAll('[data-action="close-early-return"]').forEach(function (el) {
    el.addEventListener('click', closeEarlyReturnModal);
});

async function completeWithDeposit(action) {
    var amount = null;
    var reason = '';
    var fileInput = document.getElementById('er-charge-evidence');

    if (action === 'charge') {
        amount = parseFloat(document.getElementById('er-charge-amount').value);
        reason = document.getElementById('er-charge-reason').value.trim();
        if (!amount || amount <= 0) { showToast(T["Enter a valid amount"], 'error'); return; }
        if (amount > erState.amount) { showToast(T["Amount exceeds deposit"], 'error'); return; }
        if (!reason) { showToast(T["Please provide a reason"], 'error'); return; }
        if (!fileInput || fileInput.files.length === 0) { showToast(T["Please provide a picture for proof"], 'error'); return; }
    }

    ['er-release-btn', 'er-charge-btn', 'er-charge-full-btn'].forEach(function(id) {
        var btn = document.getElementById(id);
        if (btn) btn.disabled = true;
    });

    try {
        var token = getToken();
        var formData = new FormData();
        formData.append('rentalId', erState.rentalId);
        formData.append('action', action);
        if (amount) formData.append('amount', amount);
        if (reason) formData.append('reason', reason);
        if (action === 'charge' && fileInput && fileInput.files.length > 0) {
            formData.append('evidence', fileInput.files[0]);
        }
        if (token) formData.append('__RequestVerificationToken', token);

        var response = await fetch('/Dashboard/CompleteWithDeposit', {
            method: 'POST',
            headers: { 'X-CSRF-TOKEN': token },
            body: formData
        });
        var data = await response.json();
        if (data.success) {
            showToast(data.message, 'success');
            closeEarlyReturnModal();
            // Disable the card row so owner can't re-trigger
            var card = document.querySelector('[data-rental-id="' + erState.rentalId + '"]');
            if (card) {
                card.querySelectorAll('button, a').forEach(function(el) { el.disabled = true; el.style.pointerEvents = 'none'; el.style.opacity = '0.5'; });
            }
            setTimeout(function () { location.reload(); }, 1000);
        }
        else {
            ['er-release-btn', 'er-charge-btn', 'er-charge-full-btn'].forEach(function(id) {
                var btn = document.getElementById(id);
                if (btn) btn.disabled = false;
            });
        }
    } catch (err) {
        showToast(T["Something went wrong"], 'error');
        ['er-release-btn', 'er-charge-btn', 'er-charge-full-btn'].forEach(function(id) {
            var btn = document.getElementById(id);
            if (btn) btn.disabled = false;
        });
    }
}

// === Charge Full Checkbox (Early Return) ===
function toggleErChargeFull(checked) {
    var amountInput = document.getElementById('er-charge-amount');
    if (checked) {
        amountInput.value = erState.amount;
        amountInput.readOnly = true;
        amountInput.classList.add('opacity-60');
    } else {
        amountInput.value = '';
        amountInput.readOnly = false;
        amountInput.classList.remove('opacity-60');
    }
}

// === Review Modal ===
async function openReviewModal(rentalId, itemId, reviewId) {
    var modal = document.getElementById('reviewModal');
    document.getElementById('reviewRentalId').value = rentalId;
    document.getElementById('reviewItemId').value = itemId;
    document.getElementById('reviewId').value = reviewId || '';
    document.getElementById('reviewForm').reset();
    document.getElementById('ratingValue').value = '0';
    document.querySelectorAll('.star-btn').forEach(function (btn) {
        btn.classList.remove('text-amber-400');
        btn.classList.add('star-inactive');
    });

    var isEditMode = reviewId !== null && reviewId !== '';
    document.getElementById('reviewModalTitle').textContent = isEditMode ? T["Edit Review"] : T["Leave a Review"];
    document.getElementById('reviewSubmitText').textContent = isEditMode ? T["Update Review"] : T["Submit Review"];

    if (isEditMode) {
        try {
            var response = await fetch('/api/Reviews/mine/item/' + itemId);
            if (response.ok) {
                var review = await response.json();
                document.getElementById('reviewTitle').value = review.title || '';
                document.getElementById('reviewBody').value = review.body || '';
                document.getElementById('isAnonymous').checked = review.isAnonymous || false;
                document.getElementById('ratingValue').value = review.rating;
                document.querySelectorAll('.star-btn').forEach(function (star, index) {
                    if (index < review.rating) { star.classList.remove('star-inactive'); star.classList.add('text-amber-400'); }
                    else { star.classList.remove('text-amber-400'); star.classList.add('star-inactive'); }
                });
            }
        } catch (error) { console.error('Error fetching review:', error); }
    }
    pushMobileModalContent();
    modal.classList.remove('hidden');
    setTimeout(function() { trapFocus(modal); }, 50);
}

function closeReviewModal() {
    popMobileModalContent();
    releaseFocus();
    document.getElementById('reviewModal').classList.add('hidden');
}

document.querySelectorAll('.star-btn').forEach(function (btn) {
    btn.addEventListener('click', function () {
        var rating = parseInt(this.dataset.rating);
        document.getElementById('ratingValue').value = rating;
        document.querySelectorAll('.star-btn').forEach(function (star, index) {
            if (index < rating) { star.classList.remove('star-inactive'); star.classList.add('text-amber-400'); }
            else { star.classList.remove('text-amber-400'); star.classList.add('star-inactive'); }
        });
    });
});

document.getElementById('reviewForm')?.addEventListener('submit', async function (e) {
    e.preventDefault();
    var rating = parseInt(document.getElementById('ratingValue').value);
    if (rating < 1) { alert(T["Please select a rating"]); return; }

    var reviewId = document.getElementById('reviewId').value;
    var itemId = document.getElementById('reviewItemId').value;
    var rentalId = document.getElementById('reviewRentalId').value;
    var isEditMode = reviewId !== null && reviewId !== '';

    try {
        var payload = {
            itemId: parseInt(itemId),
            rentalId: parseInt(rentalId),
            rating: rating,
            title: document.getElementById('reviewTitle').value || null,
            body: document.getElementById('reviewBody').value,
            isAnonymous: document.getElementById('isAnonymous').checked
        };
        if (isEditMode) payload.id = parseInt(reviewId);

        var response = await fetch(isEditMode ? '/api/Reviews/' + reviewId : '/api/Reviews', {
            method: isEditMode ? 'PUT' : 'POST',
            headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': getToken() },
            body: JSON.stringify(payload)
        });

        if (response.ok) { closeReviewModal(); location.reload(); }
        else { var error = await response.text(); alert(error || T["Failed to submit review"]); }
    } catch (error) {
        console.error('Error submitting review:', error);
        alert(T["An error occurred. Please try again."]);
    }
});

// === Keyboard: Escape closes topmost layer ===
document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape') {
        // Close in order: modals first, then slide-over panel
        var panelEl = document.getElementById('rental-detail-panel');
        var panelOpen = panelEl && panelEl.classList.contains('open');

        if (typeof closeEscalationWarningModal === 'function' && !document.getElementById('escalationWarningModal')?.classList.contains('hidden')) {
            closeEscalationWarningModal(); return;
        }
        if (typeof closeDisputeModal === 'function' && !document.getElementById('dispute-modal')?.classList.contains('hidden')) {
            closeDisputeModal(); return;
        }
        if (typeof closeCounterOfferModal === 'function' && !document.getElementById('counter-offer-modal')?.classList.contains('hidden')) {
            closeCounterOfferModal(); return;
        }
        if (typeof closeMaintainChargeModal === 'function' && !document.getElementById('maintain-charge-modal')?.classList.contains('hidden')) {
            closeMaintainChargeModal(); return;
        }
        if (typeof closeAddEvidenceModal === 'function' && !document.getElementById('add-evidence-modal')?.classList.contains('hidden')) {
            closeAddEvidenceModal(); return;
        }
        if (!document.getElementById('extension-modal')?.classList.contains('hidden')) { closeExtensionModal(); return; }
        if (!document.getElementById('deposit-modal')?.classList.contains('hidden')) { closeDepositModal(); return; }
        if (!document.getElementById('early-return-modal')?.classList.contains('hidden')) { closeEarlyReturnModal(); return; }
        if (!document.getElementById('reviewModal')?.classList.contains('hidden')) { closeReviewModal(); return; }

        // Close panel last
        if (panelOpen && typeof closeRentalDetail === 'function') { closeRentalDetail(); }
    }
});

// === Browser Back: close panel ===
window.addEventListener('popstate', function(e) {
    var panelEl = document.getElementById('rental-detail-panel');
    if (e.state && e.state.panel) {
        if (typeof openRentalDetail === 'function') openRentalDetail(e.state.panel, null);
    } else if (panelEl && panelEl.classList.contains('open')) {
        if (typeof closeRentalDetail === 'function') closeRentalDetail();
    }
});

// === SignalR ===
var connection = null;
try {
    connection = new signalR.HubConnectionBuilder()
        .withUrl("/rentmateHub")
        .withAutomaticReconnect()
        .build();

    connection.onreconnecting(function() {
        var dot   = document.getElementById('connectionDot');
        var label = document.getElementById('connectionLabel');
        if (dot)   { dot.className = 'w-2 h-2 rounded-full bg-amber-400'; }
        if (label) { label.textContent = T['Reconnecting...'] || 'Reconnecting...'; }
    });

    connection.onreconnected(function() {
        var dot   = document.getElementById('connectionDot');
        var label = document.getElementById('connectionLabel');
        if (dot)   { dot.className = 'w-2 h-2 rounded-full bg-emerald-500 animate-pulse'; }
        if (label) { label.textContent = T['Live'] || 'Live'; }
        // Mark lazy caches stale so next tab visit re-fetches
        tabCacheValid.history = false;
        tabCacheValid.favorites = false;
    });

    connection.onclose(function() {
        var dot   = document.getElementById('connectionDot');
        var label = document.getElementById('connectionLabel');
        if (dot)   { dot.className = 'w-2 h-2 rounded-full bg-ledger-300 dark:bg-ledger-600'; }
        if (label) { label.textContent = T['Offline'] || 'Offline'; }
    });

    connection.on("RentalRequested", function (data) {
        showToast(T["New rental request for"] + " " + data.itemTitle, "info");
        setTimeout(function() { location.reload(); }, 2000);
    });

    connection.on("RentalStatusChanged", function (data) {
        tabCacheValid.history = false;
        if (currentPanelRentalId && data.rentalId === currentPanelRentalId) {
            showPanelUpdateBanner();
        } else {
            showToast(data.message || T["Rental status updated for"] + " " + data.itemTitle, "info");
            setTimeout(function() { location.reload(); }, 2000);
        }
    });

    connection.on("ExtensionRequested", function (data) {
        if (currentPanelRentalId && data.rentalId === currentPanelRentalId) {
            showPanelUpdateBanner();
        } else {
            showToast(T["New extension request for"] + " " + data.itemTitle, "info");
            setTimeout(function() { location.reload(); }, 2000);
        }
    });

    connection.on("ExtensionStatusChanged", function (data) {
        if (currentPanelRentalId && data.rentalId === currentPanelRentalId) {
            showPanelUpdateBanner();
        } else {
            showToast(data.message || T["Extension status updated for"] + " " + data.itemTitle, "info");
            setTimeout(function() { location.reload(); }, 2000);
        }
    });

    connection.on("RentalOverdue", function (data) {
        showToast(T["Rental for"] + " " + data.itemTitle + " " + T["is overdue by"] + " " + data.daysOverdue + " " + T["days"], "warning");
        setTimeout(function() { location.reload(); }, 3000);
    });

    connection.on("DepositStatusChanged", function (data) {
        tabCacheValid.history = false;
        if (currentPanelRentalId && data.rentalId === currentPanelRentalId) {
            showPanelUpdateBanner();
        } else {
            var msg = T["Deposit status updated for"] + " " + data.itemTitle;
            if (data.status === "Escalated")    msg = T["Dispute escalated for"] + " " + data.itemTitle;
            if (data.status === "Released")     msg = T["Deposit released for"] + " " + data.itemTitle;
            if (data.status === "ChargeAccepted" || data.status === "ChargeUpheld") msg = T["Dispute resolved for"] + " " + data.itemTitle;
            if (data.status === "CounterAccepted") msg = T["Settlement reached for"] + " " + data.itemTitle;
            if (data.status === "CounterOffered")  msg = T["New counter-offer for"] + " " + data.itemTitle;
            if (data.adminNotes) msg += " - " + data.adminNotes;
            showToast(msg, "info");
            setTimeout(function() { location.reload(); }, 2500);
        }
    });

    connection.start()
        .then(function () {
            var dot   = document.getElementById('connectionDot');
            var label = document.getElementById('connectionLabel');
            if (dot)   { dot.className = 'w-2 h-2 rounded-full bg-emerald-500 animate-pulse'; }
            if (label) { label.textContent = T['Live'] || 'Live'; }
        })
        .catch(function (err) { console.error('SignalR Error:', err); });
} catch(e) {
    console.error('SignalR init failed:', e);
}

// === Slide-Over Panel ===
function openRentalDetail(rentalId, action, isRefresh) {
    var panel = document.getElementById('rental-detail-panel');
    var backdrop = document.getElementById('rental-detail-backdrop');
    var body = document.getElementById('rental-panel-body');
    if (!panel || !body) return;

    currentPanelRentalId = rentalId;

    var data = window.rentalData && window.rentalData[rentalId];

    if (data && !isRefresh) {
        // Build panel content client-side from serialized data
        window._panelEvidence = data.evidence || [];
        body.innerHTML = _buildPanelHtml(data);
        _showPanel(panel, backdrop);
        _handleDeepAction(action, data);
    } else {
        // Fetch server-rendered partial
        body.innerHTML = '<div class="flex items-center justify-center py-12"><div class="w-8 h-8 border-2 border-primary-200 border-t-primary-600 rounded-full animate-spin"></div></div>';
        _showPanel(panel, backdrop);

        fetch('/Dashboard/RentalDetailPartial?rentalId=' + rentalId)
            .then(function(r) {
                if (!r.ok) throw new Error('HTTP ' + r.status);
                return r.text();
            })
            .then(function(html) {
                body.innerHTML = html;
                _handleDeepAction(action, null);
            })
            .catch(function(err) {
                console.error('Panel fetch error:', err);
                body.innerHTML = '<div class="px-4 py-8 text-center"><p class="text-rose-600 dark:text-rose-400 text-sm">'
                    + (T['Could not load rental details'] || 'Could not load rental details.')
                    + '</p><button class="mt-2 text-sm text-primary-600 underline" onclick="openRentalDetail(' + rentalId + ')">'
                    + (T['Try again'] || 'Try again') + '</button></div>';
            });
    }

    // Browser history integration
    if (!isRefresh) {
        history.pushState({ panel: rentalId }, '', '#panel-' + rentalId);
    }
}

function _showPanel(panel, backdrop) {
    backdrop.classList.remove('hidden');
    panel.classList.add('open');
    document.body.style.overflow = 'hidden';
    // Focus close button after transition
    setTimeout(function() {
        var closeBtn = panel.querySelector('#panel-close-btn');
        if (closeBtn) closeBtn.focus();
        trapFocus(panel);
    }, 210);
}

function closeRentalDetail() {
    var panel = document.getElementById('rental-detail-panel');
    var backdrop = document.getElementById('rental-detail-backdrop');
    if (!panel) return;

    panel.classList.remove('open');
    if (backdrop) backdrop.classList.add('hidden');
    document.body.style.overflow = '';
    currentPanelRentalId = null;
    releaseFocus();

    // Return focus to the element that opened the panel
    if (lastClickedRentalDetailsLink) {
        lastClickedRentalDetailsLink.focus();
        lastClickedRentalDetailsLink = null;
    }

    // Clean URL hash
    var currentHash = window.location.hash;
    if (currentHash.startsWith('#panel-')) {
        history.replaceState({}, '', window.location.pathname + (window.location.search || ''));
    }
}

function _handleDeepAction(action, data) {
    if (!action) return;
    setTimeout(function() {
        if (action === 'dispute' && typeof openDisputeModal === 'function') {
            openDisputeModal(currentPanelRentalId);
        } else if (action === 'extension' && typeof openExtensionModal === 'function' && data) {
            openExtensionModal(data.id, data.startDate, data.endDate, data.itemPrice, data.itemId, data.autoApproveExtensions);
        } else if (action === 'review' && typeof openReviewModal === 'function' && data) {
            openReviewModal(data.id, data.itemId, data.reviewId || null);
        }
    }, 250);
}

// === Mobile Detection ===
function _isMobile() {
    return window.innerWidth < 640;
}

// === Mobile Modal Content Stack ===
// On mobile (<640px) when the panel is open, action modals hide the panel body
// and show themselves full-screen. On modal close, the panel body is restored.
var _panelContentStack = [];

function pushMobileModalContent() {
    if (!_isMobile() || !currentPanelRentalId) return;
    var body = document.getElementById('rental-panel-body');
    if (!body) return;
    // Hide panel body so modal appears to "replace" it (modal has higher z-index)
    body.setAttribute('data-mobile-hidden', '1');
    body.style.visibility = 'hidden';
    _panelContentStack.push(true);
}

function popMobileModalContent() {
    if (_panelContentStack.length === 0) return;
    _panelContentStack.pop();
    var body = document.getElementById('rental-panel-body');
    if (!body) return;
    body.removeAttribute('data-mobile-hidden');
    body.style.visibility = '';
    // Re-trap focus in restored panel
    var panel = document.getElementById('rental-detail-panel');
    if (panel) { setTimeout(function() { trapFocus(panel); }, 50); }
}

// === Panel Section Toggle ===
function togglePanelSection(btn) {
    var content = btn.nextElementSibling;
    if (!content) return;
    var icon = btn.querySelector('.bi-chevron-right');
    if (content.classList.contains('hidden')) {
        content.classList.remove('hidden');
        if (icon) icon.style.transform = 'rotate(90deg)';
    } else {
        content.classList.add('hidden');
        if (icon) icon.style.transform = '';
    }
}

// === Focus Trapping ===
var _focusTrapEl = null;
var _focusTrapHandler = null;

function trapFocus(container) {
    releaseFocus();
    _focusTrapEl = container;
    _focusTrapHandler = function(e) {
        if (e.key !== 'Tab') return;
        var focusable = container.querySelectorAll('a[href], button:not([disabled]), input:not([disabled]), textarea:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])');
        if (focusable.length === 0) return;
        var first = focusable[0];
        var last = focusable[focusable.length - 1];
        if (e.shiftKey && document.activeElement === first) {
            e.preventDefault();
            last.focus();
        } else if (!e.shiftKey && document.activeElement === last) {
            e.preventDefault();
            first.focus();
        }
    };
    document.addEventListener('keydown', _focusTrapHandler);
}

function releaseFocus() {
    if (_focusTrapHandler) {
        document.removeEventListener('keydown', _focusTrapHandler);
        _focusTrapHandler = null;
    }
    _focusTrapEl = null;
}

// === Build Panel HTML from client-side data ===
function _buildPanelHtml(d) {
    var lang = document.documentElement.lang || 'en';
    var isOverdue = d.status === 'Active' && new Date(d.endDate + 'T23:59:59') < new Date();
    var now = new Date();
    var endDate = new Date(d.endDate + 'T23:59:59');
    var daysRemaining = Math.ceil((endDate - now) / 86400000);

    var statusBadge = _badgeHtml(_statusBadgeType(d.status, isOverdue), _statusText(d.status, isOverdue));

    var thumbUrl = d.itemImage ? d.itemImage.replace('/upload/', '/upload/c_thumb,w_128,h_128,q_auto/') : '';

    var html = '';

    // Header
    html += '<div class="flex items-center gap-3 px-4 py-4 border-b border-ledger-100 dark:border-ledger-800 shrink-0">';
    html += '<div class="w-16 h-16 rounded-lg overflow-hidden bg-ledger-100 dark:bg-ledger-700 shrink-0">';
    if (thumbUrl) {
        html += '<img src="' + _esc(thumbUrl) + '" alt="' + _esc(d.itemTitle) + '" class="w-full h-full object-cover" />';
    } else {
        html += '<div class="w-full h-full flex items-center justify-center"><i class="bi bi-image text-ledger-300 dark:text-ledger-600 text-2xl"></i></div>';
    }
    html += '</div>';
    html += '<div class="flex-1 min-w-0"><p class="font-medium text-sm text-ledger-900 dark:text-white truncate">' + _esc(d.itemTitle) + '</p>' + statusBadge + '</div>';
    html += '<button type="button" onclick="closeRentalDetail()" class="p-2 rounded-lg text-ledger-400 hover:bg-ledger-100 dark:hover:bg-ledger-700 hover:text-ledger-600 dark:hover:text-ledger-300 transition-colors" aria-label="' + (T['Close'] || 'Close') + '" id="panel-close-btn">';
    html += '<svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" /></svg></button></div>';

    // Section 1: Rental Info
    html += '<div class="px-4 py-4 border-b border-ledger-100 dark:border-ledger-800">';
    html += '<h3 class="text-xs font-semibold text-ledger-400 dark:text-ledger-500 uppercase tracking-wider mb-3">' + (T['Rental Info'] || 'Rental Info') + '</h3>';
    html += '<div class="space-y-2 text-sm">';
    html += _infoRow(T['Parties'] || 'Parties', '<i class="bi bi-person text-ledger-300 dark:text-ledger-600 mr-1"></i>' + _esc(d.counterpartyName) + ' <span class="text-xs text-ledger-400 ml-1">(' + (d.role === 'renter' ? (T['Owner'] || 'Owner') : (T['Renter'] || 'Renter')) + ')</span>');
    html += _infoRow(T['Period'] || 'Period', '<span class="tabular-nums">' + _formatDateRange(d.startDate, d.endDate) + '</span>');
    html += _infoRow(T['Daily Rate'] || 'Daily Rate', '<span class="tabular-nums">' + formatCurrency(d.itemPrice) + '/' + (T['day'] || 'day') + '</span>');
    if (d.accessories && d.accessories.length > 0) {
        var accHtml = d.accessories.map(function(a) { return '<span class="block text-xs">' + _esc(a.name) + ' (+' + formatCurrency(a.dailyPrice) + '/' + (T['day'] || 'day') + ')</span>'; }).join('');
        html += _infoRow(T['Accessories'] || 'Accessories', accHtml);
    }
    if (d.depositAmount > 0) {
        html += _infoRow(T['Deposit'] || 'Deposit', '<span class="tabular-nums">' + formatCurrency(d.depositAmount) + '</span>');
    }
    html += '<div class="flex justify-between font-medium border-t border-ledger-100 dark:border-ledger-800 pt-2"><span class="text-ledger-700 dark:text-ledger-300">' + (T['Total'] || 'Total') + '</span><span class="text-ledger-900 dark:text-white tabular-nums">' + formatCurrency(d.totalPrice) + '</span></div>';
    html += '</div></div>';

    // Section 2: Countdown (only if active)
    if (d.status === 'Active') {
        html += '<div class="px-4 py-4 border-b border-ledger-100 dark:border-ledger-800">';
        html += _buildCountdownHtml(daysRemaining, d.endDate);
        html += '</div>';
    }

    // Section 3: Deposit Status
    if (d.depositStatus && d.depositAmount > 0) {
        html += _buildDepositSectionHtml(d);
    }

    // Section 4: Extensions
    if (d.status === 'Active' || (d.extensions && d.extensions.length > 0)) {
        html += _buildExtensionSectionHtml(d);
    }

    // Section 5: Evidence
    if (d.evidence && d.evidence.length > 0) {
        html += _buildEvidenceSectionHtml(d);
    }

    // Section 6: Actions Footer
    html += _buildActionsFooterHtml(d, isOverdue);

    return html;
}

// === Helper functions for panel HTML building ===
function _esc(str) {
    if (!str) return '';
    var div = document.createElement('div');
    div.textContent = str;
    return div.innerHTML;
}

function _infoRow(label, valueHtml) {
    return '<div class="flex justify-between"><span class="text-ledger-500 dark:text-ledger-400">' + label + '</span><span class="text-ledger-900 dark:text-white">' + valueHtml + '</span></div>';
}

function _formatDateRange(a, b) {
    var dA = new Date(a + 'T00:00:00');
    var dB = new Date(b + 'T00:00:00');
    var first = dA <= dB ? dA : dB;
    var second = dA <= dB ? dB : dA;
    var lang = document.documentElement.lang || 'en';
    return first.toLocaleDateString(lang, { day: 'numeric', month: 'short' })
         + ' → '
         + second.toLocaleDateString(lang, { day: 'numeric', month: 'short', year: 'numeric' });
}

function _statusBadgeType(status, isOverdue) {
    if (status === 'Active' && isOverdue) return 'overdue';
    var map = { Active: 'active', Pending: 'pending', Accepted: 'warning', Completed: 'completed', Cancelled: 'cancelled' };
    return map[status] || 'default';
}

function _statusText(status, isOverdue) {
    if (status === 'Active' && isOverdue) return T['Overdue'] || 'Overdue';
    var map = { Active: T['Active'] || 'Active', Pending: T['Pending'] || 'Pending', Accepted: T['Accepted'] || 'Accepted', Completed: T['Completed'] || 'Completed', Cancelled: T['Cancelled'] || 'Cancelled' };
    return map[status] || status;
}

function _badgeHtml(type, text) {
    var styles = {
        active:    'bg-emerald-50 dark:bg-emerald-900/10 text-emerald-700 dark:text-emerald-400',
        pending:   'bg-amber-50 dark:bg-amber-900/10 text-amber-700 dark:text-amber-400',
        warning:   'bg-clay-50 dark:bg-clay-900/10 text-clay-700 dark:text-clay-400',
        completed: 'bg-ledger-100 dark:bg-ledger-800 text-ledger-600 dark:text-ledger-400',
        cancelled: 'bg-red-50 dark:bg-red-900/10 text-red-600 dark:text-red-400',
        overdue:   'bg-clay-50 dark:bg-clay-900/10 text-clay-700 dark:text-clay-400',
        'deposit-authorized': 'bg-sky-50 dark:bg-sky-900/10 text-sky-700 dark:text-sky-400',
        'deposit-released':   'bg-emerald-50 dark:bg-emerald-900/10 text-emerald-700 dark:text-emerald-400',
        'deposit-charged':    'bg-red-50 dark:bg-red-900/10 text-red-600 dark:text-red-400',
        'extension-pending':  'bg-sky-50 dark:bg-sky-900/10 text-sky-700 dark:text-sky-400'
    };
    var cls = styles[type] || 'bg-ledger-100 dark:bg-ledger-800 text-ledger-600 dark:text-ledger-400';
    return '<span class="px-2 py-0.5 ' + cls + ' text-xs font-medium rounded-full">' + _esc(text) + '</span>';
}

function _buildCountdownHtml(daysRemaining, endDate) {
    var urgency = daysRemaining <= 0 ? 'overdue' : daysRemaining <= 1 ? 'urgent' : daysRemaining <= 3 ? 'warning' : 'normal';
    var colors = { overdue: 'text-rose-600 dark:text-rose-400', urgent: 'text-clay-600 dark:text-clay-400', warning: 'text-amber-600 dark:text-amber-400', normal: 'text-primary-600 dark:text-primary-400' };
    var bgColors = { overdue: 'bg-rose-50 dark:bg-rose-900/10', urgent: 'bg-clay-50 dark:bg-clay-900/10', warning: 'bg-amber-50 dark:bg-amber-900/10', normal: 'bg-primary-50 dark:bg-primary-900/10' };
    var label = daysRemaining <= 0
        ? (T['Overdue by'] || 'Overdue by') + ' ' + Math.abs(daysRemaining) + ' ' + (T['days'] || 'days')
        : daysRemaining + ' ' + (T['days remaining'] || 'days remaining');

    return '<div class="' + bgColors[urgency] + ' rounded-lg p-3 text-center">'
        + '<p class="text-2xl font-bold tabular-nums ' + colors[urgency] + '">' + (daysRemaining <= 0 ? Math.abs(daysRemaining) : daysRemaining) + '</p>'
        + '<p class="text-xs ' + colors[urgency] + '">' + label + '</p>'
        + '</div>';
}

function _buildDepositSectionHtml(d) {
    var depositInDispute = d.depositStatus === 'Disputed' || d.depositStatus === 'CounterOffered' || d.depositStatus === 'Escalated';
    var depIsResolved = d.depositStatus === 'Released' || d.depositStatus === 'ChargeUpheld';

    var html = '<div class="panel-section border-b border-ledger-100 dark:border-ledger-800" data-section="deposit">';
    html += '<div class="px-4 py-4">';
    html += '<h3 class="text-xs font-semibold text-ledger-400 dark:text-ledger-500 uppercase tracking-wider mb-3">' + (T['Deposit Status'] || 'Deposit Status') + '</h3>';

    // Deposit status badge
    var depBadgeType = d.depositStatus === 'Authorized' ? 'deposit-authorized'
        : d.depositStatus === 'Released' ? 'deposit-released'
        : (d.depositStatus === 'Charged' || d.depositStatus === 'PartiallyCharged') ? 'deposit-charged'
        : depositInDispute ? 'warning' : 'deposit-authorized';
    html += '<div class="mb-3">' + _badgeHtml(depBadgeType, T[d.depositStatus] || d.depositStatus) + ' <span class="text-sm text-ledger-500 dark:text-ledger-400 tabular-nums ml-1">' + formatCurrency(d.depositAmount) + '</span></div>';

    // Charge details
    if (d.depositChargedAmount) {
        html += '<div class="p-3 bg-ledger-50 dark:bg-ledger-900/50 rounded-lg text-sm space-y-1 mb-3">';
        html += '<div class="flex justify-between"><span class="text-ledger-500 dark:text-ledger-400">' + (T['Charged Amount'] || 'Charged Amount') + '</span><span class="font-medium text-ledger-900 dark:text-white tabular-nums">' + formatCurrency(d.depositChargedAmount) + '</span></div>';
        if (d.depositChargeReason) {
            html += '<div><span class="text-ledger-500 dark:text-ledger-400">' + (T['Reason'] || 'Reason') + ':</span> <span class="text-ledger-700 dark:text-ledger-300">' + _esc(d.depositChargeReason) + '</span></div>';
        }
        html += '</div>';
    }

    // Dispute reason
    if (d.depositDisputeReason) {
        html += '<div class="p-3 bg-rose-50 dark:bg-rose-900/10 rounded-lg text-sm mb-3">';
        html += '<span class="text-rose-700 dark:text-rose-400 font-medium">' + (T['Dispute Reason'] || 'Dispute Reason') + ':</span> ';
        html += '<span class="text-rose-600 dark:text-rose-300">' + _esc(d.depositDisputeReason) + '</span></div>';
    }

    // Counter offer
    if (d.depositCounterOfferAmount) {
        html += '<div class="p-3 bg-amber-50 dark:bg-amber-900/10 rounded-lg text-sm space-y-1 mb-3">';
        html += '<div class="flex justify-between"><span class="text-amber-700 dark:text-amber-400">' + (T['Counter-Offer'] || 'Counter-Offer') + '</span>';
        html += '<span class="font-medium text-amber-900 dark:text-amber-200 tabular-nums">' + formatCurrency(d.depositCounterOfferAmount) + '</span></div>';
        if (d.depositOwnerResponse) {
            html += '<div class="text-amber-600 dark:text-amber-300">' + _esc(d.depositOwnerResponse) + '</div>';
        }
        html += '</div>';
    }

    html += '</div></div>';
    return html;
}

function _buildExtensionSectionHtml(d) {
    var html = '<div class="panel-section border-b border-ledger-100 dark:border-ledger-800" data-section="extension">';
    html += '<div class="px-4 py-4">';
    html += '<h3 class="text-xs font-semibold text-ledger-400 dark:text-ledger-500 uppercase tracking-wider mb-3">' + (T['Extensions'] || 'Extensions') + '</h3>';

    if (d.extensions && d.extensions.length > 0) {
        d.extensions.forEach(function(ext) {
            var extDays = Math.ceil((new Date(ext.newEndDate + 'T00:00:00') - new Date(ext.originalEndDate + 'T00:00:00')) / 86400000);
            var extStatusText = ext.status === 'Pending' ? (T['Pending'] || 'Pending')
                : ext.status === 'Accepted' ? (T['Approved'] || 'Approved')
                : (T['Auto-Approved'] || 'Auto-Approved');
            var extBadgeType = ext.status === 'Pending' ? 'pending' : 'active';
            var endDateFmt = new Date(ext.newEndDate + 'T00:00:00').toLocaleDateString(document.documentElement.lang || 'en', { day: 'numeric', month: 'short', year: 'numeric' });

            html += '<div class="p-3 bg-sky-50 dark:bg-sky-900/10 rounded-lg text-sm space-y-2 mb-2">';
            html += '<div class="flex justify-between items-center">';
            html += '<span class="text-sky-700 dark:text-sky-400 font-medium">' + (T['Active Extension'] || 'Extension') + '</span>';
            html += _badgeHtml(extBadgeType, extStatusText);
            html += '</div>';
            html += '<div class="flex justify-between text-sky-600 dark:text-sky-300"><span>+' + extDays + ' ' + (T['days'] || 'days') + ' → ' + endDateFmt + '</span><span class="tabular-nums">' + formatCurrency(ext.additionalCost) + '</span></div>';

            // Owner: approve/decline pending
            if (d.role === 'owner' && ext.status === 'Pending') {
                html += '<div class="flex gap-2 mt-2">';
                html += '<button type="button" onclick="approveExtension(' + ext.id + ')" class="flex-1 px-3 py-1.5 text-xs font-medium bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg transition-colors">' + (T['Approve'] || 'Approve') + '</button>';
                html += '<button type="button" onclick="declineExtension(' + ext.id + ')" class="flex-1 px-3 py-1.5 text-xs font-medium border border-ledger-300 dark:border-neutral-600 text-ledger-600 dark:text-ledger-400 rounded-lg hover:bg-ledger-50 dark:hover:bg-neutral-700 transition-colors">' + (T['Decline'] || 'Decline') + '</button>';
                html += '</div>';
            }
            // Renter: cancel pending
            if (d.role === 'renter' && ext.status === 'Pending') {
                html += '<div class="mt-2"><button type="button" onclick="cancelExtension(' + ext.id + ')" class="px-3 py-1.5 text-xs font-medium border border-rose-300 dark:border-rose-700 text-rose-600 dark:text-rose-400 rounded-lg hover:bg-rose-50 dark:hover:bg-rose-900/20 transition-colors">' + (T['Cancel Extension'] || 'Cancel Extension') + '</button></div>';
            }
            // Renter: pay approved extension
            if (d.role === 'renter' && (ext.status === 'Accepted' || ext.status === 'AutoApproved')) {
                html += '<div class="mt-2"><a href="/Payment/ExtensionCheckout?extensionId=' + ext.id + '" class="inline-block px-3 py-1.5 text-xs font-medium bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg transition-colors">' + (T['Pay Extension'] || 'Pay Extension') + '</a></div>';
            }

            html += '</div>';
        });
    } else if (d.role === 'renter' && d.status === 'Active') {
        html += '<button type="button" onclick="openExtensionModal(' + d.id + ', \'' + d.startDate + '\', \'' + d.endDate + '\', ' + d.itemPrice + ', ' + d.itemId + ', ' + d.autoApproveExtensions + ')" class="w-full px-3 py-2 text-sm font-medium bg-primary-600 hover:bg-primary-700 text-white rounded-lg transition-colors">';
        html += '<i class="bi bi-calendar-plus mr-1"></i>' + (T['Request Extension'] || 'Request Extension') + '</button>';
    } else {
        html += '<p class="text-sm text-ledger-400 dark:text-ledger-500">' + (T['No extensions'] || 'No extensions') + '</p>';
    }

    html += '</div></div>';
    return html;
}

function _buildEvidenceSectionHtml(d) {
    var ownerEvidence = d.evidence.filter(function(e) { return e.isOwner; });
    var renterEvidence = d.evidence.filter(function(e) { return !e.isOwner; });
    var depositInDispute = d.depositStatus === 'Disputed' || d.depositStatus === 'CounterOffered' || d.depositStatus === 'Escalated';

    var html = '<div class="panel-section border-b border-ledger-100 dark:border-ledger-800" data-section="evidence">';
    html += '<div class="px-4 py-4">';
    html += '<h3 class="text-xs font-semibold text-ledger-400 dark:text-ledger-500 uppercase tracking-wider mb-3">' + (T['Evidence'] || 'Evidence') + '</h3>';

    if (ownerEvidence.length > 0) {
        html += '<p class="text-xs font-medium text-ledger-500 dark:text-ledger-400 mb-2">' + (T["Owner's Evidence"] || "Owner's Evidence") + '</p>';
        html += '<div class="grid grid-cols-3 gap-2 mb-4">';
        ownerEvidence.forEach(function(ev) {
            html += '<div class="relative group"><img src="' + _esc(ev.imageUrl) + '" alt="' + (T['Evidence'] || 'Evidence') + '" class="w-full aspect-square object-cover rounded-lg cursor-pointer" onclick="window.open(\'' + _esc(ev.imageUrl) + '\', \'_blank\')" />';
            if (ev.notes) {
                html += '<div class="absolute bottom-0 left-0 right-0 bg-black/60 text-white text-[10px] px-1.5 py-1 rounded-b-lg opacity-0 group-hover:opacity-100 transition-opacity truncate">' + _esc(ev.notes) + '</div>';
            }
            html += '</div>';
        });
        html += '</div>';
    }

    if (renterEvidence.length > 0) {
        html += '<p class="text-xs font-medium text-ledger-500 dark:text-ledger-400 mb-2">' + (T["Renter's Evidence"] || "Renter's Evidence") + '</p>';
        html += '<div class="grid grid-cols-3 gap-2 mb-4">';
        renterEvidence.forEach(function(ev) {
            html += '<div class="relative group"><img src="' + _esc(ev.imageUrl) + '" alt="' + (T['Evidence'] || 'Evidence') + '" class="w-full aspect-square object-cover rounded-lg cursor-pointer" onclick="window.open(\'' + _esc(ev.imageUrl) + '\', \'_blank\')" />';
            if (ev.notes) {
                html += '<div class="absolute bottom-0 left-0 right-0 bg-black/60 text-white text-[10px] px-1.5 py-1 rounded-b-lg opacity-0 group-hover:opacity-100 transition-opacity truncate">' + _esc(ev.notes) + '</div>';
            }
            html += '</div>';
        });
        html += '</div>';
    }

    if (depositInDispute) {
        html += '<button type="button" onclick="openAddEvidenceModal(' + d.id + ')" class="w-full mt-2 px-3 py-2 text-sm font-medium border border-ledger-300 dark:border-neutral-600 text-ledger-600 dark:text-ledger-400 rounded-lg hover:bg-ledger-50 dark:hover:bg-neutral-700 transition-colors">';
        html += '<i class="bi bi-camera mr-1"></i>' + (T['Add Evidence'] || 'Add Evidence') + '</button>';
    }

    html += '</div></div>';
    return html;
}

function _buildActionsFooterHtml(d, isOverdue) {
    var depositInDispute = d.depositStatus === 'Disputed' || d.depositStatus === 'CounterOffered' || d.depositStatus === 'Escalated';
    var html = '<div class="sticky bottom-0 bg-white dark:bg-ledger-900 border-t border-ledger-100 dark:border-ledger-800 px-4 py-3 shrink-0"><div class="flex flex-wrap gap-2">';

    if (d.role === 'renter') {
        if (d.status === 'Accepted') {
            html += '<a href="/Payment/Checkout?rentalId=' + d.id + '" class="flex-1 text-center px-4 py-2 text-sm font-medium bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg transition-colors">' + (T['Pay Now'] || 'Pay Now') + '</a>';
        }
        if (d.status === 'Pending') {
            html += '<button type="button" onclick="_postAction(\'/Rentals/CancelRental\', {rentalId: ' + d.id + '})" class="flex-1 px-4 py-2 text-sm font-medium border border-rose-300 dark:border-rose-700 text-rose-600 dark:text-rose-400 rounded-lg hover:bg-rose-50 dark:hover:bg-rose-900/20 transition-colors">' + (T['Cancel Request'] || 'Cancel Request') + '</button>';
        }
        if (d.status === 'Completed') {
            if (d.hasReview) {
                html += '<button type="button" onclick="openReviewModal(' + d.id + ', ' + d.itemId + ', ' + (d.reviewId || 'null') + ')" class="flex-1 px-4 py-2 text-sm font-medium border border-ledger-300 dark:border-neutral-600 text-ledger-500 dark:text-ledger-400 rounded-lg hover:bg-ledger-50 dark:hover:bg-neutral-700 transition-colors"><i class="bi bi-pencil mr-1"></i>' + (T['Edit Review'] || 'Edit Review') + '</button>';
            } else {
                html += '<button type="button" onclick="openReviewModal(' + d.id + ', ' + d.itemId + ', null)" class="flex-1 px-4 py-2 text-sm font-medium bg-amber-500 hover:bg-amber-600 text-white rounded-lg transition-colors"><i class="bi bi-star mr-1"></i>' + (T['Leave Review'] || 'Leave Review') + '</button>';
            }
        }
        if ((d.depositStatus === 'Charged' || d.depositStatus === 'PartiallyCharged')) {
            html += '<button type="button" onclick="openDisputeModal(' + d.id + ', window._panelEvidence || [])" class="flex-1 px-4 py-2 text-sm font-medium border border-rose-300 dark:border-rose-700 text-rose-600 dark:text-rose-400 rounded-lg hover:bg-rose-50 dark:hover:bg-rose-900/20 transition-colors"><i class="bi bi-shield-exclamation mr-1"></i>' + (T['Dispute Charge'] || 'Dispute Charge') + '</button>';
            html += '<button type="button" onclick="acceptCharge(' + d.id + ')" class="px-4 py-2 text-sm font-medium text-ledger-500 dark:text-ledger-400 hover:text-ledger-700 dark:hover:text-ledger-300 transition-colors">' + (T['Accept Charge'] || 'Accept Charge') + '</button>';
        }
        if (d.depositStatus === 'Disputed') {
            html += '<button type="button" onclick="acceptCharge(' + d.id + ')" class="flex-1 px-4 py-2 text-sm font-medium border border-ledger-300 dark:border-neutral-600 text-ledger-600 dark:text-ledger-400 rounded-lg hover:bg-ledger-50 dark:hover:bg-neutral-700 transition-colors">' + (T['Accept Charge'] || 'Accept Charge') + '</button>';
            html += '<button type="button" onclick="escalateDispute(' + d.id + ')" class="flex-1 px-4 py-2 text-sm font-medium border border-rose-300 dark:border-rose-700 text-rose-600 dark:text-rose-400 rounded-lg hover:bg-rose-50 dark:hover:bg-rose-900/20 transition-colors">' + (T['Escalate'] || 'Escalate') + '</button>';
        }
        if (d.depositStatus === 'CounterOffered') {
            html += '<button type="button" onclick="acceptCounterOffer(' + d.id + ')" class="flex-1 px-4 py-2 text-sm font-medium bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg transition-colors">' + (T['Accept Offer'] || 'Accept Offer') + '</button>';
            html += '<button type="button" onclick="escalateDispute(' + d.id + ')" class="flex-1 px-4 py-2 text-sm font-medium border border-rose-300 dark:border-rose-700 text-rose-600 dark:text-rose-400 rounded-lg hover:bg-rose-50 dark:hover:bg-rose-900/20 transition-colors">' + (T['Escalate'] || 'Escalate') + '</button>';
        }
    }

    if (d.role === 'owner') {
        if (d.status === 'Pending') {
            html += '<button type="button" onclick="_postAction(\'/Rentals/ApproveRental\', {rentalId: ' + d.id + '})" class="flex-1 px-4 py-2 text-sm font-medium bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg transition-colors">' + (T['Approve'] || 'Approve') + '</button>';
            html += '<button type="button" onclick="_postAction(\'/Rentals/CancelRental\', {rentalId: ' + d.id + '})" class="flex-1 px-4 py-2 text-sm font-medium border border-ledger-300 dark:border-neutral-600 text-ledger-600 dark:text-ledger-400 rounded-lg hover:bg-ledger-50 dark:hover:bg-neutral-700 transition-colors">' + (T['Reject'] || 'Reject') + '</button>';
        }
        if (d.status === 'Active' && d.depositStatus === 'Authorized') {
            html += '<button type="button" onclick="openDepositResolution(' + d.id + ', ' + d.depositAmount + ', \'' + d.depositStatus + '\')" class="flex-1 px-4 py-2 text-sm font-medium bg-primary-600 hover:bg-primary-700 text-white rounded-lg transition-colors">' + (T['Resolve Deposit'] || 'Resolve Deposit') + '</button>';
        }
        if (d.status === 'Active') {
            html += '<button type="button" onclick="openEarlyReturnModal(' + d.id + ', ' + (d.depositAmount || 0) + ')" class="flex-1 px-4 py-2 text-sm font-medium border border-ledger-300 dark:border-neutral-600 text-ledger-600 dark:text-ledger-400 rounded-lg hover:bg-ledger-50 dark:hover:bg-neutral-700 transition-colors">' + (T['End Early'] || 'End Early') + '</button>';
        }
        if (d.depositStatus === 'Disputed') {
            html += '<button type="button" onclick="openCounterOfferModal(' + d.id + ', ' + d.depositAmount + ', window._panelEvidence || [])" class="flex-1 px-4 py-2 text-sm font-medium bg-amber-600 hover:bg-amber-700 text-white rounded-lg transition-colors">' + (T['Counter-Offer'] || 'Counter-Offer') + '</button>';
            html += '<button type="button" onclick="openMaintainChargeModal(' + d.id + ', window._panelEvidence || [])" class="flex-1 px-4 py-2 text-sm font-medium border border-ledger-300 dark:border-neutral-600 text-ledger-600 dark:text-ledger-400 rounded-lg hover:bg-ledger-50 dark:hover:bg-neutral-700 transition-colors">' + (T['Maintain Charge'] || 'Maintain Charge') + '</button>';
            html += '<button type="button" onclick="releaseDisputedDeposit(' + d.id + ')" class="px-4 py-2 text-sm font-medium text-ledger-500 dark:text-ledger-400 hover:text-ledger-700 dark:hover:text-ledger-300 transition-colors">' + (T['Release Deposit'] || 'Release Deposit') + '</button>';
        }
    }

    html += '</div></div>';
    return html;
}

// POST helper for actions from client-rendered panel (approve/reject/cancel forms)
async function _postAction(url, data) {
    try {
        var token = getToken();
        var bodyParts = [];
        for (var key in data) bodyParts.push(key + '=' + encodeURIComponent(data[key]));
        if (token) bodyParts.push('__RequestVerificationToken=' + encodeURIComponent(token));
        var response = await fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'X-CSRF-TOKEN': token },
            body: bodyParts.join('&')
        });
        if (response.redirected) { window.location.href = response.url; return; }
        if (response.ok) { location.reload(); }
        else { showToast(T['Something went wrong'] || 'Something went wrong', 'error'); }
    } catch(err) { showToast(T['Something went wrong'] || 'Something went wrong', 'error'); }
}

// === Panel Update Banner ===
function showPanelUpdateBanner() {
    var panel = document.getElementById('rental-detail-panel');
    if (!panel) return;
    var existing = panel.querySelector('.panel-update-banner');
    if (existing) return; // already showing

    var hasUnsaved = Array.from(panel.querySelectorAll('textarea, input[type="text"], input[type="number"]'))
        .some(function(el) { return el.value && el.value.trim() !== ''; });

    var banner = document.createElement('div');
    banner.className = 'panel-update-banner bg-primary-50 dark:bg-primary-900/20 text-primary-700 dark:text-primary-300 border-b border-primary-200 dark:border-primary-800 px-4 py-2 text-sm flex items-center gap-2';
    banner.setAttribute('role', 'status');
    banner.setAttribute('aria-live', 'polite');

    if (hasUnsaved) {
        banner.innerHTML = (T['This rental was updated'] || 'This rental was updated.') + ' '
            + (T['You have unsaved input'] || 'You have unsaved input') + ' — '
            + '<button class="underline font-medium" onclick="refreshPanelContent(' + currentPanelRentalId + ');this.closest(\'.panel-update-banner\').remove()">' + (T['Refresh anyway'] || 'Refresh anyway') + '</button>'
            + ' ' + (T['or'] || 'or') + ' '
            + '<button class="underline" onclick="this.closest(\'.panel-update-banner\').remove()">' + (T['Dismiss'] || 'Dismiss') + '</button>';
    } else {
        banner.innerHTML = (T['This rental was updated'] || 'This rental was updated.') + ' '
            + '<button class="underline font-medium" onclick="refreshPanelContent(' + currentPanelRentalId + ');this.closest(\'.panel-update-banner\').remove()">' + (T['Refresh'] || 'Refresh') + '</button>';
    }

    var body = panel.querySelector('.slide-over-panel__body');
    if (body) body.prepend(banner);
    else panel.prepend(banner);
}

function refreshPanelContent(rentalId) {
    openRentalDetail(rentalId, null, true);
}
