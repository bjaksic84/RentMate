// =============================================
// lending-requests.js — Extension Review Modal
// =============================================
// Pending rental requests render as inline rich cards in _DashboardLending.cshtml.
// Only the extension review modal still needs JS — extension data is JSON-payload
// driven and toggled on row click.
// =============================================

(function () {
    'use strict';

    var DC = window.DashboardConfig || {};
    var S = DC.strings || {};

    var ermModal = null;

    // ── Focus trap helpers ────────────────────────────────────────────

    var FOCUSABLE = 'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

    function trapFocus(modal, e) {
        var focusable = Array.from(modal.querySelectorAll(FOCUSABLE));
        if (!focusable.length) return;
        var first = focusable[0];
        var last = focusable[focusable.length - 1];
        if (e.shiftKey) {
            if (document.activeElement === first) { e.preventDefault(); last.focus(); }
        } else {
            if (document.activeElement === last) { e.preventDefault(); first.focus(); }
        }
    }

    // ── Open / close helpers ──────────────────────────────────────────

    var _activeModal = null;
    var _triggerEl = null;

    function openModal(modal, triggerEl) {
        _activeModal = modal;
        _triggerEl = triggerEl || null;
        modal.classList.remove('hidden');
        document.body.style.overflow = 'hidden';
        var titleEl = modal.querySelector('[tabindex="-1"]');
        if (titleEl) setTimeout(function () { titleEl.focus(); }, 50);
        modal.addEventListener('keydown', onModalKeyDown);
    }

    function closeModal(modal) {
        modal.classList.add('hidden');
        document.body.style.overflow = '';
        modal.removeEventListener('keydown', onModalKeyDown);
        if (_triggerEl) {
            _triggerEl.focus();
            _triggerEl = null;
        }
        _activeModal = null;
    }

    function onModalKeyDown(e) {
        if (e.key === 'Escape') {
            e.preventDefault();
            closeModal(_activeModal);
            return;
        }
        if (e.key === 'Tab') {
            trapFocus(_activeModal, e);
        }
    }

    // ── Extension Review Modal ────────────────────────────────────────

    function getExtensionData(extensionId) {
        var script = document.querySelector('.extension-request-data[data-extension-id="' + extensionId + '"]');
        if (!script) return null;
        try { return JSON.parse(script.textContent); } catch (e) { return null; }
    }

    function fillExtensionModal(data) {
        if (!data || !ermModal) return;

        var titleEl = ermModal.querySelector('#extension-review-modal-title');
        if (titleEl) titleEl.textContent = data.item.title;

        var fromLine = ermModal.querySelector('#erm-from-line');
        if (fromLine) fromLine.textContent = (S.from || 'From') + ' ' + data.renterFirstName;

        var progressEl = ermModal.querySelector('#erm-progress');
        if (progressEl) progressEl.textContent = data.progress;

        var oldEnd = ermModal.querySelector('#erm-old-end');
        var newEnd = ermModal.querySelector('#erm-new-end');
        var extraDays = ermModal.querySelector('#erm-extra-days');
        if (oldEnd) oldEnd.textContent = data.originalEndDate;
        if (newEnd) newEnd.textContent = data.newEndDate;
        if (extraDays) extraDays.textContent = '+' + data.extraDays + ' ' + (S.days || 'days');

        var costEl = ermModal.querySelector('#erm-additional-cost');
        if (costEl) costEl.textContent = data.additionalCostFmt;

        var conflictDiv = ermModal.querySelector('#erm-conflict-warning');
        var conflictText = ermModal.querySelector('#erm-conflict-text');
        var conflictList = ermModal.querySelector('#erm-conflict-list');
        if (conflictDiv) {
            if (data.hasConflict && data.conflicts && data.conflicts.length > 0) {
                conflictDiv.classList.remove('hidden');
                if (conflictText) conflictText.textContent = (S.extensionConflict || 'Conflicts with') + ' ' + data.conflicts.length + ' upcoming booking(s)';
                if (conflictList) {
                    conflictList.innerHTML = '';
                    data.conflicts.forEach(function (c) {
                        var li = document.createElement('li');
                        li.textContent = c.start + ' – ' + c.end + (c.renterName ? ' (' + c.renterName + ')' : '');
                        conflictList.appendChild(li);
                    });
                }
            } else {
                conflictDiv.classList.add('hidden');
            }
        }

        var approveBtn = ermModal.querySelector('#erm-approve-btn');
        var declineBtn = ermModal.querySelector('#erm-decline-btn');
        if (approveBtn) {
            approveBtn.onclick = function () {
                closeModal(ermModal);
                if (typeof approveExtension === 'function') approveExtension(data.extensionId);
            };
        }
        if (declineBtn) {
            declineBtn.onclick = function () {
                closeModal(ermModal);
                if (typeof declineExtension === 'function') declineExtension(data.extensionId);
            };
        }
    }

    // ── Row click handlers ────────────────────────────────────────────

    function initExtensionRows() {
        document.querySelectorAll('.extension-request-row').forEach(function (row) {
            row.addEventListener('click', function (e) {
                if (e.target.closest('button')) return;
                var extId = row.dataset.extensionId;
                var data = getExtensionData(extId);
                if (!data) return;
                fillExtensionModal(data);
                openModal(ermModal, row);
            });
            row.addEventListener('keydown', function (e) {
                if (e.key === 'Enter' || e.key === ' ') {
                    e.preventDefault();
                    row.click();
                }
            });
        });
    }

    // ── Close triggers ────────────────────────────────────────────────

    function initCloseHandlers(modal, actionName) {
        modal.querySelectorAll('[data-action="' + actionName + '"]').forEach(function (el) {
            el.addEventListener('click', function () { closeModal(modal); });
        });
    }

    // ── Init ──────────────────────────────────────────────────────────

    function init() {
        try {
            ermModal = document.getElementById('extension-review-modal');
            if (!ermModal) return;

            initCloseHandlers(ermModal, 'close-extension-review');
            initExtensionRows();
        } catch (err) {
            console.error('[lending-requests] init error:', err);
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})();
