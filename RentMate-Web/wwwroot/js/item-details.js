/**
 * Item Details page JavaScript
 * Reads configuration from window.ItemDetailsConfig (set by Details.cshtml)
 */
(function () {
    'use strict';

    var Config = window.ItemDetailsConfig || {};
    var S = Config.strings || {};
    var URLs = Config.urls || {};

    // ── Helpers ──────────────────────────────────────────────
    function getToken() {
        return document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    }

    function escHtml(s) {
        var d = document.createElement('div');
        d.textContent = s || '';
        return d.innerHTML;
    }

    // =========================================================
    // Image Gallery
    // =========================================================
    var imageUrls = (Config.images || []).map(function (img) {
        var url = img.url || '';
        if (url.indexOf('/upload/') > -1) {
            url = url.replace('/upload/', '/upload/c_fill,w_1200,h_800,q_auto,f_auto/');
        }
        return url;
    });
    var currentImageIndex = 0;
    var totalImages = imageUrls.length;

    function showImage(index, url) {
        if (index < 0 || index >= totalImages) return;
        currentImageIndex = index;

        var mainImage = document.getElementById('mainImage');
        if (!mainImage) return;

        mainImage.style.opacity = '0';
        setTimeout(function () {
            mainImage.src = url || imageUrls[index];
            mainImage.style.opacity = '1';
        }, 150);

        // Counter
        var counter = document.getElementById('imageCounter');
        if (counter) counter.textContent = (index + 1) + ' / ' + totalImages;

        // Thumbnail highlight
        document.querySelectorAll('.thumbnail-btn').forEach(function (btn, i) {
            if (i === index) {
                btn.classList.add('border-blue-500', 'ring-2', 'ring-blue-500');
                btn.classList.remove('border-transparent');
            } else {
                btn.classList.remove('border-blue-500', 'ring-2', 'ring-blue-500');
                btn.classList.add('border-transparent');
            }
        });

        // Lightbox sync
        var lbImage = document.querySelector('.lightbox-image');
        if (lbImage) lbImage.src = imageUrls[index];
        var lbCounter = document.querySelector('.lightbox-counter');
        if (lbCounter) lbCounter.textContent = (index + 1) + ' / ' + totalImages;

        // Preload adjacent
        if (index + 1 < totalImages) { var p1 = new Image(); p1.src = imageUrls[index + 1]; }
        if (index - 1 >= 0) { var p2 = new Image(); p2.src = imageUrls[index - 1]; }
    }

    function nextImage() {
        if (totalImages < 2) return;
        showImage((currentImageIndex + 1) % totalImages);
    }

    function prevImage() {
        if (totalImages < 2) return;
        showImage((currentImageIndex - 1 + totalImages) % totalImages);
    }

    window.showImage = showImage;
    window.nextImage = nextImage;
    window.prevImage = prevImage;

    // =========================================================
    // Lightbox
    // =========================================================
    var lightboxOpen = false;

    function openLightbox() {
        var overlay = document.querySelector('.lightbox-overlay');
        if (!overlay) return;
        overlay.classList.remove('hidden');
        overlay.classList.add('flex');
        lightboxOpen = true;
        document.body.style.overflow = 'hidden';

        var lbImage = overlay.querySelector('.lightbox-image');
        if (lbImage && imageUrls[currentImageIndex]) {
            lbImage.src = imageUrls[currentImageIndex];
        }
        var lbCounter = overlay.querySelector('.lightbox-counter');
        if (lbCounter) lbCounter.textContent = (currentImageIndex + 1) + ' / ' + totalImages;
    }

    function closeLightbox() {
        var overlay = document.querySelector('.lightbox-overlay');
        if (!overlay) return;
        overlay.classList.add('hidden');
        overlay.classList.remove('flex');
        lightboxOpen = false;
        document.body.style.overflow = '';

        var lbImage = overlay.querySelector('.lightbox-image');
        if (lbImage) {
            lbImage.classList.remove('zoomed');
            lbImage.style.transformOrigin = '';
        }
    }

    window.openLightbox = openLightbox;
    window.closeLightbox = closeLightbox;

    // =========================================================
    // Map Initialization (Leaflet)
    // =========================================================
    function initMap() {
        var mapEl = document.getElementById('detailMap');
        if (!mapEl || typeof L === 'undefined') return;

        var lat = Config.map?.lat;
        var lng = Config.map?.lng;
        var cityName = Config.map?.city || '';

        if (!lat || !lng || lat === 0 || lng === 0) {
            mapEl.innerHTML = '<div class="flex items-center justify-center h-full text-slate-500">Map unavailable for this location.</div>';
            return;
        }

        try {
            var map = L.map('detailMap', { scrollWheelZoom: false }).setView([lat, lng], 11);

            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                attribution: '&copy; OpenStreetMap contributors'
            }).addTo(map);

            L.circle([lat, lng], {
                color: '#2563eb',
                fillColor: '#2563eb',
                fillOpacity: 0.15,
                radius: 1200
            }).addTo(map).bindPopup('<b>' + cityName + '</b>');

            // Fix tiles not rendering when map is below the fold
            setTimeout(function () { map.invalidateSize(); }, 500);
            if (typeof IntersectionObserver !== 'undefined') {
                var observer = new IntersectionObserver(function (entries) {
                    if (entries[0].isIntersecting) {
                        map.invalidateSize();
                        observer.disconnect();
                    }
                });
                observer.observe(mapEl);
            }
        } catch (e) {
            mapEl.innerHTML = '<div class="flex items-center justify-center h-full text-red-500">Map failed to load.</div>';
            console.error('Map initialization error:', e);
        }
    }

    // =========================================================
    // Reviews — Delete
    // =========================================================
    async function deleteReview(reviewId) {
        if (!confirm(S.confirmDelete || 'Are you sure?')) return;
        try {
            var response = await fetch(URLs.deleteReview + reviewId, {
                method: 'DELETE',
                headers: { 'X-CSRF-TOKEN': getToken() }
            });
            if (response.ok) {
                location.reload();
            } else {
                var error = await response.text();
                if (typeof showToast === 'function') showToast(error || 'Failed to delete review.', 'error');
            }
        } catch (err) {
            console.error('Error deleting review:', err);
            if (typeof showToast === 'function') showToast('An error occurred.', 'error');
        }
    }
    window.deleteReview = deleteReview;

    // =========================================================
    // Reviews — Edit Modal
    // =========================================================
    function setStars(containerSel, rating) {
        document.querySelectorAll(containerSel + ' i').forEach(function (s, idx) {
            if (idx < rating) {
                s.classList.remove('bi-star', 'text-slate-300', 'dark:text-slate-500');
                s.classList.add('bi-star-fill', 'text-amber-400');
            } else {
                s.classList.remove('bi-star-fill', 'text-amber-400');
                s.classList.add('bi-star', 'text-slate-300');
            }
        });
    }

    function openEditReviewModal(reviewId, rating, title, body, isAnonymous) {
        var modal = document.getElementById('editReviewModal');
        if (!modal) return;
        document.getElementById('editReviewId').value = reviewId;
        document.getElementById('editReviewTitle').value = title || '';
        document.getElementById('editReviewBody').value = body || '';
        document.getElementById('editReviewAnonymous').checked = isAnonymous;
        document.getElementById('editRatingInput').value = rating;
        setStars('#editStarPicker', rating);
        modal.classList.remove('hidden');
    }
    window.openEditReviewModal = openEditReviewModal;

    function closeEditReviewModal() {
        var modal = document.getElementById('editReviewModal');
        if (modal) modal.classList.add('hidden');
    }
    window.closeEditReviewModal = closeEditReviewModal;

    // =========================================================
    // Share Button
    // =========================================================
    function initShareButton() {
        var btn = document.getElementById('shareBtn');
        if (!btn) return;
        btn.addEventListener('click', function () {
            navigator.clipboard.writeText(window.location.href).then(function () {
                if (typeof showToast === 'function') showToast(S.linkCopied || 'Link copied!', 'success');
            }).catch(function () {
                var input = document.createElement('input');
                input.value = window.location.href;
                document.body.appendChild(input);
                input.select();
                document.execCommand('copy');
                document.body.removeChild(input);
                if (typeof showToast === 'function') showToast(S.linkCopied || 'Link copied!', 'success');
            });
        });
    }

    // =========================================================
    // Review Card Builder (for Load More)
    // =========================================================
    function buildReviewCard(review) {
        // Normalize API ReviewSummary fields vs MVC ReviewViewModel fields
        var reviewerName = review.reviewerName || review.reviewerUserName || '';
        var isAnonymous = review.isAnonymous || (reviewerName === 'Anonymous') || !reviewerName;
        var body = review.body || review.comment || '';
        var title = review.title || '';
        var profilePic = review.reviewerProfilePictureUrl;

        var rawName = isAnonymous ? (S.anonymous || 'Anonymous') : (reviewerName || S.user || 'User');
        var name = escHtml(rawName);
        var initial = escHtml(isAnonymous ? 'A' : (rawName.charAt(0).toUpperCase() || 'U'));
        var grad = isAnonymous ? 'from-slate-400 to-slate-500' : 'from-blue-500 to-blue-600';
        var dateStr = review.createdAt
            ? new Date(review.createdAt).toLocaleDateString(undefined, { month: 'long', year: 'numeric' })
            : '';

        var avatarHtml;
        if (!isAnonymous && profilePic) {
            avatarHtml = '<img src="' + escHtml(profilePic) + '" class="w-10 h-10 rounded-full object-cover shrink-0" />';
        } else {
            avatarHtml = '<div class="w-10 h-10 rounded-full bg-gradient-to-br ' + grad + ' flex items-center justify-center text-white font-semibold text-sm shrink-0">' + initial + '</div>';
        }

        var starsHtml = '';
        for (var i = 1; i <= 5; i++) {
            starsHtml += '<i class="bi bi-star' + (i <= review.rating ? '-fill' : '') + ' text-sm"></i>';
        }

        var editBtns = '';
        if (review.reviewerId && review.reviewerId === Config.currentUserId) {
            editBtns = '<div class="flex items-center gap-1 shrink-0">' +
                '<button type="button" data-action="editReview" data-review-id="' + review.id + '" data-item-id="' + Config.itemId + '" data-rating="' + review.rating + '" data-title="' + escHtml(title) + '" data-body="' + escHtml(body) + '" data-is-anonymous="' + isAnonymous + '" class="p-1.5 text-slate-400 hover:text-blue-600 dark:hover:text-blue-400 transition-colors rounded-lg hover:bg-slate-100 dark:hover:bg-slate-700"><i class="bi bi-pencil"></i></button>' +
                '<button type="button" onclick="deleteReview(' + review.id + ')" class="p-1.5 text-slate-400 hover:text-red-600 dark:hover:text-red-400 transition-colors rounded-lg hover:bg-slate-100 dark:hover:bg-slate-700"><i class="bi bi-trash"></i></button></div>';
        }

        return '<div class="bg-white dark:bg-slate-800 rounded-xl border border-slate-200 dark:border-slate-700 p-4" id="review-card-' + review.id + '">' +
            '<div class="flex items-start justify-between gap-2 mb-3"><div class="flex items-center gap-3">' +
            avatarHtml +
            '<div><div class="font-semibold text-slate-900 dark:text-white text-sm">' + name + '</div>' +
            '<div class="text-xs text-slate-400 dark:text-slate-500">' + escHtml(dateStr) + '</div></div></div>' +
            editBtns + '</div>' +
            '<div class="flex items-center gap-0.5 text-amber-400 mb-2">' + starsHtml + '</div>' +
            (title ? '<div class="font-bold text-slate-900 dark:text-white text-sm mb-1">' + escHtml(title) + '</div>' : '') +
            (body ? '<p class="text-slate-600 dark:text-slate-400 text-sm line-clamp-3">' + escHtml(body) + '</p>' : '') +
            '</div>';
    }

    // =========================================================
    // Gallery Events
    // =========================================================
    function initGalleryEvents() {
        // Keyboard
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && lightboxOpen) { closeLightbox(); return; }
            if (e.key === 'ArrowLeft') prevImage();
            if (e.key === 'ArrowRight') nextImage();
        });

        // Touch / swipe on main image container
        var mainImage = document.getElementById('mainImage');
        var container = mainImage?.parentElement;
        if (container) {
            var touchStartX = 0;
            container.addEventListener('touchstart', function (e) {
                touchStartX = e.changedTouches[0].screenX;
            }, { passive: true });
            container.addEventListener('touchend', function (e) {
                var diff = touchStartX - e.changedTouches[0].screenX;
                if (Math.abs(diff) > 50) {
                    if (diff > 0) nextImage(); else prevImage();
                }
            }, { passive: true });
        }

        // Lightbox zoom toggle
        var lbImage = document.querySelector('.lightbox-image');
        if (lbImage) {
            lbImage.addEventListener('click', function (e) {
                e.stopPropagation();
                if (this.classList.contains('zoomed')) {
                    this.classList.remove('zoomed');
                    this.style.transformOrigin = '';
                } else {
                    var rect = this.getBoundingClientRect();
                    var x = ((e.clientX - rect.left) / rect.width * 100);
                    var y = ((e.clientY - rect.top) / rect.height * 100);
                    this.style.transformOrigin = x + '% ' + y + '%';
                    this.classList.add('zoomed');
                }
            });
        }
    }

    // =========================================================
    // Review Events
    // =========================================================
    function initReviewEvents() {
        // Edit button delegation
        document.addEventListener('click', function (e) {
            var btn = e.target.closest('[data-action="editReview"]');
            if (!btn) return;
            openEditReviewModal(
                parseInt(btn.dataset.reviewId, 10),
                parseInt(btn.dataset.rating, 10),
                btn.dataset.title || '',
                btn.dataset.body || '',
                btn.dataset.isAnonymous === 'true'
            );
        });

        // Edit star picker
        document.querySelectorAll('#editStarPicker i').forEach(function (star) {
            star.addEventListener('click', function () {
                var rating = parseInt(this.dataset.value);
                document.getElementById('editRatingInput').value = rating;
                setStars('#editStarPicker', rating);
            });
        });

        // Edit form submit
        var editForm = document.getElementById('editReviewForm');
        if (editForm) {
            editForm.addEventListener('submit', async function (e) {
                e.preventDefault();
                var rating = parseInt(document.getElementById('editRatingInput').value);
                if (rating < 1) {
                    if (typeof showToast === 'function') showToast('Please select a rating.', 'warning');
                    return;
                }
                var reviewId = document.getElementById('editReviewId').value;
                try {
                    var response = await fetch(URLs.updateReview + reviewId, {
                        method: 'PUT',
                        headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': getToken() },
                        body: JSON.stringify({
                            id: parseInt(reviewId),
                            itemId: Config.itemId,
                            rating: rating,
                            title: document.getElementById('editReviewTitle').value || null,
                            body: document.getElementById('editReviewBody').value,
                            isAnonymous: document.getElementById('editReviewAnonymous').checked
                        })
                    });
                    if (response.ok) { closeEditReviewModal(); location.reload(); }
                    else {
                        var error = await response.text();
                        if (typeof showToast === 'function') showToast(error || 'Failed to update review.', 'error');
                    }
                } catch (err) {
                    console.error('Error updating review:', err);
                    if (typeof showToast === 'function') showToast('An error occurred.', 'error');
                }
            });
        }

        // New review star picker
        document.querySelectorAll('#newReviewStarPicker i').forEach(function (star) {
            star.addEventListener('click', function () {
                var rating = parseInt(this.dataset.value);
                document.getElementById('newRatingInput').value = rating;
                setStars('#newReviewStarPicker', rating);
            });
        });

        // New review form submit
        var newForm = document.getElementById('newReviewForm');
        if (newForm) {
            newForm.addEventListener('submit', async function (e) {
                e.preventDefault();
                var rating = parseInt(document.getElementById('newRatingInput').value);
                if (rating < 1) {
                    if (typeof showToast === 'function') showToast('Please select a rating.', 'warning');
                    return;
                }
                var formData = new FormData(this);
                try {
                    var response = await fetch('/api/Reviews', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': getToken() },
                        body: JSON.stringify({
                            itemId: Config.itemId,
                            rating: rating,
                            title: formData.get('Title') || null,
                            body: formData.get('Body'),
                            isAnonymous: formData.get('IsAnonymous') === 'on'
                        })
                    });
                    if (response.ok) { location.reload(); }
                    else {
                        var error = await response.text();
                        if (typeof showToast === 'function') showToast(error || 'Failed to submit review.', 'error');
                    }
                } catch (err) {
                    console.error('Error submitting review:', err);
                    if (typeof showToast === 'function') showToast('An error occurred.', 'error');
                }
            });
        }

        // Load more reviews
        var loadMoreBtn = document.getElementById('loadMoreReviewsBtn');
        if (loadMoreBtn) {
            loadMoreBtn.addEventListener('click', async function () {
                try {
                    var response = await fetch(URLs.loadReviews + '?pageSize=100');
                    if (!response.ok) return;
                    var data = await response.json();
                    var reviews = data.data || data || [];
                    var container = document.getElementById('reviewCardsContainer');
                    if (!container) return;

                    container.innerHTML = '';
                    reviews.forEach(function (review) {
                        container.insertAdjacentHTML('beforeend', buildReviewCard(review));
                    });
                    loadMoreBtn.style.display = 'none';
                } catch (err) {
                    console.error('Error loading reviews:', err);
                }
            });
        }
    }

    // =========================================================
    // Availability Calendar (inline read-only month grid)
    // =========================================================
    function initAvailabilityCalendar() {
        var container = document.getElementById('availabilityCalendar');
        if (!container) return;

        var blockedRanges = (Config.blockedRanges || []).map(function (r) {
            return { from: new Date(r.from + 'T00:00:00'), to: new Date(r.to + 'T00:00:00') };
        });

        var today = new Date();
        today.setHours(0, 0, 0, 0);
        var currentMonth = today.getMonth();
        var currentYear = today.getFullYear();

        var dayNames = ['Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa', 'Su'];

        function isBooked(date) {
            for (var i = 0; i < blockedRanges.length; i++) {
                if (date >= blockedRanges[i].from && date <= blockedRanges[i].to) return true;
            }
            return false;
        }

        function isToday(date) {
            return date.getFullYear() === today.getFullYear() &&
                   date.getMonth() === today.getMonth() &&
                   date.getDate() === today.getDate();
        }

        function isPast(date) {
            return date < today;
        }

        function renderMonth(year, month) {
            var firstDay = new Date(year, month, 1);
            var lastDay = new Date(year, month + 1, 0);
            var startDow = (firstDay.getDay() + 6) % 7; // Monday = 0

            var html = '<div class="grid grid-cols-7 gap-0.5 mb-1">';
            for (var d = 0; d < 7; d++) {
                html += '<div class="text-center text-xs font-medium text-slate-400 dark:text-slate-500 py-1">' + dayNames[d] + '</div>';
            }
            html += '</div>';

            html += '<div class="grid grid-cols-7 gap-0.5">';

            // Empty cells before first day
            for (var e = 0; e < startDow; e++) {
                html += '<div class="h-9"></div>';
            }

            for (var day = 1; day <= lastDay.getDate(); day++) {
                var date = new Date(year, month, day);
                var classes = 'h-9 flex items-center justify-center text-sm rounded-lg transition-colors';
                var booked = isBooked(date);
                var todayCell = isToday(date);
                var past = isPast(date);

                if (booked) {
                    classes += ' bg-red-100 dark:bg-red-900/30 text-red-700 dark:text-red-400 font-medium';
                } else if (past) {
                    classes += ' text-slate-300 dark:text-slate-600';
                } else {
                    classes += ' text-slate-700 dark:text-slate-300';
                }

                if (todayCell) {
                    classes += ' ring-2 ring-blue-500 ring-inset font-semibold';
                }

                html += '<div class="' + classes + '">' + day + '</div>';
            }

            html += '</div>';
            return html;
        }

        function render() {
            var monthLabel = new Date(currentYear, currentMonth, 1)
                .toLocaleDateString(undefined, { month: 'long', year: 'numeric' });

            var html = '<div class="flex items-center justify-between mb-4">';
            html += '<button type="button" onclick="window._availCalPrev()" class="p-2 rounded-lg hover:bg-slate-100 dark:hover:bg-slate-700 text-slate-500 dark:text-slate-400 transition-colors" aria-label="Previous month"><i class="bi bi-chevron-left"></i></button>';
            html += '<div class="text-sm font-semibold text-slate-700 dark:text-slate-300">' + escHtml(monthLabel) + '</div>';
            html += '<button type="button" onclick="window._availCalNext()" class="p-2 rounded-lg hover:bg-slate-100 dark:hover:bg-slate-700 text-slate-500 dark:text-slate-400 transition-colors" aria-label="Next month"><i class="bi bi-chevron-right"></i></button>';
            html += '</div>';

            html += renderMonth(currentYear, currentMonth);

            container.innerHTML = html;
        }

        window._availCalPrev = function () {
            currentMonth--;
            if (currentMonth < 0) { currentMonth = 11; currentYear--; }
            render();
        };

        window._availCalNext = function () {
            currentMonth++;
            if (currentMonth > 11) { currentMonth = 0; currentYear++; }
            render();
        };

        render();
    }

    // =========================================================
    // Init
    // =========================================================
    document.addEventListener('DOMContentLoaded', function () {
        initMap();
        initShareButton();
        initGalleryEvents();
        initReviewEvents();
        initAvailabilityCalendar();
    });
})();
