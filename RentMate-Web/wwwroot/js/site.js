/**
 * RentMate Site-wide Utilities
 * Handles: toasts, translations, lazy loading, and UI helpers
 */
(function () {
	'use strict';

	// ==========================================
	// Client-Side Translation API
	// ==========================================
	window.Translations = {};

	const TRANSLATION_CACHE_KEY = 'rentmate_translations';
	const TRANSLATION_VERSION_KEY = 'rentmate_translations_version';

	async function loadTranslations() {
		try {
			const cached = sessionStorage.getItem(TRANSLATION_CACHE_KEY);
			const cachedVersion = sessionStorage.getItem(TRANSLATION_VERSION_KEY);

			const url = cachedVersion ? `/api/translations?v=${cachedVersion}` : '/api/translations';
			const response = await fetch(url);

			if (response.status === 304 && cached) {
				window.Translations = JSON.parse(cached);
				return;
			}

			if (response.ok) {
				const data = await response.json();
				window.Translations = data.translations;
				sessionStorage.setItem(TRANSLATION_CACHE_KEY, JSON.stringify(window.Translations));
				sessionStorage.setItem(TRANSLATION_VERSION_KEY, data.version);
			} else if (cached) {
				window.Translations = JSON.parse(cached);
			}
		} catch (err) {
			console.error('Failed to load translations:', err);
			// Attempt to use cached translations on error
			const cached = sessionStorage.getItem(TRANSLATION_CACHE_KEY);
			if (cached) {
				window.Translations = JSON.parse(cached);
			}
		}
	}

	// ==========================================
	// Toast Notification System (Tailwind CSS)
	// ==========================================
	const TOAST_CONTAINER_ID = 'toastContainer';
	const TOAST_DURATION = 3000;

	const TOAST_STYLES = {
		success: { bg: 'bg-green-500', icon: 'bi-check-circle' },
		error: { bg: 'bg-red-500', icon: 'bi-x-circle' },
		warning: { bg: 'bg-amber-500', icon: 'bi-exclamation-triangle' },
		info: { bg: 'bg-blue-500', icon: 'bi-info-circle' }
	};

	function getOrCreateToastContainer() {
		let container = document.getElementById(TOAST_CONTAINER_ID);
		if (!container) {
			container = document.createElement('div');
			container.id = TOAST_CONTAINER_ID;
			container.className = 'fixed top-4 right-4 z-50 flex flex-col gap-2';
			container.setAttribute('aria-live', 'polite');
			container.setAttribute('aria-label', 'Notifications');
			document.body.appendChild(container);
		}
		return container;
	}

	function showToast(message, type = 'info') {
		const container = getOrCreateToastContainer();
		const style = TOAST_STYLES[type] || TOAST_STYLES.info;

		const toast = document.createElement('div');
		toast.className = `${style.bg} text-white px-4 py-3 rounded-xl shadow-lg flex items-center gap-3 transform translate-x-full opacity-0 transition-all duration-300`;
		toast.setAttribute('role', 'alert');
		toast.innerHTML = `
			<i class="bi ${style.icon}"></i>
			<span class="text-sm font-medium">${escapeHtml(message)}</span>
			<button type="button" class="ml-2 hover:opacity-70 transition-opacity" aria-label="Dismiss">
				<i class="bi bi-x"></i>
			</button>
		`;

		// Close button handler
		toast.querySelector('button').addEventListener('click', () => dismissToast(toast));

		container.appendChild(toast);

		// Animate in
		requestAnimationFrame(() => {
			toast.classList.remove('translate-x-full', 'opacity-0');
		});

		// Auto-dismiss
		setTimeout(() => dismissToast(toast), TOAST_DURATION);
	}

	function dismissToast(toast) {
		toast.classList.add('translate-x-full', 'opacity-0');
		setTimeout(() => toast.remove(), 300);
	}

	function escapeHtml(text) {
		const div = document.createElement('div');
		div.textContent = text;
		return div.innerHTML;
	}

	// ==========================================
	// Lazy Image Loading
	// ==========================================
	function enableLazyImages() {
		document.querySelectorAll('img:not([loading])').forEach(img => {
			img.setAttribute('loading', 'lazy');
		});
	}

	// ==========================================
	// Initialization
	// ==========================================
	function init() {
		enableLazyImages();
		window.showToast = showToast;
	}

	// Load translations immediately
	loadTranslations();

	// Initialize on DOM ready
	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', init);
	} else {
		init();
	}
})();
