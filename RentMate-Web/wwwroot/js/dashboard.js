// =============================================
// Dashboard JS — Utilities, Extensions, Reviews, SignalR
// =============================================

// --- Shorthand for config ---
var DC = window.DashboardConfig || {};
var S = DC.strings || {};

// === Utility Functions ===
function formatCurrency(amount) {
    var lang = document.documentElement.lang || 'sl';
    var currency = window.CurrentCurrency ? window.CurrentCurrency.Code : 'EUR';
    return new Intl.NumberFormat(lang, { style: 'currency', currency: currency }).format(amount);
}

function getToken() {
    return document.querySelector('input[name="__RequestVerificationToken"]')?.value;
}

function showToast(message, type) {
    var container = document.getElementById('toastContainer');
    var toast = document.createElement('div');
    var bgClass = type === 'success' ? 'bg-emerald-600' : type === 'error' ? 'bg-rose-600' : 'bg-trust-blue-600';
    toast.className = bgClass + ' text-white px-4 py-3 rounded-xl shadow-lg text-sm font-medium animate-fade-in';
    toast.textContent = message;
    container.appendChild(toast);
    setTimeout(function () { toast.remove(); }, 4000);
}

// === Toggle Listing ===
async function toggleListing(itemId, btn) {
    try {
        var response = await fetch(DC.urls.toggleListing + itemId, {
            method: 'POST',
            headers: { 'X-CSRF-TOKEN': getToken() }
        });
        if (response.ok) location.reload();
    } catch (error) {
        console.error('Error toggling listing:', error);
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
    document.getElementById('ext-daily-rate').textContent = formatCurrency(dailyRate) + '/' + S.day;
    document.getElementById('ext-cost-preview').classList.add('hidden');
    document.getElementById('ext-submit-btn').disabled = true;

    var notice = document.getElementById('ext-approval-notice');
    if (autoApprove) { notice.classList.add('hidden'); } else { notice.classList.remove('hidden'); }

    // Fetch booked dates for the item and configure calendar
    var userHighlight = { from: new Date(startDate + 'T00:00:00'), to: new Date(currentEndDate + 'T00:00:00') };
    function toLocalISODate(d) {
        return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0');
    }
    var minDateForSelection = new Date(extState.currentEndDate);
    minDateForSelection.setDate(minDateForSelection.getDate() + 1);
    var minDateStr = toLocalISODate(minDateForSelection);
    var calendarMinStr = startDate;

    fetch(DC.urls.getBookedDates + '?itemId=' + itemId)
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
    modal.classList.remove('hidden');
    modal.classList.add('flex');
    document.body.style.overflow = 'hidden';
}

function closeExtensionModal() {
    var modal = document.getElementById('extension-modal');
    modal.classList.add('hidden');
    modal.classList.remove('flex');
    document.body.style.overflow = '';
}

// Listen for calendar date selection
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
    btn.textContent = S.submitting;

    var formData = new FormData(this);
    try {
        var response = await fetch(DC.urls.requestExtension, {
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
            btn.textContent = S.requestExtension;
        }
    } catch (err) {
        showToast(S.somethingWentWrong, 'error');
        btn.disabled = false;
        btn.textContent = S.requestExtension;
    }
});

// === Extension Approval/Decline ===
async function approveExtension(extensionId) {
    try {
        var token = getToken();
        var response = await fetch(DC.urls.approveExtension, {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'X-CSRF-TOKEN': token },
            body: 'extensionId=' + extensionId + (token ? '&__RequestVerificationToken=' + encodeURIComponent(token) : '')
        });
        var data = await response.json();
        showToast(data.message, data.success ? 'success' : 'error');
        if (data.success) setTimeout(function () { location.reload(); }, 1000);
    } catch (err) { showToast(S.somethingWentWrong, 'error'); }
}

async function declineExtension(extensionId) {
    try {
        var token = getToken();
        var response = await fetch(DC.urls.declineExtension, {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'X-CSRF-TOKEN': token },
            body: 'extensionId=' + extensionId + (token ? '&__RequestVerificationToken=' + encodeURIComponent(token) : '')
        });
        var data = await response.json();
        showToast(data.message, data.success ? 'success' : 'error');
        if (data.success) setTimeout(function () { location.reload(); }, 1000);
    } catch (err) { showToast(S.somethingWentWrong, 'error'); }
}

async function cancelExtension(extensionId) {
    try {
        var token = getToken();
        var response = await fetch(DC.urls.cancelExtension, {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'X-CSRF-TOKEN': token },
            body: 'extensionId=' + extensionId + (token ? '&__RequestVerificationToken=' + encodeURIComponent(token) : '')
        });
        var data = await response.json();
        showToast(data.message, data.success ? 'success' : 'error');
        if (data.success) setTimeout(function () { location.reload(); }, 1000);
    } catch (err) { showToast(S.somethingWentWrong, 'error'); }
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
        btn.classList.add('text-slate-300');
    });

    var isEditMode = reviewId !== null && reviewId !== '';
    document.getElementById('reviewModalTitle').textContent = isEditMode ? S.editReview : S.leaveReview;
    document.getElementById('reviewSubmitText').textContent = isEditMode ? S.updateReview : S.submitReview;

    if (isEditMode) {
        try {
            var response = await fetch(DC.urls.reviewsMineItem + itemId);
            if (response.ok) {
                var review = await response.json();
                document.getElementById('reviewTitle').value = review.title || '';
                document.getElementById('reviewBody').value = review.body || '';
                document.getElementById('isAnonymous').checked = review.isAnonymous || false;
                document.getElementById('ratingValue').value = review.rating;
                document.querySelectorAll('.star-btn').forEach(function (star, index) {
                    if (index < review.rating) { star.classList.remove('text-slate-300'); star.classList.add('text-amber-400'); }
                    else { star.classList.remove('text-amber-400'); star.classList.add('text-slate-300'); }
                });
            }
        } catch (error) { console.error('Error fetching review:', error); }
    }
    modal.classList.remove('hidden');
}

