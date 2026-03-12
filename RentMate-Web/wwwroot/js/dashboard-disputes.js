// =============================================
// Dashboard Disputes JS — Deposit/Dispute Modals & Actions
// =============================================

// --- Shorthand for config ---
var DC = window.DashboardConfig || {};
var S = DC.strings || {};

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
    modal.classList.remove('hidden');
    modal.classList.add('flex');
    document.body.style.overflow = 'hidden';
}

function closeDepositModal() {
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
        var response = await fetch(DC.urls.releaseDeposit, {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'X-CSRF-TOKEN': token },
            body: 'rentalId=' + depState.rentalId + (token ? '&__RequestVerificationToken=' + encodeURIComponent(token) : '')
        });
        var data = await response.json();
        showToast(data.message, data.success ? 'success' : 'error');
        if (data.success) { closeDepositModal(); setTimeout(function () { location.reload(); }, 1000); }
        else { btn.disabled = false; }
    } catch (err) { showToast(S.somethingWentWrong, 'error'); btn.disabled = false; }
}

async function releaseDisputedDeposit(rentalId) {
    if (!confirm(S.releaseDisputedConfirm)) return;
    var btn = event.target.closest('button');
    btn.disabled = true;
    try {
        var token = getToken();
        var response = await fetch(DC.urls.releaseDisputedDeposit, {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'X-CSRF-TOKEN': token },
            body: 'rentalId=' + rentalId + (token ? '&__RequestVerificationToken=' + encodeURIComponent(token) : '')
        });
        var data = await response.json();
        showToast(data.message, data.success ? 'success' : 'error');
        if (data.success) setTimeout(function () { location.reload(); }, 1000);
        else btn.disabled = false;
    } catch (err) { showToast(S.somethingWentWrong, 'error'); btn.disabled = false; }
}

async function chargeDeposit() {
    var amount = parseFloat(document.getElementById('dep-charge-amount').value);
    var reason = document.getElementById('dep-charge-reason').value.trim();
    var fileInput = document.getElementById('dep-charge-evidence');

    if (!amount || amount <= 0) { showToast(S.enterValidAmount, 'error'); return; }
    if (amount > depState.amount) { showToast(S.amountExceedsDeposit, 'error'); return; }
    if (!reason) { showToast(S.pleaseProvideReason, 'error'); return; }
    if (!fileInput || fileInput.files.length === 0) { showToast(S.pleaseProvideProof, 'error'); return; }

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

        var response = await fetch(DC.urls.chargeDeposit, {
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
    } catch (err) { showToast(S.somethingWentWrong, 'error'); btn.disabled = false; }
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
    modal.classList.remove('hidden');
    modal.classList.add('flex');
    document.body.style.overflow = 'hidden';
}

function closeEarlyReturnModal() {
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
        if (!amount || amount <= 0) { showToast(S.enterValidAmount, 'error'); return; }
        if (amount > erState.amount) { showToast(S.amountExceedsDeposit, 'error'); return; }
        if (!reason) { showToast(S.pleaseProvideReason, 'error'); return; }
        if (!fileInput || fileInput.files.length === 0) { showToast(S.pleaseProvideProof, 'error'); return; }
    }

    // Disable all buttons
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

        var response = await fetch(DC.urls.completeWithDeposit, {
            method: 'POST',
            headers: { 'X-CSRF-TOKEN': token },
            body: formData
        });
        var data = await response.json();
        if (data.success) {
            showToast(data.message, 'success');
            closeEarlyReturnModal();
            setTimeout(function () { location.reload(); }, 1000);
        }
        else {
            ['er-release-btn', 'er-charge-btn', 'er-charge-full-btn'].forEach(function(id) {
                var btn = document.getElementById(id);
                if (btn) btn.disabled = false;
            });
        }
    } catch (err) {
        showToast(S.somethingWentWrong, 'error');
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

// === Dispute Modal (Renter) ===
var disputeState = { rentalId: 0 };

function openDisputeModal(rentalId, evidenceJson) {
    disputeState.rentalId = rentalId;
    document.getElementById('dispute-rental-id').value = rentalId;
    document.getElementById('dispute-reason').value = '';

    // Populating existing evidence
    renderModalEvidence('dispute-existing-evidence', evidenceJson);

    var modal = document.getElementById('dispute-modal');
    modal.classList.remove('hidden');
    modal.classList.add('flex');
    document.body.style.overflow = 'hidden';
    clearDisputeEvidence();
}

function clearDisputeEvidence() {
    document.getElementById('dispute-evidence-file').value = '';
    document.getElementById('dispute-evidence-preview').classList.add('hidden');
}

document.getElementById('dispute-evidence-file')?.addEventListener('change', function(e) {
    var fileName = e.target.files[0]?.name;
    if (fileName) {
        document.getElementById('dispute-filename').textContent = fileName;
        document.getElementById('dispute-evidence-preview').classList.remove('hidden');
    }
});

function closeDisputeModal() {
    var modal = document.getElementById('dispute-modal');
    modal.classList.add('hidden');
    modal.classList.remove('flex');
    document.body.style.overflow = '';
}

document.querySelectorAll('[data-action="close-dispute"]').forEach(function (el) {
    el.addEventListener('click', closeDisputeModal);
});

async function submitDispute() {
    var reason = document.getElementById('dispute-reason').value.trim();
    if (!reason) { showToast(S.pleaseProvideReason, 'error'); return; }

    var btn = document.getElementById('dispute-submit-btn');
    btn.disabled = true;
    try {
        var token = getToken();
        var response = await fetch(DC.urls.disputeDeposit, {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'X-CSRF-TOKEN': token },
            body: 'rentalId=' + disputeState.rentalId + '&reason=' + encodeURIComponent(reason) + (token ? '&__RequestVerificationToken=' + encodeURIComponent(token) : '')
        });
        var data = await response.json();
        if (data.success) {
            var fileInput = document.getElementById('dispute-evidence-file');
            if (fileInput.files.length > 0) {
                var uploadResult = await uploadEvidence(disputeState.rentalId, fileInput.files[0], reason);
                if (!uploadResult.success) {
                    showToast(S.disputeSubmittedEvidenceFailed, 'warning');
                }
            }
            showToast(data.message, 'success');
            closeDisputeModal();
            setTimeout(function () { location.reload(); }, 1000);
        } else {
            showToast(data.message, 'error');
            btn.disabled = false;
        }
    } catch (err) { showToast(S.somethingWentWrong, 'error'); btn.disabled = false; }
}

