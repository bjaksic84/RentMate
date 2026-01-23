/**
 * RentMate Translation Service
 * Provides client-side access to localized strings from the server.
 */
const RentMateTranslations = (function () {
    let translations = {};
    let isLoaded = false;
    let loadPromise = null;

    /**
     * Loads all translations from the server.
     * @returns {Promise} Promise that resolves when translations are loaded.
     */
    async function load() {
        if (loadPromise) {
            return loadPromise;
        }

        loadPromise = fetch('/api/translations')
            .then(response => {
                if (!response.ok) {
                    throw new Error('Failed to load translations');
                }
                return response.json();
            })
            .then(data => {
                translations = data;
                isLoaded = true;
                console.log('Translations loaded successfully');
                return translations;
            })
            .catch(error => {
                console.error('Error loading translations:', error);
                throw error;
            });

        return loadPromise;
    }

    /**
     * Gets a translation by key.
     * @param {string} key - The translation key.
     * @param {string} defaultValue - Default value if key not found.
     * @returns {string} The translated string or the key/default if not found.
     */
    function get(key, defaultValue) {
        if (!isLoaded) {
            console.warn('Translations not loaded yet. Call RentMateTranslations.load() first.');
            return defaultValue || key;
        }
        return translations[key] || defaultValue || key;
    }

    /**
     * Gets multiple translations as an object.
     * @param {string[]} keys - Array of translation keys.
     * @returns {Object} Object with key-value pairs of translations.
     */
    function getMany(keys) {
        const result = {};
        keys.forEach(key => {
            result[key] = get(key);
        });
        return result;
    }

    /**
     * Checks if translations have been loaded.
     * @returns {boolean} True if translations are loaded.
     */
    function loaded() {
        return isLoaded;
    }

    /**
     * Clears cached translations and forces a reload.
     * @returns {Promise} Promise that resolves when translations are reloaded.
     */
    function reload() {
        translations = {};
        isLoaded = false;
        loadPromise = null;
        return load();
    }

    /**
     * Formats a translation string with placeholders.
     * @param {string} key - The translation key.
     * @param {...any} args - Values to replace placeholders {0}, {1}, etc.
     * @returns {string} The formatted translated string.
     */
    function format(key, ...args) {
        let text = get(key);
        args.forEach((arg, index) => {
            text = text.replace(new RegExp(`\\{${index}\\}`, 'g'), arg);
        });
        return text;
    }

    // Public API
    return {
        load,
        get,
        getMany,
        loaded,
        reload,
        format
    };
})();

// Auto-load translations when DOM is ready
document.addEventListener('DOMContentLoaded', function () {
    RentMateTranslations.load().catch(err => {
        console.warn('Could not auto-load translations:', err);
    });
});