function closeReviewModal() { document.getElementById('reviewModal').classList.add('hidden'); }

document.querySelectorAll('.star-btn').forEach(function (btn) {
    btn.addEventListener('click', function () {
        var rating = parseInt(this.dataset.rating);
        document.getElementById('ratingValue').value = rating;
        document.querySelectorAll('.star-btn').forEach(function (star, index) {
            if (index < rating) { star.classList.remove('text-slate-300'); star.classList.add('text-amber-400'); }
            else { star.classList.remove('text-amber-400'); star.classList.add('text-slate-300'); }
        });
    });
});

document.getElementById('reviewForm')?.addEventListener('submit', async function (e) {
    e.preventDefault();
    var rating = parseInt(document.getElementById('ratingValue').value);
    if (rating < 1) { alert(S.pleaseSelectRating); return; }

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

        var response = await fetch(isEditMode ? DC.urls.reviewSubmit + reviewId : DC.urls.reviewSubmit, {
            method: isEditMode ? 'PUT' : 'POST',
            headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': getToken() },
            body: JSON.stringify(payload)
        });

        if (response.ok) { closeReviewModal(); location.reload(); }
        else { var error = await response.text(); alert(error || S.failedToSubmitReview); }
    } catch (error) {
        console.error('Error submitting review:', error);
        alert(S.anErrorOccurred);
    }
});

// === Keyboard: Escape closes modals ===
document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape') {
        closeExtensionModal();
        if (typeof closeDepositModal === 'function') closeDepositModal();
        if (typeof closeEarlyReturnModal === 'function') closeEarlyReturnModal();
        closeReviewModal();
    }
});

// === SignalR ===
var connection = new signalR.HubConnectionBuilder()
    .withUrl(DC.urls.signalRHub)
    .withAutomaticReconnect()
    .build();

connection.on("RentalRequested", function (data) {
    showToast(S.newRentalRequest + " " + data.itemTitle, "info");
    setTimeout(function() { location.reload(); }, 2000);
});

connection.on("RentalStatusChanged", function (data) {
    showToast(data.message || S.rentalStatusUpdated + " " + data.itemTitle, "info");
    setTimeout(function() { location.reload(); }, 2000);
});

connection.on("ExtensionRequested", function (data) {
    showToast(S.newExtensionRequest + " " + data.itemTitle, "info");
    setTimeout(function() { location.reload(); }, 2000);
});

connection.on("ExtensionStatusChanged", function (data) {
    showToast(data.message || S.extensionStatusUpdated + " " + data.itemTitle, "info");
    setTimeout(function() { location.reload(); }, 2000);
});

connection.on("RentalOverdue", function (data) {
    showToast(S.rentalFor + " " + data.itemTitle + " " + S.isOverdueBy + " " + data.daysOverdue + " " + S.days, "warning");
    setTimeout(function() { location.reload(); }, 3000);
});

connection.on("DepositStatusChanged", function (data) {
    var msg = S.depositStatusUpdated + " " + data.itemTitle;
    if (data.status === "Escalated") msg = S.disputeEscalated + " " + data.itemTitle;
    if (data.status === "Released") msg = S.depositReleased + " " + data.itemTitle;
    if (data.status === "ChargeAccepted" || data.status === "ChargeUpheld") msg = S.disputeResolved + " " + data.itemTitle;
    if (data.status === "CounterAccepted") msg = S.settlementReached + " " + data.itemTitle;
    if (data.status === "CounterOffered") msg = S.newCounterOffer + " " + data.itemTitle;
    if (data.status === "CounterRejected") msg = S.counterOfferRejected + " " + data.itemTitle;
    if (data.status === "Disputed") msg = S.depositDisputed + " " + data.itemTitle;
    if (data.status === "Charged") msg = S.depositCharged + " " + data.itemTitle;

    if (data.adminNotes) {
        msg += " - " + data.adminNotes;
    }

    showToast(msg, "info");
    setTimeout(function() { location.reload(); }, 2500);
});

connection.start()
    .then(function () {
        var indicator = document.getElementById('connectionIndicator');
        indicator.className = 'px-3 py-1.5 bg-emerald-100 dark:bg-emerald-900/30 text-emerald-700 dark:text-emerald-400 text-sm font-medium rounded-full';
        indicator.textContent = S.live;
    })
    .catch(function (err) { console.error('SignalR Error:', err); });
