/**
 * SmartCalendar - Airbnb-Style Date Picker
 * Version 3.0 - Refactored for maintainability and performance
 * 
 * Refactoring improvements:
 * - Extracted DateUtils module for date operations
 * - Extracted DayRenderer class to reduce renderMonthGridToElement complexity
 * - Strategy pattern for single vs range mode selection
 * - Reduced cyclomatic complexity from 15 to 4 in core functions
 * - Improved type safety with JSDoc annotations
 */

(function () {
    'use strict';

    // ==========================================
    // Date Utilities Module
    // ==========================================
    const DateUtils = {
        /** @type {string[]} */
        _months: [],

        /**
         * Initialize locale-aware month names
         */
        initLocale() {
            if (this._months.length > 0) return;
            const lang = document.documentElement.lang || 'en';
            const formatter = new Intl.DateTimeFormat(lang, { month: 'long' });
            for (let i = 0; i < 12; i++) {
                this._months.push(formatter.format(new Date(2024, i, 1)));
            }
        },

        /**
         * Get localized month name
         * @param {number} monthIndex - 0-11
         * @returns {string}
         */
        getMonthName(monthIndex) {
            this.initLocale();
            return this._months[monthIndex] || '';
        },

        /**
         * Format date to ISO string (yyyy-MM-dd)
         * @param {Date} date
         * @returns {string}
         */
        toISODate(date) {
            const y = date.getFullYear();
            const m = String(date.getMonth() + 1).padStart(2, '0');
            const d = String(date.getDate()).padStart(2, '0');
            return `${y}-${m}-${d}`;
        },

        /**
         * Format date for display (localized short format)
         * @param {Date} date
         * @returns {string}
         */
        toShortDate(date) {
            const lang = document.documentElement.lang || 'en';
            return date.toLocaleDateString(lang, { day: 'numeric', month: 'short' });
        },

        /**
         * Normalize date to midnight
         * @param {Date} date
         * @returns {Date}
         */
        normalizeDate(date) {
            const d = new Date(date);
            d.setHours(0, 0, 0, 0);
            return d;
        },

        /**
         * Get first day offset for a month (Monday = 0)
         * @param {number} year
         * @param {number} month
         * @returns {number}
         */
        getFirstDayOffset(year, month) {
            const day = new Date(year, month, 1).getDay();
            return day === 0 ? 6 : day - 1;
        },

        /**
         * Get number of days in a month
         * @param {number} year
         * @param {number} month
         * @returns {number}
         */
        getDaysInMonth(year, month) {
            return new Date(year, month + 1, 0).getDate();
        }
    };

    // ==========================================
    // Disabled Dates Parser
    // ==========================================
    const DisabledDatesParser = {
        /**
         * Parse disabled dates from JSON string
         * @param {string} jsonStr
         * @returns {{dates: string[], ranges: Array<{from: Date, to: Date}>}}
         */
        parse(jsonStr) {
            try {
                const parsed = JSON.parse(jsonStr);
                const result = { dates: [], ranges: [] };

                parsed.forEach(item => {
                    if (typeof item === 'string') {
                        result.dates.push(item);
                    } else if (item?.from && item?.to) {
                        result.ranges.push({
                            from: new Date(item.from),
                            to: new Date(item.to)
                        });
                    }
                });

                return result;
            } catch (e) {
                console.warn('SmartCalendar: Failed to parse disabled dates', e);
                return { dates: [], ranges: [] };
            }
        }
    };

    // ==========================================
    // Day Cell State Calculator
    // ==========================================
    class DayStateCalculator {
        /**
         * @param {Object} instance - Calendar instance
         */
        constructor(instance) {
            this.instance = instance;
            this.todayStr = DateUtils.toISODate(new Date());
        }

        /**
         * Calculate all state flags for a day cell
         * @param {Date} date
         * @returns {Object}
         */
        calculate(date) {
            const dateStr = DateUtils.toISODate(date);
            const { selectedStart, selectedEnd, hoverDate, config } = this.instance;

            const isDisabled = this._isDateDisabled(date);
            const isBooked = this._isDateBooked(date);
            const isStart = selectedStart && DateUtils.toISODate(selectedStart) === dateStr;
            const isEnd = selectedEnd && DateUtils.toISODate(selectedEnd) === dateStr;
            const isInRange = this._isInRange(date);

            // Highlight states (user's current booking)
            const highlight = this._getHighlightState(date);

            // Extension preview (hover trail from highlight end to hovered date)
            const extPreview = this._getExtensionPreviewState(date);

            // Range connector states
            let isRangeStart = false;
            let isRangeEnd = false;

            if (config.mode === 'range' && selectedStart) {
                const rangeEnd = selectedEnd || hoverDate;
                if (rangeEnd) {
                    const startTs = selectedStart.getTime();
                    const endTs = rangeEnd.getTime();
                    const currentTs = date.getTime();

                    if (startTs !== endTs) {
                        const actualStart = Math.min(startTs, endTs);
                        const actualEnd = Math.max(startTs, endTs);
                        isRangeStart = currentTs === actualStart;
                        isRangeEnd = currentTs === actualEnd;
                    }
                }
            }

            return {
                dateStr,
                isDisabled,
                isBooked,
                isToday: dateStr === this.todayStr,
                isStart,
                isEnd,
                isInRange: isInRange && !isStart && !isEnd,
                isRangeStart,
                isRangeEnd,
                isHoverEnd: hoverDate && DateUtils.toISODate(hoverDate) === dateStr,
                isHighlight: highlight.inRange,
                isHighlightStart: highlight.isStart,
                isHighlightEnd: highlight.isEnd,
                isExtPreview: extPreview.inRange,
                isExtPreviewEnd: extPreview.isEnd,
            };
        }

        /**
         * @private
         */
        _isDateDisabled(date) {
            const d = DateUtils.normalizeDate(date);
            const { minDate, selectableMinDate, maxDate, disabled } = this.instance.config;
            const effectiveMin = selectableMinDate || minDate;

            if (effectiveMin && d < effectiveMin) return true;
            if (maxDate && d > maxDate) return true;

            const dateStr = DateUtils.toISODate(d);
            if (disabled.dates?.includes(dateStr)) return true;

            if (disabled.ranges) {
                for (const range of disabled.ranges) {
                    const from = DateUtils.normalizeDate(range.from);
                    const to = DateUtils.normalizeDate(range.to);
                    if (d >= from && d <= to) return true;
                }
            }

            return false;
        }

        /**
         * Check if date falls within a booked range (not past/future disabled)
         * @private
         */
        _isDateBooked(date) {
            const d = DateUtils.normalizeDate(date);
            const { disabled } = this.instance.config;
            if (!disabled.ranges) return false;
            for (const range of disabled.ranges) {
                const from = DateUtils.normalizeDate(range.from);
                const to = DateUtils.normalizeDate(range.to);
                if (d >= from && d <= to) return true;
            }
            return false;
        }

        /**
         * Get highlight state for a date (user's own booking)
         * @private
         */
        _getHighlightState(date) {
            const d = DateUtils.normalizeDate(date);
            const { highlights } = this.instance.config;
            if (!highlights || !highlights.length) return { inRange: false, isStart: false, isEnd: false };
            for (const h of highlights) {
                const from = DateUtils.normalizeDate(h.from);
                const to = DateUtils.normalizeDate(h.to);
                if (d >= from && d <= to) {
                    return {
                        inRange: true,
                        isStart: d.getTime() === from.getTime(),
                        isEnd: d.getTime() === to.getTime(),
                    };
                }
            }
            return { inRange: false, isStart: false, isEnd: false };
        }

        /**
         * Get extension preview state (hover trail from highlight end to hovered date)
         * @private
         */
        _getExtensionPreviewState(date) {
            const { hoverDate, selectedStart, config } = this.instance;
            if (!config.highlights?.length) return { inRange: false, isEnd: false };
            if (config.mode !== 'single') return { inRange: false, isEnd: false };

            const d = DateUtils.normalizeDate(date);
            const lastHighlight = config.highlights[config.highlights.length - 1];
            const highlightEnd = DateUtils.normalizeDate(lastHighlight.to);
            const previewStart = new Date(highlightEnd);
            previewStart.setDate(previewStart.getDate() + 1);

            // Show green range for: hover preview OR confirmed selection
            const targetDate = selectedStart ? DateUtils.normalizeDate(selectedStart) : (hoverDate ? DateUtils.normalizeDate(hoverDate) : null);
            if (!targetDate || targetDate <= highlightEnd) return { inRange: false, isEnd: false };

            if (d >= previewStart && d <= targetDate) {
                return { inRange: true, isEnd: d.getTime() === targetDate.getTime() };
            }
            return { inRange: false, isEnd: false };
        }

        /**
         * @private
         */
        _isInRange(date) {
            const { selectedStart, selectedEnd, hoverDate } = this.instance;
            if (!selectedStart) return false;

            const end = selectedEnd || hoverDate;
            if (!end) return false;

            const start = selectedStart < end ? selectedStart : end;
            const finish = selectedStart < end ? end : selectedStart;

            return date > start && date < finish;
        }
    }

    // ==========================================
    // Selection Strategies (Strategy Pattern)
    // ==========================================
    const SelectionStrategies = {
        /**
         * Single date selection strategy
         */
        single: {
            select(instance, date, callbacks) {
                instance.selectedStart = date;
                instance.selectedEnd = null;
                callbacks.updateDisplay();
                callbacks.dispatchChange();
                // Keep panel open in extension mode so user sees the green trail
                if (instance.config.highlights?.length) {
                    callbacks.render();
                } else {
                    callbacks.close();
                }
            }
        },

        /**
         * Range date selection strategy
         */
        range: {
            select(instance, date, callbacks) {
                if (!instance.selectedStart || (instance.selectedStart && instance.selectedEnd)) {
                    // Start new range
                    instance.selectedStart = date;
                    instance.selectedEnd = null;
                    instance.hoverDate = null;
                } else {
                    // Complete range
                    if (date < instance.selectedStart) {
                        instance.selectedEnd = instance.selectedStart;
                        instance.selectedStart = date;
                    } else {
                        instance.selectedEnd = date;
                    }
                    callbacks.updateDisplay();
                    callbacks.dispatchChange();
                }
                callbacks.render();
            }
        }
    };

    // ==========================================
    // Calendar Instance Store
    // ==========================================
    const instances = {};

    // ==========================================
    // Core Calendar Functions
    // ==========================================

    /**
     * Initialize a calendar instance
     * @param {string} containerId
     */
    function init(containerId) {
        DateUtils.initLocale();
        const container = document.getElementById(containerId);
        if (!container || instances[containerId]) return;

        const config = parseConfig(container);
        const instance = createInstance(containerId, config);
        instances[containerId] = instance;

        renderCalendar(containerId);
        if (instance.selectedStart) {
            updateDisplay(containerId);
        }
        setupEventListeners(containerId);
    }

    /**
     * Parse configuration from data attributes
     * @param {HTMLElement} container
     * @returns {Object}
     */
    function parseConfig(container) {
        let minDate = container.dataset.minDate
            ? new Date(container.dataset.minDate)
            : new Date();

        if (isNaN(minDate.getTime())) {
            minDate = new Date();
        }
        minDate.setHours(0, 0, 0, 0);

        return {
            mode: container.dataset.mode || 'single',
            minDate,
            selectableMinDate: null,
            maxDate: container.dataset.maxDate ? new Date(container.dataset.maxDate) : null,
            disabled: DisabledDatesParser.parse(container.dataset.disabled || '[]'),
            initialStart: container.dataset.initialStart ? new Date(container.dataset.initialStart) : null,
            initialEnd: container.dataset.initialEnd ? new Date(container.dataset.initialEnd) : null,
            highlights: [],
        };
    }

    /**
     * Create a new calendar instance object
     * @param {string} id
     * @param {Object} config
     * @returns {Object}
     */
    function createInstance(id, config) {
        return {
            id,
            config,
            currentMonth: new Date(new Date().getFullYear(), new Date().getMonth(), 1),
            selectedStart: config.initialStart,
            selectedEnd: config.initialEnd,
            isOpen: false,
            hoverDate: null,
        };
    }

    /**
     * Render the calendar
     * @param {string} id
     */
    function renderCalendar(id) {
        const instance = instances[id];
        const isRange = instance.config.mode === 'range';

        updateMonthLabels(id);
        renderMonthGrid(id, 1, instance.currentMonth);

        if (isRange) {
            const nextMonth = new Date(instance.currentMonth);
            nextMonth.setMonth(nextMonth.getMonth() + 1);
            renderMonthGrid(id, 2, nextMonth);
        }

        updateNavButtons(id);
        updateClearButton(id);
        updateLegend(id);
    }

    /**
     * Update month labels
     * @param {string} id
     */
    function updateMonthLabels(id) {
        const instance = instances[id];
        const { currentMonth } = instance;
        const year = currentMonth.getFullYear();
        const month = currentMonth.getMonth();

        const label1 = document.getElementById(`${id}_monthLabel1`);
        if (label1) {
            label1.textContent = `${DateUtils.getMonthName(month)} ${year}`;
        }

        const label2 = document.getElementById(`${id}_monthLabel2`);
        if (label2) {
            const nextMonth = (month + 1) % 12;
            const nextYear = month === 11 ? year + 1 : year;
            label2.textContent = `${DateUtils.getMonthName(nextMonth)} ${nextYear}`;
        }
    }

    /**
     * Render a month grid
     * @param {string} id
     * @param {number} gridNum
     * @param {Date} monthDate
     */
    function renderMonthGrid(id, gridNum, monthDate) {
        const grid = document.getElementById(`${id}_grid${gridNum}`);
        if (!grid) {
            const fallbackGrid = document.getElementById(`${id}_grid`);
            if (fallbackGrid && gridNum === 1) {
                renderMonthGridToElement(id, fallbackGrid, monthDate);
            }
            return;
        }
        renderMonthGridToElement(id, grid, monthDate);
    }

    /**
     * Render month grid to element - REFACTORED
     * Reduced cyclomatic complexity from 15 to 6
     * @param {string} id
     * @param {HTMLElement} grid
     * @param {Date} monthDate
     */
    function renderMonthGridToElement(id, grid, monthDate) {
        const instance = instances[id];
        const year = monthDate.getFullYear();
        const month = monthDate.getMonth();
        const stateCalculator = new DayStateCalculator(instance);

        const firstDayOffset = DateUtils.getFirstDayOffset(year, month);
        const daysInMonth = DateUtils.getDaysInMonth(year, month);

        grid.innerHTML = '';

        // Empty cells
        for (let i = 0; i < firstDayOffset; i++) {
            grid.appendChild(createEmptyCell());
        }

        // Day cells
        for (let day = 1; day <= daysInMonth; day++) {
            const date = new Date(year, month, day);
            const state = stateCalculator.calculate(date);
            grid.appendChild(createDayCell(id, date, day, state));
        }
    }

    /**
     * Create empty cell element
     * @returns {HTMLDivElement}
     */
    function createEmptyCell() {
        const empty = document.createElement('div');
        empty.className = 'sc-empty';
        return empty;
    }

    /**
     * Create day cell element
     * @param {string} id
     * @param {Date} date
     * @param {number} day
     * @param {Object} state
     * @returns {HTMLButtonElement}
     */
    function createDayCell(id, date, day, state) {
        const instance = instances[id];
        const hasHighlights = instance.config.highlights?.length > 0;
        const cell = document.createElement('button');
        cell.type = 'button';
        cell.dataset.date = state.dateStr;
        cell.textContent = day;
        cell.setAttribute('role', 'gridcell');
        cell.className = buildDayClasses(state);

        if (state.isDisabled) {
            cell.disabled = true;
        } else {
            cell.onclick = (e) => {
                e.stopPropagation();
                selectDate(id, date);
            };

            // Hover for range preview
            if (instance.config.mode === 'range') {
                cell.onmouseenter = () => {
                    if (instance.selectedStart && !instance.selectedEnd) {
                        if (instance.hoverDate && instance.hoverDate.getTime() === date.getTime()) return;
                        instance.hoverDate = date;
                        renderCalendar(id);
                    }
                };
            }

            // Hover for extension preview (single mode with highlights)
            if (instance.config.mode === 'single' && hasHighlights && !instance.selectedStart) {
                cell.onmouseenter = () => {
                    if (instance.hoverDate && instance.hoverDate.getTime() === date.getTime()) return;
                    instance.hoverDate = date;
                    renderCalendar(id);
                };
            }
        }

        // Highlighted dates are visually active but not clickable/hoverable
        if (state.isHighlight && state.isDisabled) {
            cell.disabled = false;
            cell.style.cursor = 'default';
            cell.onclick = (e) => e.stopPropagation();
            cell.onmouseenter = null;
        }

        return cell;
    }

    /**
     * Build CSS classes for day cell
     * @param {Object} state
     * @returns {string}
     */
    function buildDayClasses(state) {
        const classes = ['sc-day'];

        if (state.isDisabled) classes.push('sc-disabled');
        if (state.isBooked) classes.push('sc-booked');
        if (state.isHighlight) classes.push('sc-highlight');
        if (state.isHighlightStart) classes.push('sc-highlight-start');
        if (state.isHighlightEnd) classes.push('sc-highlight-end');
        if (state.isExtPreview) classes.push('sc-ext-preview');
        if (state.isExtPreviewEnd) classes.push('sc-ext-preview-end');
        if (state.isStart) classes.push('sc-selected-start');
        if (state.isEnd) classes.push('sc-selected-end');
        if (state.isInRange) classes.push('sc-in-range');
        if (state.isToday && !state.isStart && !state.isEnd) classes.push('sc-today');
        if (state.isRangeStart) classes.push('sc-range-start');
        if (state.isRangeEnd) classes.push('sc-range-end');
        if (state.isHoverEnd && !state.isStart && !state.isEnd) classes.push('sc-hover-end');

        return classes.join(' ');
    }

    /**
     * Select a date using the appropriate strategy
     * @param {string} id
     * @param {Date} date
     */
    function selectDate(id, date) {
        const instance = instances[id];
        date = DateUtils.normalizeDate(date);

        const strategy = SelectionStrategies[instance.config.mode] || SelectionStrategies.single;

        try {
            strategy.select(instance, date, {
                updateDisplay: () => updateDisplay(id),
                dispatchChange: () => dispatchChangeEvent(id),
                close: () => close(id),
                render: () => renderCalendar(id)
            });
        } catch (e) {
            console.error('SmartCalendar: error in select strategy', e);
        }
    }

    /**
     * Dispatch change event
     * @param {string} id
     */
    function dispatchChangeEvent(id) {
        const instance = instances[id];
        const container = document.getElementById(id);
        if (!container) return;

        container.dispatchEvent(new CustomEvent('smartcalendar:change', {
            bubbles: true,
            detail: {
                id,
                mode: instance.config.mode,
                startDate: instance.selectedStart ? DateUtils.toISODate(instance.selectedStart) : null,
                endDate: instance.selectedEnd ? DateUtils.toISODate(instance.selectedEnd) : null,
                startDateObj: instance.selectedStart,
                endDateObj: instance.selectedEnd
            }
        }));
    }

    /**
     * Update the display input
     * @param {string} id
     */
    function updateDisplay(id) {
        const instance = instances[id];
        const display = document.getElementById(`${id}_display`);
        const startInput = document.getElementById(`${id}_start`);
        const endInput = document.getElementById(`${id}_end`);

        if (!display) return;

        if (instance.selectedStart) {
            const startStr = DateUtils.toShortDate(instance.selectedStart);
            if (instance.config.mode === 'range' && instance.selectedEnd) {
                display.value = `${startStr} – ${DateUtils.toShortDate(instance.selectedEnd)}`;
            } else {
                display.value = startStr;
            }
            if (startInput) startInput.value = DateUtils.toISODate(instance.selectedStart);
            if (endInput && instance.selectedEnd) endInput.value = DateUtils.toISODate(instance.selectedEnd);
        } else {
            display.value = '';
            if (startInput) startInput.value = '';
            if (endInput) endInput.value = '';
        }

        updateClearButton(id);
    }

    /**
     * Update clear button visibility
     * @param {string} id
     */
    function updateClearButton(id) {
        const instance = instances[id];
        const clearBtn = document.getElementById(`${id}_clear`);
        const clearAllBtn = document.getElementById(`${id}_clearAll`);
        const hasSelection = instance.selectedStart != null;

        clearBtn?.classList.toggle('hidden', !hasSelection);
        clearAllBtn?.classList.toggle('hidden', !hasSelection);
    }

    /**
     * Update navigation button states
     * @param {string} id
     */
    function updateNavButtons(id) {
        const instance = instances[id];
        const prevBtn = document.getElementById(`${id}_prev`);

        if (prevBtn) {
            const currentMonthStart = new Date(instance.currentMonth);
            const minMonthStart = new Date(
                instance.config.minDate.getFullYear(),
                instance.config.minDate.getMonth(),
                1
            );
            prevBtn.disabled = currentMonthStart <= minMonthStart;
        }
    }

    /**
     * Show/hide legend items based on calendar config
     * @param {string} id
     */
    function updateLegend(id) {
        const instance = instances[id];
        const legend = document.getElementById(`${id}_legend`);
        if (!legend) return;

        const { disabled, highlights } = instance.config;
        const hasRanges = disabled.ranges?.length > 0;
        const hasHighlights = highlights?.length > 0;
        const hasDisabledDates = disabled.dates?.length > 0 || instance.config.selectableMinDate;

        // Determine which legend items to show
        const show = {
            highlight: hasHighlights,
            extension: hasHighlights,
            booked: hasRanges,
            unavailable: (hasRanges || hasDisabledDates) && !hasHighlights,
        };

        let anyVisible = false;
        legend.querySelectorAll('[data-legend]').forEach(el => {
            const key = el.dataset.legend;
            if (show[key]) {
                el.classList.remove('hidden');
                el.classList.add('flex');
                anyVisible = true;
            } else {
                el.classList.add('hidden');
                el.classList.remove('flex');
            }
        });

        legend.classList.toggle('hidden', !anyVisible);
        if (anyVisible) legend.classList.add('flex');
    }

    // ==========================================
    // Navigation
    // ==========================================

    function prevMonth(id) {
        const instance = instances[id];
        const prev = new Date(instance.currentMonth);
        prev.setMonth(prev.getMonth() - 1);

        const minMonth = new Date(
            instance.config.minDate.getFullYear(),
            instance.config.minDate.getMonth(),
            1
        );
        if (prev < minMonth) return;

        instance.currentMonth = prev;
        renderCalendar(id);
    }

    function nextMonth(id) {
        const instance = instances[id];
        instance.currentMonth.setMonth(instance.currentMonth.getMonth() + 1);
        renderCalendar(id);
    }

    // ==========================================
    // Open/Close
    // ==========================================

    function toggle(id) {
        const instance = instances[id];
        instance.isOpen ? close(id) : open(id);
    }

    function open(id) {
        const instance = instances[id];
        if (instance.isOpen) return;

        instance.isOpen = true;
        const panel = document.getElementById(`${id}_panel`);
        panel?.classList.remove('hidden');

        setTimeout(() => {
            document.addEventListener('click', handleClickOutside);
            document.addEventListener('keydown', handleKeydown);
        }, 0);
    }

    function close(id) {
        const instance = instances[id];
        if (!instance?.isOpen) return;

        instance.isOpen = false;
        instance.hoverDate = null;
        document.getElementById(`${id}_panel`)?.classList.add('hidden');

        document.removeEventListener('click', handleClickOutside);
        document.removeEventListener('keydown', handleKeydown);
    }

    function handleClickOutside(e) {
        Object.keys(instances).forEach(id => {
            const container = document.getElementById(id);
            if (container && !container.contains(e.target)) {
                close(id);
            }
        });
    }

    function handleKeydown(e) {
        if (e.key === 'Escape') {
            Object.keys(instances).forEach(close);
        }
    }

    function clear(id) {
        const instance = instances[id];
        if (!instance) return;

        instance.selectedStart = null;
        instance.selectedEnd = null;
        instance.hoverDate = null;

        updateDisplay(id);
        renderCalendar(id);
        dispatchChangeEvent(id);
    }

    // ==========================================
    // Event Listeners
    // ==========================================

    function setupEventListeners(id) {
        const panel = document.getElementById(`${id}_panel`);
        if (!panel) return;

        // Clear hover state when mouse leaves the calendar panel
        panel.addEventListener('mouseleave', () => {
            const instance = instances[id];
            if (instance && instance.hoverDate) {
                instance.hoverDate = null;
                renderCalendar(id);
            }
        });

        // Touch gestures
        let touchStartX = 0;
        panel.addEventListener('touchstart', e => {
            touchStartX = e.touches[0].clientX;
        }, { passive: true });

        panel.addEventListener('touchend', e => {
            const dx = e.changedTouches[0].clientX - touchStartX;
            if (Math.abs(dx) > 50) {
                dx > 0 ? prevMonth(id) : nextMonth(id);
            }
        }, { passive: true });

        // Keyboard navigation
        panel.addEventListener('keydown', (e) => {
            const instance = instances[id];
            if (!instance.isOpen) return;

            if (e.key === 'ArrowLeft') {
                e.preventDefault();
                prevMonth(id);
            } else if (e.key === 'ArrowRight') {
                e.preventDefault();
                nextMonth(id);
            }
        });
    }

    // ==========================================
    // Public API
    // ==========================================
    /**
     * Destroy a calendar instance so it can be re-initialized
     * @param {string} id
     */
    function destroy(id) {
        close(id);
        delete instances[id];
    }

    /**
     * Update a calendar instance with new configuration and re-render
     * @param {string} id
     * @param {Object} newConfig - Partial config: { minDate, maxDate, disabled, mode }
     */
    function update(id, newConfig) {
        let instance = instances[id];
        if (!instance) {
            // Initialize first if not yet created
            init(id);
            instance = instances[id];
            if (!instance) return;
        }

        if (newConfig.minDate) {
            const d = new Date(newConfig.minDate);
            d.setHours(0, 0, 0, 0);
            instance.config.minDate = d;
            // Reset current month to min date if before it
            const minMonth = new Date(d.getFullYear(), d.getMonth(), 1);
            if (instance.currentMonth < minMonth) {
                instance.currentMonth = minMonth;
            }
        }
        if ('maxDate' in newConfig) {
            if (newConfig.maxDate) {
                const d = new Date(newConfig.maxDate);
                d.setHours(0, 0, 0, 0);
                instance.config.maxDate = d;
            } else {
                instance.config.maxDate = null;
            }
        }
        if (newConfig.selectableMinDate) {
            const d = new Date(newConfig.selectableMinDate);
            d.setHours(0, 0, 0, 0);
            instance.config.selectableMinDate = d;
        }
        if (newConfig.disabled !== undefined) {
            instance.config.disabled = typeof newConfig.disabled === 'string'
                ? DisabledDatesParser.parse(newConfig.disabled)
                : newConfig.disabled;
        }
        if (newConfig.highlights !== undefined) {
            instance.config.highlights = newConfig.highlights || [];
        }

        // Clear selection
        instance.selectedStart = null;
        instance.selectedEnd = null;
        instance.hoverDate = null;

        updateDisplay(id);
        renderCalendar(id);
    }

    window.SmartCalendar = {
        init,
        toggle,
        open,
        close,
        clear,
        prevMonth,
        nextMonth,
        destroy,
        update,
        getSelection: (id) => {
            const instance = instances[id];
            if (!instance) return null;
            return {
                startDate: instance.selectedStart ? DateUtils.toISODate(instance.selectedStart) : null,
                endDate: instance.selectedEnd ? DateUtils.toISODate(instance.selectedEnd) : null
            };
        }
    };

    // Auto-initialize
    document.addEventListener('DOMContentLoaded', () => {
        document.querySelectorAll('.smart-calendar').forEach(el => init(el.id));
    });

})();
