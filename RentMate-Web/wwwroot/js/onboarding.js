/**
 * onboarding.js
 * Spotlight tour for post-onboarding homepage walkthrough.
 * Loaded conditionally when TempData["ShowSpotlightTour"] is set.
 */
(function() {
    'use strict';

    // ── Spotlight Tour ──────────────────────────────────────────────

    window.initSpotlightTour = function(config) {
        if (!config || !config.stops || config.stops.length === 0) return;

        var stops = config.stops;
        var str = config.strings || {};
        var csrfToken = config.csrfToken || '';
        var pendingTimeout = null;
        var overlay = null;
        var hole = null;
        var tooltip = null;
        var sheet = null;
        var isMobile = window.innerWidth < 768;

        function createOverlay() {
            overlay = document.createElement('div');
            overlay.className = 'spotlight-overlay';
            overlay.innerHTML = '<div class="spotlight-overlay-bg"></div>';

            hole = document.createElement('div');
            hole.className = 'spotlight-hole';
            overlay.appendChild(hole);

            // Desktop tooltip
            tooltip = document.createElement('div');
            tooltip.className = 'spotlight-tooltip';
            overlay.appendChild(tooltip);

            // Mobile sheet
            sheet = document.createElement('div');
            sheet.className = 'spotlight-sheet';
            overlay.appendChild(sheet);

            document.body.appendChild(overlay);

            // Click outside to dismiss
            overlay.addEventListener('click', function(e) {
                if (e.target === overlay || e.target.classList.contains('spotlight-overlay-bg')) {
                    completeTour();
                }
            });

            // Escape to dismiss
            document.addEventListener('keydown', handleEscape);
        }

        function handleEscape(e) {
            if (e.key === 'Escape') completeTour();
        }

        function positionHole(el) {
            var rect = el.getBoundingClientRect();
            var padding = 8;
            hole.style.left = (rect.left - padding) + 'px';
            hole.style.top = (rect.top - padding) + 'px';
            hole.style.width = (rect.width + padding * 2) + 'px';
            hole.style.height = (rect.height + padding * 2) + 'px';
            return rect;
        }

        function showStop(index) {
            // Cancel any pending scroll-to-position timeout from previous stop
            if (pendingTimeout) { clearTimeout(pendingTimeout); pendingTimeout = null; }

            var stop = stops[index];
            var el = document.querySelector('[data-spotlight="' + stop.target + '"]');
            if (!el) {
                if (index < stops.length - 1) { showStop(index + 1); return; }
                else { completeTour(); return; }
            }

            // Scroll to element first, then position after scroll settles
            el.scrollIntoView({ behavior: 'smooth', block: 'center' });
            pendingTimeout = setTimeout(function() {
                var rect = positionHole(el);

            var stepLabel = (str.stepXofY || 'Step {0} of {1}').replace('{0}', index + 1).replace('{1}', stops.length);

            function buildContent() {
                var frag = document.createDocumentFragment();

                var badge = document.createElement('div');
                badge.className = 'inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full bg-blue-100 dark:bg-blue-900/40 text-blue-600 dark:text-blue-400 text-xs font-medium mb-2';
                badge.textContent = stepLabel;
                frag.appendChild(badge);

                var heading = document.createElement('h4');
                heading.className = 'font-heading font-bold text-slate-900 dark:text-white mb-1';
                heading.textContent = stop.title || '';
                frag.appendChild(heading);

                var desc = document.createElement('p');
                desc.className = 'text-sm text-slate-500 dark:text-slate-400 mb-4';
                desc.textContent = stop.description || '';
                frag.appendChild(desc);

                var actions = document.createElement('div');
                actions.className = 'flex items-center justify-between';

                var skipBtn = document.createElement('button');
                skipBtn.type = 'button';
                skipBtn.className = 'spotlight-skip text-sm text-slate-500 hover:text-blue-600 transition-colors';
                skipBtn.textContent = str.skipTour || 'Skip tour';
                actions.appendChild(skipBtn);

                var nextBtn = document.createElement('button');
                nextBtn.type = 'button';
                nextBtn.className = 'spotlight-next px-4 py-1.5 bg-gradient-to-r from-blue-600 to-blue-500 hover:from-blue-700 hover:to-blue-600 text-white rounded-lg text-sm font-medium transition-all';
                nextBtn.textContent = index < stops.length - 1 ? (str.next || 'Next') : (str.finish || 'Finish');
                actions.appendChild(nextBtn);

                frag.appendChild(actions);
                return frag;
            }

            var padding = 8;
            isMobile = window.innerWidth < 768;

            if (isMobile) {
                tooltip.classList.remove('visible');
                tooltip.style.display = 'none';
                while (sheet.firstChild) sheet.removeChild(sheet.firstChild);
                sheet.appendChild(buildContent());
                sheet.style.display = '';
                requestAnimationFrame(function() { sheet.classList.add('visible'); });
            } else {
                sheet.classList.remove('visible');
                sheet.style.display = 'none';
                while (tooltip.firstChild) tooltip.removeChild(tooltip.firstChild);
                tooltip.appendChild(buildContent());
                tooltip.style.display = '';

                // Position tooltip relative to element (viewport coordinates)
                tooltip.classList.remove('visible', 'arrow-top', 'arrow-bottom', 'arrow-left', 'arrow-right');

                // Default: below the element
                var top = rect.bottom + padding + 12;
                var left = rect.left;

                // If not enough room below, show above
                if (top + 200 > window.innerHeight) {
                    top = rect.top - padding - 12 - 180;
                    tooltip.classList.add('arrow-bottom');
                } else {
                    tooltip.classList.add('arrow-top');
                }

                // Clamp horizontally
                if (left + 320 > window.innerWidth) {
                    left = window.innerWidth - 330;
                }
                if (left < 10) left = 10;

                tooltip.style.top = top + 'px';
                tooltip.style.left = left + 'px';

                // Position arrow to point at the element center
                var elCenterX = rect.left + rect.width / 2;
                var arrowPos = Math.max(16, Math.min(elCenterX - left, 304));
                tooltip.style.setProperty('--arrow-offset', arrowPos + 'px');

                requestAnimationFrame(function() { tooltip.classList.add('visible'); });
            }

            // Bind buttons
            var container = isMobile ? sheet : tooltip;
            container.querySelector('.spotlight-skip').addEventListener('click', completeTour);
            container.querySelector('.spotlight-next').addEventListener('click', function() {
                if (index < stops.length - 1) {
                    showStop(index + 1);
                } else {
                    completeTour();
                }
            });
            }, 400); // wait for scrollIntoView to settle
        }

        function completeTour() {
            // Fade out
            if (overlay) {
                overlay.style.transition = 'opacity 400ms ease';
                overlay.style.opacity = '0';
                setTimeout(function() {
                    if (overlay && overlay.parentNode) overlay.parentNode.removeChild(overlay);
                }, 400);
            }

            document.removeEventListener('keydown', handleEscape);

            // Persist to server
            fetch('/Onboarding/MarkSpotlightComplete', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'X-CSRF-TOKEN': csrfToken || (document.querySelector('input[name="__RequestVerificationToken"]') || {}).value || ''
                }
            }).catch(function() { /* silent */ });
        }

        // Start the tour
        setTimeout(function() {
            createOverlay();
            showStop(0);
        }, 500);
    };
})();
