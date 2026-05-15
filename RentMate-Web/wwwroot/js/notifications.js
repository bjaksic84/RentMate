(function() {
    'use strict';

    var notifSound = null;
    var soundEnabled = localStorage.getItem('notifSound') !== 'false';

    function playNotifSound() {
        if (!soundEnabled) return;
        if (!notifSound) {
            notifSound = new Audio('/sounds/notification.mp3');
            notifSound.volume = 0.3;
        }
        notifSound.play().catch(function() {});
    }

    function escapeHtml(s) {
        if (typeof window.escapeHtml === 'function') return window.escapeHtml(s);
        var d = document.createElement('div');
        d.textContent = String(s);
        return d.innerHTML;
    }

    function getNotifIcon(type) {
        if (type === 'ProfileSuggestion') return { icon: 'bi-person-check', color: 'text-blue-500' };
        if (type.startsWith('Rental')) return { icon: 'bi-house-heart', color: 'text-trust-blue-600' };
        if (type.startsWith('Extension')) return { icon: 'bi-calendar-plus', color: 'text-sky-500' };
        if (type.startsWith('Deposit') || type.startsWith('Deadline')) return { icon: 'bi-shield-check', color: 'text-amber-500' };
        if (type.startsWith('Review')) return { icon: 'bi-star', color: 'text-yellow-500' };
        if (type.startsWith('Payment')) return { icon: 'bi-credit-card', color: 'text-emerald-500' };
        if (type.startsWith('Admin')) return { icon: 'bi-shield-exclamation', color: 'text-rose-500' };
        if (type.startsWith('Account') || type.startsWith('Security')) return { icon: 'bi-person-exclamation', color: 'text-rose-500' };
        return { icon: 'bi-bell', color: 'text-slate-500' };
    }

    function timeAgo(dateStr) {
        var now = new Date();
        var date = new Date(dateStr);
        var diffMs = now - date;
        var diffMin = Math.floor(diffMs / 60000);
        var diffHr = Math.floor(diffMs / 3600000);
        var diffDay = Math.floor(diffMs / 86400000);

        if (diffMin < 1) return window.T && window.T['just now'] ? window.T['just now'] : 'just now';
        if (diffMin < 60) return diffMin + ' ' + (window.T && window.T['minutes ago'] ? window.T['minutes ago'] : 'minutes ago');
        if (diffHr < 24) return diffHr + ' ' + (window.T && window.T['hours ago'] ? window.T['hours ago'] : 'hours ago');
        return diffDay + ' ' + (window.T && window.T['days ago'] ? window.T['days ago'] : 'days ago');
    }

    function renderNotification(n, container) {
        var info = getNotifIcon(n.type);
        var isProfileSuggestion = n.type === 'ProfileSuggestion';
        var el = document.createElement('div');

        if (isProfileSuggestion) {
            // Profile suggestions get a distinct card-like treatment
            el.className = 'px-4 py-3 flex items-start gap-3 hover:bg-blue-50/80 dark:hover:bg-blue-900/20 transition-colors cursor-pointer border-l-2 border-blue-500'
                + ' bg-gradient-to-r from-blue-50/60 to-transparent dark:from-blue-950/20 dark:to-transparent';
        } else {
            el.className = 'px-4 py-3 flex items-start gap-3 hover:bg-slate-50 dark:hover:bg-slate-700/50 transition-colors cursor-pointer'
                + (n.isRead ? '' : ' bg-blue-50/50 dark:bg-blue-900/10');
        }
        el.setAttribute('data-notif-id', n.id);

        var safeTitle = escapeHtml(n.title) + (n.count > 1 ? ' (' + n.count + 'x)' : '');
        var safeMessage = escapeHtml(n.message);

        if (isProfileSuggestion) {
            // Profile suggestion: icon bubble + title + action arrow
            el.innerHTML =
                '<div class="shrink-0 w-9 h-9 rounded-xl bg-blue-100 dark:bg-blue-900/40 flex items-center justify-center">' +
                    '<i class="bi ' + info.icon + ' ' + info.color + ' text-base"></i>' +
                '</div>' +
                '<div class="flex-1 min-w-0">' +
                    '<p class="text-sm font-semibold text-slate-900 dark:text-white">' + safeTitle + '</p>' +
                    '<p class="text-xs text-blue-600 dark:text-blue-400 font-medium mt-0.5">' +
                        '<span class="inline-flex items-center gap-1">' + (window.T ? window.T['Complete profile'] : 'Complete profile') + ' <i class="bi bi-arrow-right text-[10px]"></i></span>' +
                    '</p>' +
                '</div>' +
                '<button class="shrink-0 p-1 text-slate-400 hover:text-slate-600 dark:hover:text-slate-300 transition-colors notif-dismiss" title="Dismiss">' +
                    '<i class="bi bi-x-lg text-xs"></i>' +
                '</button>';
        } else {
            el.innerHTML =
                '<div class="shrink-0 w-8 h-8 rounded-full bg-slate-100 dark:bg-slate-700 flex items-center justify-center">' +
                    '<i class="bi ' + info.icon + ' ' + info.color + '"></i>' +
                '</div>' +
                '<div class="flex-1 min-w-0">' +
                    '<p class="text-sm font-medium text-slate-900 dark:text-white truncate">' + safeTitle + '</p>' +
                    (safeMessage ? '<p class="text-xs text-slate-500 dark:text-slate-400 truncate mt-0.5">' + safeMessage + '</p>' : '') +
                    '<p class="text-xs text-slate-400 dark:text-slate-500 mt-1">' + timeAgo(n.createdAt) + '</p>' +
                '</div>' +
                '<button class="shrink-0 p-1 text-slate-400 hover:text-slate-600 dark:hover:text-slate-300 transition-colors notif-dismiss" title="Dismiss">' +
                    '<i class="bi bi-x-lg text-xs"></i>' +
                '</button>';
        }

        // Click body → navigate
        el.addEventListener('click', function(e) {
            if (e.target.closest('.notif-dismiss')) return;
            if (n.actionUrl) {
                // Mark as read
                var ids = n.ids || [n.id];
                ids.forEach(function(nid) {
                    fetch('/Notification/MarkAsRead', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': window.getToken() },
                        body: JSON.stringify({ id: nid })
                    }).catch(function() {});
                });
                window.location.href = n.actionUrl;
            }
        });

        // Dismiss button
        el.querySelector('.notif-dismiss').addEventListener('click', function(e) {
            e.stopPropagation();
            var ids = n.ids || [n.id];
            var promises = ids.map(function(nid) {
                return fetch('/Notification/Dismiss', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': window.getToken() },
                    body: JSON.stringify({ id: nid })
                });
            });
            Promise.all(promises).then(function(responses) {
                var allOk = responses.every(function(r) { return r.ok; });
                if (!allOk) return;
                el.remove();
                updateBadge(-1 * ids.length);
                // Check if list is now empty
                var list = container;
                if (list && list.children.length === 0) {
                    var emptyId = list.id === 'notificationMobileList' ? 'notificationMobileEmpty' : 'notificationEmpty';
                    var emptyEl = document.getElementById(emptyId);
                    if (emptyEl) emptyEl.classList.remove('hidden');
                }
            }).catch(function() {});
        });

        container.appendChild(el);
    }

    function updateBadge(delta) {
        var badge = document.getElementById('notificationBadge');
        if (!badge) return;
        var count = parseInt(badge.textContent, 10) || 0;
        count = Math.max(0, count + delta);
        badge.textContent = count;
        if (count > 0) {
            badge.classList.remove('hidden');
        } else {
            badge.classList.add('hidden');
        }
    }

    function fetchUnreadCount() {
        fetch('/Notification/UnreadCount')
            .then(function(r) { return r.json(); })
            .then(function(data) {
                var badge = document.getElementById('notificationBadge');
                if (badge) {
                    badge.textContent = data.count;
                    if (data.count > 0) badge.classList.remove('hidden');
                    else badge.classList.add('hidden');
                }
            })
            .catch(function() {});
    }

    function fetchAndRender(listId, emptyId) {
        var list = document.getElementById(listId);
        var empty = document.getElementById(emptyId);
        if (!list) return;

        fetch('/Notification/Recent')
            .then(function(r) { return r.json(); })
            .then(function(notifications) {
                list.innerHTML = '';
                if (notifications.length === 0) {
                    if (empty) empty.classList.remove('hidden');
                } else {
                    if (empty) empty.classList.add('hidden');
                    notifications.forEach(function(n) {
                        renderNotification(n, list);
                    });
                }
            })
            .catch(function(err) {
                console.error('[notifications] fetchAndRender error:', err);
            });
    }

    window.NotificationBell = {
        toggle: function() {
            if (window.innerWidth < 768) {
                var panel = document.getElementById('notificationMobilePanel');
                if (panel) {
                    panel.classList.remove('hidden');
                    fetchAndRender('notificationMobileList', 'notificationMobileEmpty');
                }
            } else {
                var dd = document.getElementById('notificationDropdown');
                if (dd) {
                    var isHidden = dd.classList.contains('hidden');
                    dd.classList.toggle('hidden');
                    if (isHidden) fetchAndRender('notificationList', 'notificationEmpty');
                }
            }
        },

        closeMobile: function() {
            var panel = document.getElementById('notificationMobilePanel');
            if (panel) panel.classList.add('hidden');
        },

        markAllAsRead: function() {
            fetch('/Notification/MarkAllAsRead', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': window.getToken() }
            }).then(function() {
                var badge = document.getElementById('notificationBadge');
                if (badge) {
                    badge.textContent = '0';
                    badge.classList.add('hidden');
                }
                // Remove unread styling from all items
                document.querySelectorAll('[data-notif-id]').forEach(function(el) {
                    el.classList.remove('bg-blue-50/50', 'dark:bg-blue-900/10');
                });
            }).catch(function() {});
        },

        toggleSound: function() {
            soundEnabled = !soundEnabled;
            localStorage.setItem('notifSound', soundEnabled ? 'true' : 'false');
            var icon = document.querySelector('#notifSoundToggle i');
            if (icon) icon.className = soundEnabled ? 'bi bi-volume-up text-sm' : 'bi bi-volume-mute text-sm';
        }
    };

    // Close dropdown on outside click
    document.addEventListener('click', function(e) {
        if (!e.target.closest('#notificationBellContainer')) {
            var dd = document.getElementById('notificationDropdown');
            if (dd && !dd.classList.contains('hidden')) {
                dd.classList.add('hidden');
            }
        }
    });

    // Init: update sound toggle icon
    document.addEventListener('DOMContentLoaded', function() {
        var icon = document.querySelector('#notifSoundToggle i');
        if (icon && !soundEnabled) {
            icon.className = 'bi bi-volume-mute text-sm';
        }
    });

    // Fetch unread count on page load (for authenticated users)
    if (document.querySelector('#notificationBellBtn')) {
        fetchUnreadCount();
    }

    // Expose for layout.js SignalR handler
    window.playNotifSound = playNotifSound;
})();
