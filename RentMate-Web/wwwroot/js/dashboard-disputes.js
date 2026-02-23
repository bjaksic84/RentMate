/**
 * Dashboard Dispute System
 *
 * Handles dispute modal interactions, counter-offers, evidence uploads,
 * escalation, and charge acceptance flows.
 *
 * Dependencies: dashboard.js (formatCurrency, getToken, showToast, uploadEvidence)
 */

// === Dispute Modal (Renter) ===
var disputeModalState = { rentalId: 0 };

function openDisputeModal(rentalId, evidenceJson) {
    disputeModalState.rentalId = rentalId;
    document.getElementById('dispute-rental-id').value = rentalId;
    document.getElementById('dispute-reason').value = '';

    renderModalEvidence('dispute-existing-evidence', evidenceJson);

    var modal = document.getElementById('dispute-modal');
    if (typeof pushMobileModalContent === 'function') pushMobileModalContent();
    modal.classList.remove('hidden');
    modal.classList.add('flex');
    document.body.style.overflow = 'hidden';
    if (typeof trapFocus === 'function') setTimeout(function() { trapFocus(modal); }, 50);
    clearDisputeEvidence();
}

function clearDisputeEvidence() {
    document.getElementById('dispute-evidence-file').value = '';
    document.getElementById('dispute-evidence-preview').classList.add('hidden');
}

function closeDisputeModal() {
    if (typeof popMobileModalContent === 'function') popMobileModalContent();
    if (typeof releaseFocus === 'function') releaseFocus();
    var modal = document.getElementById('dispute-modal');
    modal.classList.add('hidden');
    modal.classList.remove('flex');
    document.body.style.overflow = '';
}

async function submitDispute() {
    var reason = document.getElementById('dispute-reason').value.trim();
    if (!reason) { showToast(T["Please provide a reason"], 'error'); return; }

    var btn = document.getElementById('dispute-submit-btn');
    btn.disabled = true;
    try {
        var token = getToken();
        var response = await fetch('/Dashboard/DisputeDeposit', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'X-CSRF-TOKEN': token },
            body: 'rentalId=' + disputeModalState.rentalId + '&reason=' + encodeURIComponent(reason) + (token ? '&__RequestVerificationToken=' + encodeURIComponent(token) : '')
        });
        var data = await response.json();
        if (data.success) {
            var fileInput = document.getElementById('dispute-evidence-file');
            if (fileInput.files.length > 0) {
                var uploadResult = await uploadEvidence(disputeModalState.rentalId, fileInput.files[0], reason);
                if (!uploadResult.success) {
                    showToast(T["Dispute submitted, but evidence upload failed."], 'warning');
                }
            }
            showToast(data.message, 'success');
            closeDisputeModal();
            setTimeout(function () { location.reload(); }, 1000);
        } else {
            showToast(data.message, 'error');
            btn.disabled = false;
        }
    } catch (err) { showToast(T["Something went wrong"], 'error'); btn.disabled = false; }
}

// === Counter-Offer Modal (Owner) ===
var counterOfferModalState = { rentalId: 0, originalAmount: 0 };

function openCounterOfferModal(rentalId, currentAmount, evidenceJson) {
    counterOfferModalState.rentalId = rentalId;
    counterOfferModalState.originalAmount = currentAmount;

    document.getElementById('co-rental-id').value = rentalId;
    document.getElementById('co-amount').value = '';
    document.getElementById('co-reason').value = '';
    document.getElementById('co-max-hint').textContent = T["Maximum"] + ': ' + formatCurrency(currentAmount);

    renderModalEvidence('co-existing-evidence', evidenceJson);

    var modal = document.getElementById('counter-offer-modal');
    if (typeof pushMobileModalContent === 'function') pushMobileModalContent();
    modal.classList.remove('hidden');
    modal.classList.add('flex');
    document.body.style.overflow = 'hidden';
    if (typeof trapFocus === 'function') setTimeout(function() { trapFocus(modal); }, 50);
    clearCoEvidence();
}

function clearCoEvidence() {
    document.getElementById('co-evidence-file').value = '';
    document.getElementById('co-evidence-preview').classList.add('hidden');
}