// === Counter-Offer Modal (Owner) ===
var counterState = { rentalId: 0, originalAmount: 0 };

function openCounterOfferModal(rentalId, currentAmount, evidenceJson) {
    counterState.rentalId = rentalId;
    counterState.originalAmount = currentAmount;

    document.getElementById('co-rental-id').value = rentalId;
    document.getElementById('co-amount').value = '';
    document.getElementById('co-reason').value = '';
    document.getElementById('co-max-hint').textContent = S.maximum + ': ' + formatCurrency(currentAmount);

    // Populating existing evidence
    renderModalEvidence('co-existing-evidence', evidenceJson);

    var modal = document.getElementById('counter-offer-modal');
    modal.classList.remove('hidden');
    modal.classList.add('flex');
    document.body.style.overflow = 'hidden';
    clearCoEvidence();
}

function clearCoEvidence() {
    document.getElementById('co-evidence-file').value = '';
    document.getElementById('co-evidence-preview').classList.add('hidden');
}

document.getElementById('co-evidence-file')?.addEventListener('change', function(e) {
    var fileName = e.target.files[0]?.name;
    if (fileName) {
        document.getElementById('co-filename').textContent = fileName;
        document.getElementById('co-evidence-preview').classList.remove('hidden');
    }
});

function closeCounterOfferModal() {
    var modal = document.getElementById('counter-offer-modal');
    modal.classList.add('hidden');
    modal.classList.remove('flex');
    document.body.style.overflow = '';
}

