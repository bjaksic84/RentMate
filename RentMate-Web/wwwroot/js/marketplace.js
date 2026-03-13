// ==========================================
// Marketplace Page Utilities
// ==========================================

// Mobile filter drawer
function openMobileFilters() {
    document.getElementById('mobileFiltersOverlay').classList.remove('hidden');
    document.getElementById('mobileFiltersDrawer').classList.remove('translate-x-full');
    document.body.style.overflow = 'hidden';
}

function closeMobileFilters() {
    document.getElementById('mobileFiltersOverlay').classList.add('hidden');
    document.getElementById('mobileFiltersDrawer').classList.add('translate-x-full');
    document.body.style.overflow = '';
}

// City search filter
const citySearch = document.getElementById('citySearch');
if (citySearch) {
    citySearch.addEventListener('input', function(e) {
        const query = e.target.value.toLowerCase();
        document.querySelectorAll('.city-option').forEach(function(option) {
            const city = option.dataset.city;
            option.style.display = city.includes(query) ? '' : 'none';
        });
    });
}

// Price range slider
const minSlider = document.getElementById('minPriceSlider');
const maxSlider = document.getElementById('maxPriceSlider');
const minInput = document.getElementById('minPriceInput');
const maxInput = document.getElementById('maxPriceInput');
const minDisplay = document.getElementById('minPriceDisplay');
const maxDisplay = document.getElementById('maxPriceDisplay');
const priceTrack = document.getElementById('priceTrack');
const currencySymbol = (window.MarketplaceConfig && window.MarketplaceConfig.currencySymbol) || '€';

if (minSlider && maxSlider) {
    const maxValue = parseInt(maxSlider.max);

    function updatePriceRange() {
        let minVal = parseInt(minSlider.value);
        let maxVal = parseInt(maxSlider.value);

        if (minVal > maxVal) {
            [minVal, maxVal] = [maxVal, minVal];
        }

        minDisplay.textContent = currencySymbol + minVal;
        maxDisplay.textContent = currencySymbol + maxVal;
        minInput.value = minVal > 0 ? minVal : '';
        maxInput.value = maxVal < maxValue ? maxVal : '';

        const minPercent = (minVal / maxValue) * 100;
        const maxPercent = (maxVal / maxValue) * 100;
        priceTrack.style.left = minPercent + '%';
        priceTrack.style.right = (100 - maxPercent) + '%';
    }

    minSlider.addEventListener('input', updatePriceRange);
    maxSlider.addEventListener('input', updatePriceRange);

    // Initialize
    updatePriceRange();
}

function setPriceRange(min, max) {
    if (minSlider && maxSlider) {
        minSlider.value = min;
        maxSlider.value = max;
        minSlider.dispatchEvent(new Event('input'));
        maxSlider.dispatchEvent(new Event('input'));
    }
}

// Close mobile filters on escape key
document.addEventListener('keydown', function(e) {
    if (e.key === 'Escape') {
        closeMobileFilters();
    }
});

// Helper function to format date as yyyy-MM-dd in local timezone
function formatLocalDate(date) {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
}

// Search clear button
function toggleClearButton(input) {
    const clearBtn = document.getElementById('mktSearchClear');
    if (input.value.length > 0) {
        clearBtn.classList.remove('opacity-0', 'invisible');
        clearBtn.classList.add('opacity-100', 'visible');
    } else {
        clearBtn.classList.add('opacity-0', 'invisible');
        clearBtn.classList.remove('opacity-100', 'visible');
    }
}

function clearSearchInput() {
    const input = document.getElementById('mktSearch');
    input.value = '';
    input.focus();
    toggleClearButton(input);
}

// Initialize clear button
document.addEventListener('DOMContentLoaded', function() {
    const input = document.getElementById('mktSearch');
    if (input) toggleClearButton(input);
});