function closeCounterOfferModal() {
    if (typeof popMobileModalContent === 'function') popMobileModalContent();
    if (typeof releaseFocus === 'function') releaseFocus();
    var modal = document.getElementById('counter-offer-modal');
    modal.classList.add('hidden');
    modal.classList.remove('flex');
    document.body.style.overflow = '';
}

async function submitCounterOffer() {
    var amount = parseFloat(document.getElementById('co-amount').value);
    var reason = document.getElementById('co-reason').value.trim();

    if (!amount || amount <= 0) { showToast(T["Enter a valid amount"], 'error'); return; }
    if (amount >= counterOfferModalState.originalAmount) { showToast(T["Counter-offer must be less than current charge"], 'error'); return; }
    if (!reason) { showToast(T["Please provide a reason"], 'error'); return; }

    var fileInput = document.getElementById('co-evidence-file');
    var hasFile = fileInput && fileInput.files.length > 0;

    var btn = document.getElementById('co-submit-btn');
    btn.disabled = true;
    try {
        var token = getToken();
        var formData = new FormData();
        formData.append('rentalId', counterOfferModalState.rentalId);
        formData.append('amount', amount);
        formData.append('response', reason);
        if (hasFile) formData.append('evidence', fileInput.files[0]);
        if (token) formData.append('__RequestVerificationToken', token);

        var response = await fetch('/Dashboard/CounterOfferDeposit', {
            method: 'POST',
            headers: { 'X-CSRF-TOKEN': token },
            body: formData
        });
        var data = await response.json();
        if (data.success) {
            showToast(data.message, 'success');
            closeCounterOfferModal();
            setTimeout(function () { location.reload(); }, 1000);
        } else {
            showToast(data.message, 'error');
            btn.disabled = false;
        }
    } catch (err) { showToast(T["Something went wrong"], 'error'); btn.disabled = false; }
}

// === Maintain Charge (Owner) ===
function openMaintainChargeModal(rentalId, evidenceJson) {
    document.getElementById('mc-rental-id').value = rentalId;
    document.getElementById('mc-reason').value = '';

    renderModalEvidence('mc-existing-evidence', evidenceJson);

    var modal = document.getElementById('maintain-charge-modal');
    if (typeof pushMobileModalContent === 'function') pushMobileModalContent();
    modal.classList.remove('hidden');
    modal.classList.add('flex');
    if (typeof trapFocus === 'function') setTimeout(function() { trapFocus(modal); }, 50);
}

function closeMaintainChargeModal() {
    if (typeof popMobileModalContent === 'function') popMobileModalContent();
    if (typeof releaseFocus === 'function') releaseFocus();
    document.getElementById('maintain-charge-modal').classList.add('hidden');
    document.getElementById('maintain-charge-modal').classList.remove('flex');
    clearMcEvidence();
}

function clearMcEvidence() {
    document.getElementById('mc-evidence-file').value = '';
    document.getElementById('mc-evidence-preview').classList.add('hidden');
}

async function submitMaintainCharge() {
    var rentalId = document.getElementById('mc-rental-id').value;
    var reason = document.getElementById('mc-reason').value;
    var btn = document.getElementById('mc-submit-btn');
    var fileInput = document.getElementById('mc-evidence-file');
    var hasFile = fileInput && fileInput.files.length > 0;

    if (!confirm(T["Maintain current charge and send to admin review?"])) return;

    btn.disabled = true;
    btn.innerHTML = '<i class="bi bi-arrow-repeat animate-spin mr-1"></i> Processing...';

    try {
        var token = getToken();
        var formData = new FormData();
        formData.append('rentalId', rentalId);
        formData.append('response', reason);
        if (hasFile) formData.append('evidence', fileInput.files[0]);
        if (token) formData.append('__RequestVerificationToken', token);

        var response = await fetch('/Dashboard/MaintainCharge', {
            method: 'POST',
            headers: { 'X-CSRF-TOKEN': token },
            body: formData
        });
        var data = await response.json();

        if (data.success) {
            showToast(data.message, 'success');
            closeMaintainChargeModal();
            setTimeout(function () { location.reload(); }, 1000);
        } else {
            showToast(data.message, 'error');
            btn.disabled = false;
            btn.innerHTML = T["Confirm & Send to Admin"];
        }
    } catch (err) {
        showToast(T["Something went wrong"], 'error');
        btn.disabled = false;
        btn.innerHTML = T["Confirm & Send to Admin"];
    }
}