async function submitCounterOffer() {
    var amount = parseFloat(document.getElementById('co-amount').value);
    var reason = document.getElementById('co-reason').value.trim();

    if (!amount || amount <= 0) { showToast(S.enterValidAmount, 'error'); return; }
    if (amount >= counterState.originalAmount) { showToast(S.counterOfferMustBeLess, 'error'); return; }
    if (!reason) { showToast(S.pleaseProvideReason, 'error'); return; }

    var fileInput = document.getElementById('co-evidence-file');
    var hasFile = fileInput && fileInput.files.length > 0;

    var btn = document.getElementById('co-submit-btn');
    btn.disabled = true;
    try {
        var token = getToken();
        var formData = new FormData();
        formData.append('rentalId', counterState.rentalId);
        formData.append('amount', amount);
        formData.append('response', reason);
        if (hasFile) formData.append('evidence', fileInput.files[0]);
        if (token) formData.append('__RequestVerificationToken', token);

        var response = await fetch(DC.urls.counterOfferDeposit, {
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
    } catch (err) { showToast(S.somethingWentWrong, 'error'); btn.disabled = false; }
}

// === Add Evidence (Global) ===
function openAddEvidenceModal(rentalId) {
    document.getElementById('ae-rental-id').value = rentalId;
    document.getElementById('ae-notes').value = '';
    clearAeEvidence();
    var modal = document.getElementById('add-evidence-modal');
    modal.classList.remove('hidden');
    modal.classList.add('flex');
    document.body.style.overflow = 'hidden';
}

function closeAddEvidenceModal() {
    var modal = document.getElementById('add-evidence-modal');
    modal.classList.add('hidden');
    modal.classList.remove('flex');
    document.body.style.overflow = '';
}

function clearAeEvidence() {
    document.getElementById('ae-evidence-file').value = '';
    document.getElementById('ae-evidence-preview').classList.add('hidden');
}

document.getElementById('ae-evidence-file')?.addEventListener('change', function(e) {
    if (e.target.files && e.target.files[0]) {
        var fileName = e.target.files[0].name;
        document.getElementById('ae-filename').textContent = fileName;
        document.getElementById('ae-evidence-preview').classList.remove('hidden');
    }
});

async function submitAddEvidence() {
    var rentalId = document.getElementById('ae-rental-id').value;
    var notes = document.getElementById('ae-notes').value.trim();
    var fileInput = document.getElementById('ae-evidence-file');

    if (!fileInput.files || fileInput.files.length === 0) {
        showToast(S.pleaseSelectFile, 'warning');
        return;
    }

    var btn = document.getElementById('ae-submit-btn');
    btn.disabled = true;
    btn.innerHTML = '<i class="bi bi-hourglass-split animate-spin mr-2"></i>' + S.uploading;

    try {
        var result = await uploadEvidence(rentalId, fileInput.files[0], notes);
        if (result.success) {
            showToast(S.evidenceAddedSuccess, 'success');
            closeAddEvidenceModal();
            setTimeout(function () { window.location.reload(); }, 1000);
        } else {
            showToast(result.message || S.uploadFailed, 'error');
        }
    } catch (err) {
        showToast(S.unexpectedError, 'error');
    } finally {
        btn.disabled = false;
        btn.innerHTML = S.upload;
    }
}

// === Maintain Charge (Owner) ===
function openMaintainChargeModal(rentalId, evidenceJson) {
    document.getElementById('mc-rental-id').value = rentalId;
    document.getElementById('mc-reason').value = '';

    // Populating existing evidence
    renderModalEvidence('mc-existing-evidence', evidenceJson);

    var modal = document.getElementById('maintain-charge-modal');
    modal.classList.remove('hidden');
    modal.classList.add('flex');
    document.body.style.overflow = 'hidden';
}

function closeMaintainChargeModal() {
    document.getElementById('maintain-charge-modal').classList.add('hidden');
    document.getElementById('maintain-charge-modal').classList.remove('flex');
    document.body.style.overflow = '';
    clearMcEvidence();
}

function clearMcEvidence() {
    document.getElementById('mc-evidence-file').value = '';
    document.getElementById('mc-evidence-preview').classList.add('hidden');
}

document.getElementById('mc-evidence-file')?.addEventListener('change', function(e) {
    var fileName = e.target.files[0]?.name;
    if (fileName) {
        document.getElementById('mc-filename').textContent = fileName;
        document.getElementById('mc-evidence-preview').classList.remove('hidden');
    }
});

async function submitMaintainCharge() {
    var rentalId = document.getElementById('mc-rental-id').value;
    var reason = document.getElementById('mc-reason').value.trim();
    if (!reason) { showToast(S.pleaseProvideReason, 'error'); return; }
    var btn = document.getElementById('mc-submit-btn');
    var fileInput = document.getElementById('mc-evidence-file');
    var hasFile = fileInput && fileInput.files.length > 0;

    if (!confirm(S.maintainChargeConfirm)) return;

    btn.disabled = true;
    btn.innerHTML = '<i class="bi bi-arrow-repeat animate-spin mr-1"></i> ' + S.processing;

    try {
        var token = getToken();
        var formData = new FormData();
        formData.append('rentalId', rentalId);
        formData.append('response', reason);
        if (hasFile) formData.append('evidence', fileInput.files[0]);
        if (token) formData.append('__RequestVerificationToken', token);

        var response = await fetch(DC.urls.maintainCharge, {
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
            btn.innerHTML = S.confirmSendAdmin;
        }
    } catch (err) {
        showToast(S.somethingWentWrong, 'error');
        btn.disabled = false;
        btn.innerHTML = S.confirmSendAdmin;
    }
}

// === Accept Charge (Renter) ===
async function acceptCharge(rentalId) {
    if (!confirm(S.acceptChargeConfirm)) return;
    var btn = event.target.closest('button');
    btn.disabled = true;
    try {
        var token = getToken();
        var response = await fetch(DC.urls.acceptCharge, {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'X-CSRF-TOKEN': token },
            body: 'rentalId=' + rentalId + (token ? '&__RequestVerificationToken=' + encodeURIComponent(token) : '')
        });
        var data = await response.json();
        showToast(data.message, data.success ? 'success' : 'error');
        if (data.success) setTimeout(function () { location.reload(); }, 1000);
        else btn.disabled = false;
    } catch (err) { showToast(S.somethingWentWrong, 'error'); btn.disabled = false; }
}

// === Accept Counter-Offer (Renter) ===
async function acceptCounterOffer(rentalId) {
    if (!confirm(S.acceptCounterOfferConfirm)) return;
    var btn = event.target.closest('button');
    btn.disabled = true;
    try {
        var token = getToken();
        var response = await fetch(DC.urls.acceptCounterOffer, {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'X-CSRF-TOKEN': token },
            body: 'rentalId=' + rentalId + (token ? '&__RequestVerificationToken=' + encodeURIComponent(token) : '')
        });
        var data = await response.json();
        showToast(data.message, data.success ? 'success' : 'error');
        if (data.success) setTimeout(function () { location.reload(); }, 1000);
        else btn.disabled = false;
    } catch (err) { showToast(S.somethingWentWrong, 'error'); btn.disabled = false; }
}

// === Reject Counter-Offer (Renter) ===
async function rejectCounterOffer(rentalId) {
    if (!confirm(S.rejectCounterOfferConfirm)) return;
    var btn = event.target.closest('button');
    btn.disabled = true;
    try {
        var token = getToken();
        var response = await fetch(DC.urls.rejectCounterOffer, {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'X-CSRF-TOKEN': token },
            body: 'rentalId=' + rentalId + (token ? '&__RequestVerificationToken=' + encodeURIComponent(token) : '')
        });
        var data = await response.json();
        showToast(data.message, data.success ? 'success' : 'error');
        if (data.success) setTimeout(function () { location.reload(); }, 1000);
        else btn.disabled = false;
    } catch (err) { showToast(S.somethingWentWrong, 'error'); btn.disabled = false; }
}

