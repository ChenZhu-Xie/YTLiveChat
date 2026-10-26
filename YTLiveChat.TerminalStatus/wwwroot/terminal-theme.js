(function initializeTerminalTheme(global) {
    "use strict";

    const defaultThemeIds = Object.freeze([
        "mist-cottage",
        "harvest-barn",
        "rain-greenhouse",
        "winter-kitchen"
    ]);
    const defaultIntervalMs = 60_000;
    const defaultTransitionMs = 1_200;
    let terminal = null;
    let cycleTimer = null;
    let cleanupTimer = null;
    let runtimeState = null;

    function normalizeState(candidate) {
        const themeIds = Array.isArray(candidate?.themeIds)
            ? candidate.themeIds.filter(theme => defaultThemeIds.includes(theme))
            : [...defaultThemeIds];
        const safeThemeIds = themeIds.length === defaultThemeIds.length
            ? themeIds
            : [...defaultThemeIds];
        const theme = safeThemeIds.includes(candidate?.theme)
            ? candidate.theme
            : "harvest-barn";
        const changedAt = Number.isFinite(Number(candidate?.changedAt)) && Number(candidate.changedAt) > 0
            ? Number(candidate.changedAt)
            : Date.now();
        const autoCycleMs = Number.isFinite(Number(candidate?.autoCycleMs)) && Number(candidate.autoCycleMs) > 0
            ? Number(candidate.autoCycleMs)
            : defaultIntervalMs;
        const transitionMs = Number.isFinite(Number(candidate?.transitionMs)) && Number(candidate.transitionMs) >= 0
            ? Number(candidate.transitionMs)
            : defaultTransitionMs;

        return { themeIds: safeThemeIds, theme, changedAt, autoCycleMs, transitionMs };
    }

    function resolveThemeState(candidate, wallClockMs = Date.now()) {
        const state = normalizeState(candidate);
        const elapsed = Math.max(0, wallClockMs - state.changedAt);
        const elapsedSlots = Math.floor(elapsed / state.autoCycleMs);
        const baseIndex = state.themeIds.indexOf(state.theme);
        return {
            ...state,
            currentTheme: state.themeIds[(baseIndex + elapsedSlots) % state.themeIds.length],
            nextChangeAt: state.changedAt + ((elapsedSlots + 1) * state.autoCycleMs)
        };
    }

    function removeDuplicateIds(root) {
        for (const element of root.querySelectorAll("[id]")) {
            if (element.id === "title") {
                element.classList.add("theme-title");
            }
            element.removeAttribute("id");
        }
    }

    function transitionTo(theme, transitionMs, animate) {
        if (!terminal || terminal.dataset.theme === theme) {
            return;
        }

        const previousGhost = document.querySelector(".terminal.theme-outgoing");
        previousGhost?.remove();
        if (cleanupTimer !== null) {
            clearTimeout(cleanupTimer);
            cleanupTimer = null;
        }

        let ghost = null;
        if (animate && transitionMs > 0) {
            ghost = terminal.cloneNode(true);
            removeDuplicateIds(ghost);
            ghost.setAttribute("aria-hidden", "true");
            ghost.classList.remove("theme-incoming");
            ghost.classList.add("theme-outgoing");
            ghost.style.setProperty("--theme-transition-ms", `${transitionMs}ms`);
            terminal.parentElement.insertBefore(ghost, terminal.nextSibling);
        }

        terminal.dataset.theme = theme;
        terminal.style.setProperty("--theme-transition-ms", `${transitionMs}ms`);
        terminal.classList.remove("theme-incoming");
        if (ghost) {
            void terminal.offsetWidth;
            terminal.classList.add("theme-incoming");
            cleanupTimer = setTimeout(() => {
                ghost.remove();
                terminal.classList.remove("theme-incoming");
                cleanupTimer = null;
            }, transitionMs + 80);
        }
    }

    function scheduleNextCycle() {
        if (cycleTimer !== null) {
            clearTimeout(cycleTimer);
        }
        if (!runtimeState) {
            return;
        }

        const resolved = resolveThemeState(runtimeState);
        const delay = Math.max(20, resolved.nextChangeAt - Date.now() + 20);
        cycleTimer = setTimeout(() => {
            const next = resolveThemeState(runtimeState);
            transitionTo(next.currentTheme, next.transitionMs, true);
            scheduleNextCycle();
        }, delay);
    }

    function applyState(candidate, animate = true) {
        runtimeState = normalizeState(candidate);
        const resolved = resolveThemeState(runtimeState);
        transitionTo(resolved.currentTheme, resolved.transitionMs, animate);
        scheduleNextCycle();
        return resolved;
    }

    async function load() {
        terminal = document.querySelector(".terminal");
        if (!terminal) {
            return null;
        }

        try {
            const response = await fetch("/api/theme", { cache: "no-store" });
            if (!response.ok) {
                throw new Error(`Theme request failed with ${response.status}.`);
            }
            return applyState(await response.json(), false);
        } catch (error) {
            console.error("Failed to load terminal theme", error);
            return applyState({ theme: terminal.dataset.theme || "harvest-barn" }, false);
        }
    }

    global.TerminalTheme = Object.freeze({
        applyState,
        defaultThemeIds,
        load,
        resolveThemeState
    });
})(globalThis);