// === Add Evidence (Global) ===
function openAddEvidenceModal(rentalId) {
    document.getElementById('ae-rental-id').value = rentalId;
    document.getElementById('ae-notes').value = '';
    clearAeEvidence();
    var modal = document.getElementById('add-evidence-modal');
    if (typeof pushMobileModalContent === 'function') pushMobileModalContent();
    modal.classList.remove('hidden');
    modal.classList.add('flex');
    document.body.style.overflow = 'hidden';
    if (typeof trapFocus === 'function') setTimeout(function() { trapFocus(modal); }, 50);
}

function closeAddEvidenceModal() {
    if (typeof popMobileModalContent === 'function') popMobileModalContent();
    if (typeof releaseFocus === 'function') releaseFocus();
    var modal = document.getElementById('add-evidence-modal');
    modal.classList.add('hidden');
    modal.classList.remove('flex');
    document.body.style.overflow = 'auto';
}

function clearAeEvidence() {
    document.getElementById('ae-evidence-file').value = '';
    document.getElementById('ae-evidence-preview').classList.add('hidden');
}

async function submitAddEvidence() {
    var rentalId = document.getElementById('ae-rental-id').value;
    var notes = document.getElementById('ae-notes').value.trim();
    var fileInput = document.getElementById('ae-evidence-file');

    if (!fileInput.files || fileInput.files.length === 0) {
        showToast(T["Please select a file."], 'warning');
        return;
    }

    var btn = document.getElementById('ae-submit-btn');
    btn.disabled = true;
    btn.innerHTML = '<i class="bi bi-hourglass-split animate-spin mr-2"></i>' + T["Uploading..."];

    try {
        var result = await uploadEvidence(rentalId, fileInput.files[0], notes);
        if (result.success) {
            showToast(T["Evidence added successfully."], 'success');
            closeAddEvidenceModal();
            setTimeout(function() { window.location.reload(); }, 1000);
        } else {
            showToast(result.message || T["Upload failed."], 'error');
        }
    } catch (err) {
        showToast(T["An unexpected error occurred."], 'error');
    } finally {
        btn.disabled = false;
        btn.innerHTML = T["Upload"];
    }
}

// === Accept Charge (Renter) ===
async function acceptCharge(rentalId) {
    if (!confirm(T["Accept this charge and close the dispute?"])) return;
    try {
        var token = getToken();
        var response = await fetch('/Dashboard/AcceptCharge', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'X-CSRF-TOKEN': token },
            body: 'rentalId=' + rentalId + (token ? '&__RequestVerificationToken=' + encodeURIComponent(token) : '')
        });
        var data = await response.json();
        showToast(data.message, data.success ? 'success' : 'error');
        if (data.success) setTimeout(function () { location.reload(); }, 1000);
    } catch (err) { showToast(T["Something went wrong"], 'error'); }
}

// === Accept Counter-Offer (Renter) ===
async function acceptCounterOffer(rentalId) {
    if (!confirm(T["Accept counter-offer and pay the settled amount?"])) return;
    try {
        var token = getToken();
        var response = await fetch('/Dashboard/AcceptCounterOffer', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'X-CSRF-TOKEN': token },
            body: 'rentalId=' + rentalId + (token ? '&__RequestVerificationToken=' + encodeURIComponent(token) : '')
        });
        var data = await response.json();
        showToast(data.message, data.success ? 'success' : 'error');
        if (data.success) setTimeout(function () { location.reload(); }, 1000);
    } catch (err) { showToast(T["Something went wrong"], 'error'); }
}

// === Release Disputed Deposit (Owner) ===
async function releaseDisputedDeposit(rentalId) {
    if (!confirm(T["Release the deposit and close the dispute?"])) return;
    try {
        var token = getToken();
        var response = await fetch('/Dashboard/ReleaseDeposit', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'X-CSRF-TOKEN': token },
            body: 'rentalId=' + rentalId + (token ? '&__RequestVerificationToken=' + encodeURIComponent(token) : '')
        });
        var data = await response.json();
        showToast(data.message, data.success ? 'success' : 'error');
        if (data.success) setTimeout(function () { location.reload(); }, 1000);
    } catch (err) { showToast(T["Something went wrong"], 'error'); }
}