// === Escalate Dispute - Internal Process ===
async function _processEscalation(rentalId) {
    var escBtn = document.getElementById('escalate-btn-' + rentalId) || document.getElementById('escalate-btn-co-' + rentalId);
    if (escBtn) escBtn.disabled = true;
    try {
        var token = getToken();
        var response = await fetch(DC.urls.escalateDispute, {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'X-CSRF-TOKEN': token },
            body: 'rentalId=' + rentalId + (token ? '&__RequestVerificationToken=' + encodeURIComponent(token) : '')
        });
        var data = await response.json();
        showToast(data.message, data.success ? 'success' : 'error');
        if (data.success) setTimeout(function () { location.reload(); }, 1000);
        else if (escBtn) escBtn.disabled = false;
    } catch (err) { showToast(S.somethingWentWrong, 'error'); if (escBtn) escBtn.disabled = false; }
}

// === Evidence Upload Helper ===
async function uploadEvidence(rentalId, file, notes) {
    if (!file) return { success: true };

    var formData = new FormData();
    formData.append('rentalId', rentalId);
    formData.append('file', file);
    if (notes) formData.append('notes', notes);
    var token = getToken();
    if (token) formData.append('__RequestVerificationToken', token);

    try {
        var response = await fetch(DC.urls.uploadEvidence, {
            method: 'POST',
            headers: { 'X-CSRF-TOKEN': getToken() },
            body: formData
        });
        if (!response.ok) throw new Error('Upload failed');
        return await response.json();
    } catch (err) {
        console.error('Evidence upload error:', err);
        return { success: false, message: 'Upload failed' };
    }
}

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
            var a = document.createElement('a');
            a.href = ev.Url;
            a.target = '_blank';
            a.className = 'w-12 h-12 rounded-lg overflow-hidden border border-slate-200 dark:border-slate-700 hover:scale-105 transition-transform';
            if (ev.Notes) a.title = ev.Notes;

            var img = document.createElement('img');
            img.src = ev.Url;
            img.className = 'w-full h-full object-cover';

            a.appendChild(img);
            list.appendChild(a);
        });
        container.classList.remove('hidden');
    } else {
        container.classList.add('hidden');
    }
}

// === Escalation Warning Modal ===
window.escalateDispute = function(rentalId) {
    showEscalationWarning(rentalId);
};

window.showEscalationWarning = function(rentalId) {
    document.getElementById('escalateRentalId').value = rentalId;
    document.getElementById('escalationWarningModal').classList.remove('hidden');
};

window.closeEscalationWarningModal = function() {
    document.getElementById('escalationWarningModal').classList.add('hidden');
};
