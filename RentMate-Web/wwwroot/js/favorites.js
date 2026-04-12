/**
 * Favorites functionality with optimistic UI updates
 */
(function () {
    'use strict';

    // Debounce to prevent rapid clicking
    const pendingRequests = new Map();

    /**
     * Toggle favorite status for an item
     * @param {HTMLElement} button - The favorite button element
     */
    async function toggleFavorite(button) {
        const itemId = button.dataset.itemId;
        
        // Prevent duplicate requests for the same item
        if (pendingRequests.has(itemId)) {
            return;
        }

        const currentState = button.dataset.favorited === 'true';
        const newState = !currentState;

        // Get icon elements
        const outlineIcon = button.querySelector('.favorite-icon-outline');
        const filledIcon = button.querySelector('.favorite-icon-filled');
        const loadingIcon = button.querySelector('.favorite-icon-loading');

        // Optimistic UI update - change immediately
        updateButtonState(button, outlineIcon, filledIcon, newState);

        // Show subtle loading indicator (optional - keep icons visible)
        button.classList.add('pointer-events-none');
        
        pendingRequests.set(itemId, true);

        try {
            const token = window.getToken();
            const headers = {
                'Content-Type': 'application/json'
            };
            
            // Add anti-forgery token if available (header name configured in Program.cs)
            if (token) {
                headers['X-CSRF-TOKEN'] = token;
            }
            
            const response = await fetch(`/api/FavoritesApi/toggle/${itemId}`, {
                method: 'POST',
                headers: headers,
                credentials: 'same-origin'
            });

            if (!response.ok) {
                // Handle authentication redirect
                if (response.status === 401) {
                    // Revert optimistic update
                    updateButtonState(button, outlineIcon, filledIcon, currentState);
                    
                    // Optionally redirect to login or show message
                    showToast('Please sign in to save favorites', 'warning');
                    return;
                }
                throw new Error(`HTTP error! status: ${response.status}`);
            }

            const data = await response.json();

            if (data.success) {
                // Confirm the state matches server response
                updateButtonState(button, outlineIcon, filledIcon, data.isFavorited);
                
                // If unfavorited and we're on the dashboard, remove the card with animation
                if (!data.isFavorited) {
                    const card = button.closest('[data-favorite-card]');
                    if (card) {
                        card.classList.add('opacity-0', 'scale-95', 'transition-all', 'duration-300');
                        setTimeout(() => {
                            card.remove();
                            // Update the favorites count in stats if it exists
                            updateFavoritesCount(-1);
                        }, 300);
                    }
                }
                
                // Show feedback
                const message = data.isFavorited ? 'Added to favorites' : 'Removed from favorites';
                showToast(message, 'success');
            } else {
                // Revert on failure
                updateButtonState(button, outlineIcon, filledIcon, currentState);
                showToast('Failed to update favorite', 'error');
            }
        } catch (error) {
            console.error('Error toggling favorite:', error);
            
            // Revert optimistic update on error
            updateButtonState(button, outlineIcon, filledIcon, currentState);
            showToast('Something went wrong. Please try again.', 'error');
        } finally {
            button.classList.remove('pointer-events-none');
            pendingRequests.delete(itemId);
        }
    }

    /**
     * Update the favorites count displayed in stats
     */
    function updateFavoritesCount(delta) {
        const countElement = document.querySelector('[data-favorites-count]');
        if (countElement) {
            const currentCount = parseInt(countElement.textContent, 10) || 0;
            const newCount = Math.max(0, currentCount + delta);
            countElement.textContent = newCount;
        }
    }

    /**
     * Update the button's visual state
     */
    function updateButtonState(button, outlineIcon, filledIcon, isFavorited) {
        button.dataset.favorited = isFavorited.toString();
        button.setAttribute('aria-label', isFavorited ? 'Remove from favorites' : 'Add to favorites');
        button.setAttribute('title', isFavorited ? 'Remove from favorites' : 'Add to favorites');

        if (isFavorited) {
            outlineIcon?.classList.add('hidden');
            filledIcon?.classList.remove('hidden');
        } else {
            outlineIcon?.classList.remove('hidden');
            filledIcon?.classList.add('hidden');
        }

        // Add a brief scale animation for feedback
        button.classList.add('scale-125');
        setTimeout(() => button.classList.remove('scale-125'), 150);
    }

    /**
     * Initialize favorite buttons
     */
    function initFavoriteButtons() {
        document.addEventListener('click', function (e) {
            const button = e.target.closest('.favorite-btn');
            if (button) {
                e.preventDefault();
                e.stopPropagation();
                toggleFavorite(button);
            }
        });
    }

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initFavoriteButtons);
    } else {
        initFavoriteButtons();
    }

    // Expose for external use if needed
    window.FavoritesManager = {
        toggle: toggleFavorite
    };
})();