// === Escalate Dispute (Renter) ===
async function _processEscalation(rentalId) {
    try {
        var token = getToken();
        var response = await fetch('/Dashboard/EscalateDispute', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'X-CSRF-TOKEN': token },
            body: 'rentalId=' + rentalId + (token ? '&__RequestVerificationToken=' + encodeURIComponent(token) : '')
        });
        var data = await response.json();
        showToast(data.message, data.success ? 'success' : 'error');
        if (data.success) setTimeout(function () { location.reload(); }, 1000);
    } catch (err) { showToast(T["Something went wrong"], 'error'); }
}

window.escalateDispute = function(rentalId) {
    showEscalationWarning(rentalId);
};

window.showEscalationWarning = function(rentalId) {
    document.getElementById('escalateRentalId').value = rentalId;
    if (typeof pushMobileModalContent === 'function') pushMobileModalContent();
    var modal = document.getElementById('escalationWarningModal');
    modal.classList.remove('hidden');
    if (typeof trapFocus === 'function') setTimeout(function() { trapFocus(modal); }, 50);
};

window.closeEscalationWarningModal = function() {
    if (typeof popMobileModalContent === 'function') popMobileModalContent();
    if (typeof releaseFocus === 'function') releaseFocus();
    document.getElementById('escalationWarningModal').classList.add('hidden');
};

// === Evidence Rendering Helper ===
function renderModalEvidence(containerId, evidenceJson) {
    var container = document.getElementById(containerId);
    if (!container) return;

    var list = container.querySelector('.evidence-container');
    if (!list) {
        list = document.createElement('div');
        list.className = 'evidence-container flex flex-wrap gap-2 mt-2';
        container.appendChild(list);
    }
    list.innerHTML = '';

    if (evidenceJson && evidenceJson.length > 0) {
        evidenceJson.forEach(function(ev) {
            var url = ev.Url || ev.url || ev.imageUrl || '';
            var notes = ev.Notes || ev.notes || '';
            var a = document.createElement('a');
            a.href = url;
            a.target = '_blank';
            a.className = 'w-12 h-12 rounded-lg overflow-hidden border border-ledger-200 dark:border-neutral-700 hover:scale-105 transition-transform';
            if (notes) a.title = notes;

            var img = document.createElement('img');
            img.src = url;
            img.className = 'w-full h-full object-cover';

            a.appendChild(img);
            list.appendChild(a);
        });
        container.classList.remove('hidden');
    } else {
        container.classList.add('hidden');
    }
}

// === Event Listeners (run after DOM ready) ===
document.querySelectorAll('[data-action="close-dispute"]').forEach(function (el) {
    el.addEventListener('click', closeDisputeModal);
});

document.getElementById('dispute-evidence-file')?.addEventListener('change', function(e) {
    var fileName = e.target.files[0]?.name;
    if (fileName) {
        document.getElementById('dispute-filename').textContent = fileName;
        document.getElementById('dispute-evidence-preview').classList.remove('hidden');
    }
});

document.getElementById('co-evidence-file')?.addEventListener('change', function(e) {
    var fileName = e.target.files[0]?.name;
    if (fileName) {
        document.getElementById('co-filename').textContent = fileName;
        document.getElementById('co-evidence-preview').classList.remove('hidden');
    }
});

document.getElementById('mc-evidence-file')?.addEventListener('change', function(e) {
    var fileName = e.target.files[0]?.name;
    if (fileName) {
        document.getElementById('mc-filename').textContent = fileName;
        document.getElementById('mc-evidence-preview').classList.remove('hidden');
    }
});

document.getElementById('ae-evidence-file')?.addEventListener('change', function(e) {
    if (e.target.files && e.target.files[0]) {
        var fileName = e.target.files[0].name;
        document.getElementById('ae-filename').textContent = fileName;
        document.getElementById('ae-evidence-preview').classList.remove('hidden');
    }
});
