// Site-wide helpers: toast, rent picker init, small UI utilities
(function () {
	// --- Client-Side Translation API ---
	window.Translations = {}; // Global storage

	async function loadTranslations() {
		try {
			const cached = sessionStorage.getItem('rentmate_translations');
			const cachedVersion = sessionStorage.getItem('rentmate_translations_version');

			let url = '/api/translations';
			if (cachedVersion) {
				url += `?v=${cachedVersion}`;
			}

			const response = await fetch(url);
			if (response.status === 304 && cached) {
				window.Translations = JSON.parse(cached);
				return;
			}

			if (response.ok) {
				const data = await response.json();
				window.Translations = data.translations;
				sessionStorage.setItem('rentmate_translations', JSON.stringify(window.Translations));
				sessionStorage.setItem('rentmate_translations_version', data.version);
			} else if (cached) {
				window.Translations = JSON.parse(cached);
			}
		} catch (err) {
			console.error('Failed to load translations:', err);
		}
	}
	loadTranslations(); // Trigger on load

	function showToast(message, type = 'info') {
		const toastContainerId = 'toastContainer';
		let container = document.getElementById(toastContainerId);
		if (!container) {
			container = document.createElement('div');
			container.id = toastContainerId;
			container.className = 'toast-container position-fixed top-0 end-0 p-3';
			document.body.appendChild(container);
		}

		const toastEl = document.createElement('div');
		toastEl.className = `toast align-items-center text-bg-${type} border-0 show mb-2`;
		toastEl.setAttribute('role', 'alert');
		toastEl.innerHTML = `
			<div class="d-flex">
				<div class="toast-body">${message}</div>
				<button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button>
			</div>
		`;
		container.appendChild(toastEl);

		const toast = new bootstrap.Toast(toastEl, { delay: 3000 });
		toast.show();
		toastEl.addEventListener('hidden.bs.toast', () => toastEl.remove());
	}

	// Initialize rent date pickers on elements with .rent-daterange-picker
	function initRentPickers() {
		document.querySelectorAll('.rent-daterange-picker').forEach(function (element) {
			try {
				const blockedAttr = element.getAttribute('data-blocked') || '[]';
				const blocked = JSON.parse(blockedAttr);
				const form = element.closest('form');
				const startInput = form ? form.querySelector('.start-date-input') : null;
				const endInput = form ? form.querySelector('.end-date-input') : null;

				// Determine id suffix for modal-specific calc elements (if present)
				const modal = element.closest('.modal');
				const idSuffix = modal ? modal.id.replace('rentModal-', '') : null;
				const calcWrapper = idSuffix ? document.getElementById('modalPriceCalculation-' + idSuffix) : document.getElementById('modalPriceCalculation');
				const calcDays = idSuffix ? document.getElementById('modalCalcDays-' + idSuffix) : document.getElementById('modalCalcDays');
				const calcTotalBase = idSuffix ? document.getElementById('modalCalcTotalBase-' + idSuffix) : document.getElementById('modalCalcTotalBase');
				const calcFinalTotal = idSuffix ? document.getElementById('modalCalcFinalTotal-' + idSuffix) : document.getElementById('modalCalcFinalTotal');

				const pricePerDay = parseFloat((form && form.dataset.price) || element.getAttribute('data-price') || 0);

				flatpickr(element, {
					mode: 'range',
					dateFormat: 'Y-m-d',
					minDate: 'today',
					disable: blocked,
					locale: { firstDayOfWeek: 1 },
					onChange: function (selectedDates, dateStr, instance) {
						if (selectedDates.length === 2) {
							const start = selectedDates[0];
							const end = selectedDates[1];

							if (startInput) startInput.value = instance.formatDate(start, 'Y-m-d');
							if (endInput) endInput.value = instance.formatDate(end, 'Y-m-d');

							// calc days inclusive
							const diffTime = Math.abs(end - start);
							const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24)) + 1;

							const basePrice = (isNaN(pricePerDay) ? 0 : pricePerDay);
							const exchangeRate = window.CurrentCurrency ? window.CurrentCurrency.ExchangeRate : 1.0;
							const symbol = window.CurrentCurrency ? window.CurrentCurrency.Symbol : '€';

							const totalConverted = diffDays * basePrice * exchangeRate;
							const formattedPrice = window.CurrentCurrency ? (window.CurrentCurrency.Code === "CHF" ? `${(basePrice * exchangeRate).toFixed(2)} ${symbol}` : `${symbol}${(basePrice * exchangeRate).toFixed(2)}`) : `${basePrice}€`;

							if (calcWrapper) {
								if (calcDays) {
									const daysText = window.Translations && window.Translations["days"] ? window.Translations["days"] : "days";
									calcDays.innerText = `${diffDays} ${daysText} x ${formattedPrice}`;
								}
								if (calcTotalBase) {
									calcTotalBase.innerText = window.CurrentCurrency?.Code === "CHF" ? `${totalConverted.toFixed(2)} ${symbol}` : `${symbol}${totalConverted.toFixed(2)}`;
								}
								if (calcFinalTotal) {
									calcFinalTotal.innerText = window.CurrentCurrency?.Code === "CHF" ? `${totalConverted.toFixed(2)} ${symbol}` : `${symbol}${totalConverted.toFixed(2)}`;
								}
								calcWrapper.classList.remove('d-none');
							}
						}
					}
				});
			} catch (err) {
				console.error('Failed initializing rent picker:', err);
			}
		});
	}

	// Lazy image helper: add loading=lazy to all product images if not present
	function enableLazyImages() {
		document.querySelectorAll('img').forEach(img => {
			if (!img.hasAttribute('loading')) img.setAttribute('loading', 'lazy');
		});
	}

	document.addEventListener('DOMContentLoaded', function () {
		initRentPickers();
		enableLazyImages();
		initTheme();
		window.showToast = showToast; // expose for inline scripts
	});

	// (floating-label autofill handling reverted)
})();

/* Theme handling: setTheme / initTheme */
function setTheme(theme) {
	try {
		document.documentElement.setAttribute('data-theme', theme);
		localStorage.setItem('theme', theme);
		const icon = document.getElementById('themeIcon');
		if (icon) icon.className = theme === 'dark' ? 'bi bi-sun-fill' : 'bi bi-moon-fill';
	} catch (e) {
		console.error('setTheme error', e);
	}
}

function initTheme() {
	try {
		const stored = localStorage.getItem('theme');
		let theme = stored;
		if (!theme) {
			theme = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
		}
		setTheme(theme);

		const toggle = document.getElementById('themeToggle');
		if (toggle) {
			toggle.addEventListener('click', function () {
				const current = document.documentElement.getAttribute('data-theme') === 'dark' ? 'dark' : 'light';
				setTheme(current === 'dark' ? 'light' : 'dark');
			});
		}
	} catch (e) {
		console.error('initTheme error', e);
	}
}
