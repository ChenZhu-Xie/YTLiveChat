(function initializeTerminalTheme(global) {
    "use strict";

    const defaultThemeIds = Object.freeze([
        "mist-cottage",
        "rain-greenhouse",
        "harvest-barn",
        "winter-kitchen"
    ]);
    const defaultStagePlaybackMs = 100_000;
    const defaultIntervalMs = defaultStagePlaybackMs;
    const defaultTransitionMs = 1_200;

    const playbackThemes = Object.freeze([
        "mist-cottage", "mist-cottage", "mist-cottage",
        "rain-greenhouse", "rain-greenhouse", "rain-greenhouse", "rain-greenhouse",
        "harvest-barn", "harvest-barn", "harvest-barn",
        "winter-kitchen", "winter-kitchen", "winter-kitchen"
    ]);

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
            : defaultStagePlaybackMs;
        const transitionMs = Number.isFinite(Number(candidate?.transitionMs)) && Number(candidate.transitionMs) >= 0
            ? Number(candidate.transitionMs)
            : defaultTransitionMs;

        return { themeIds: safeThemeIds, theme, changedAt, autoCycleMs, transitionMs };
    }

    function resolveThemeState(candidate, wallClockMs = Date.now()) {
        const state = normalizeState(candidate);
        const safeStageMs = state.autoCycleMs > 0 ? state.autoCycleMs : defaultStagePlaybackMs;
        const startIndex = Math.max(0, playbackThemes.indexOf(state.theme));
        const playlistDuration = playbackThemes.length * safeStageMs;
        const elapsed = Math.max(0, wallClockMs - state.changedAt);
        const cycleElapsed = elapsed % playlistDuration;

        const currentSlot = Math.floor(cycleElapsed / safeStageMs);
        const currentIndex = (startIndex + currentSlot) % playbackThemes.length;
        const currentTheme = playbackThemes[currentIndex];

        let slotsRemainingInTheme = playbackThemes.length;
        for (let offset = 1; offset < playbackThemes.length; offset += 1) {
            const checkIndex = (currentIndex + offset) % playbackThemes.length;
            if (playbackThemes[checkIndex] !== currentTheme) {
                slotsRemainingInTheme = offset;
                break;
            }
        }

        const timeIntoCurrentSlot = cycleElapsed % safeStageMs;
        const remainingInTheme = (slotsRemainingInTheme - 1) * safeStageMs + (safeStageMs - timeIntoCurrentSlot);
        const clockSkewLead = state.changedAt > wallClockMs ? (state.changedAt - wallClockMs) : 0;

        return {
            ...state,
            currentTheme,
            nextChangeAt: wallClockMs + clockSkewLead + remainingInTheme
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
