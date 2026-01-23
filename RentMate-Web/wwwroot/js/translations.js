/**
 * Translations Helper (Typed Proxy)
 * 
 * Provides a cleaner way to access translations on the client side.
 * Usage: T.Title_Required or T.format("Welcome {0}", "User")
 */
window.Translations = window.Translations || {};

window.T = new Proxy(window.Translations, {
    get: function (target, prop) {
        if (prop === 'format') {
            return function (key, ...args) {
                let val = target[key] || key;
                return val.replace(/{(\d+)}/g, function (match, number) {
                    return typeof args[number] != 'undefined' ? args[number] : match;
                });
            };
        }
        return target[prop] || prop;
    }
});
