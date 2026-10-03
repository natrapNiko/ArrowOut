// The game page: loads the board from the API, plays taps locally so they feel instant,
// animates everything, and at the end sends all the taps to the server to check the win.
(function () {
    'use strict';

    const root = document.getElementById('game');
    if (!root) {
        return;
    }

    const Engine = window.ArrowOutEngine;
    const Renderer = window.ArrowOutRenderer;
    const Viewport = window.ArrowOutViewport; // optional, zoom and pan for big boards
    const CELL = Renderer.CELL;
    const HINTS_PER_ATTEMPT = 3;
    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    const config = {
        levelId: Number(root.dataset.levelId),
        maxLives: Number(root.dataset.maxLives) || 3,
        apiBase: root.dataset.apiBase
    };

    const ui = {
        board: document.getElementById('board'),
        lives: document.getElementById('lives'),
        timer: document.getElementById('timer'),
        winTime: document.getElementById('winTime'),
        hintButton: document.getElementById('hintButton'),
        hintCount: document.getElementById('hintCount'),
        restartButton: document.getElementById('restartButton'),
        winModal: new bootstrap.Modal(document.getElementById('winModal')),
        failModal: new bootstrap.Modal(document.getElementById('failModal')),
        winStars: document.getElementById('winStars'),
        winDetail: document.getElementById('winDetail'),
        winBest: document.getElementById('winBest'),
        winError: document.getElementById('winError'),
        zoomInButton: document.getElementById('zoomInButton'),
        zoomOutButton: document.getElementById('zoomOutButton'),
        zoomFitButton: document.getElementById('zoomFitButton')
    };

    const antiforgeryInput = document.querySelector('#antiforgery input[name="__RequestVerificationToken"]');
    const csrfToken = antiforgeryInput ? antiforgeryInput.value : '';

    const state = {
        level: null,
        board: null,
        elements: new Map(),
        lives: config.maxLives,
        taps: [],
        hintsLeft: HINTS_PER_ATTEMPT,
        elapsedMs: 0, // play time for this attempt, not counting time the tab was hidden
        runningSince: null, // performance.now() when the clock last started, null while it's stopped
        timerId: null,
        hintsUsed: 0,
        hintedId: null,
        finished: false,
        viewport: null
    };

    // ---------- API ----------

    async function api(method, path, body) {
        const response = await fetch(config.apiBase + '/' + config.levelId + path, {
            method: method,
            credentials: 'same-origin',
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json',
                'X-CSRF-TOKEN': csrfToken
            },
            body: body === undefined ? undefined : JSON.stringify(body)
        });

        if (response.status === 401) {
            window.location.assign('/Identity/Account/Login?returnUrl=' + encodeURIComponent(window.location.pathname));
            throw new Error('Not signed in');
        }

        if (!response.ok) {
            let detail = 'Request failed (' + response.status + ')';
            try {
                const problem = await response.json();
                detail = problem.detail || problem.title || detail;
            } catch (e) {
                // The error wasn't JSON, just use the generic message.
            }
            throw new Error(detail);
        }

        return response.status === 204 ? null : response.json();
    }

    // ---------- Game flow ----------

    async function load() {
        try {
            state.level = await api('GET', '');
            start();
        } catch (error) {
            showBoardMessage(error.message || 'The level could not be loaded.');
        }
    }

    function start() {
        const level = state.level;
        state.board = new Engine.Board(level.width, level.height, level.arrows);
        state.lives = level.maxLives;
        state.taps = [];
        state.hintsLeft = HINTS_PER_ATTEMPT;
        state.hintsUsed = 0;
        state.hintedId = null;
        state.finished = false;

        const rendered = Renderer.render(ui.board, level.width, level.height, Array.from(state.board.arrows.values()));
        state.elements = rendered.elements;

        // Keep the zoom when restarting the same board.
        const previousView = state.viewport ? state.viewport.getView() : null;
        state.viewport = Viewport ? Viewport.attach(rendered.svg, level.width, level.height, CELL, previousView) : null;
        ui.board.classList.remove('is-cleared');

        renderLives();
        renderCounters();
        resetClock();
        startClock();

        // Don't wait for this, it should never hold up the game.
        api('POST', '/attempts').catch(() => { /* offline or transient; ignored */ });
    }

    function tap(id) {
        if (state.finished || !state.board.has(id)) {
            return;
        }

        clearHint();
        const arrow = state.board.get(id);
        const result = state.board.move(id);
        state.taps.push(id);

        const element = state.elements.get(id);
        if (result.success) {
            state.elements.delete(id);
            animateExit(element, arrow, result.distance);
            renderCounters();

            if (state.board.isCleared) {
                state.finished = true;
                stopClock(); // the time is when the last arrow left, not when the dialog shows up
                window.setTimeout(complete, reduceMotion ? 50 : 450);
            }
            return;
        }

        state.lives--;
        animateCollision(element, arrow, result.distance, state.elements.get(result.blockerId));
        renderLives(true);

        if (state.lives <= 0) {
            state.finished = true;
            stopClock();
            window.setTimeout(() => ui.failModal.show(), reduceMotion ? 50 : 700);
        }
    }

    async function complete() {
        ui.board.classList.add('is-cleared');
        ui.winError.classList.add('d-none');
        ui.winBest.classList.add('d-none');

        const mistakes = config.maxLives - state.lives;
        let stars = mistakes === 0 ? 3 : (mistakes === 1 ? 2 : 1);

        const pointsBox = document.getElementById('winPoints');
        if (pointsBox) {
            pointsBox.classList.add('d-none');
        }

        try {
            const result = await api('POST', '/completion', { taps: state.taps, hintsUsed: state.hintsUsed });
            stars = result.stars;
            ui.winBest.classList.toggle('d-none', !(result.isNewBest && !result.isFirstCompletion));
            renderPoints(result);
        } catch (error) {
            ui.winError.textContent = 'Progress was not saved: ' + error.message;
            ui.winError.classList.remove('d-none');
        }

        renderStars(stars);
        if (ui.winTime) {
            ui.winTime.textContent = formatTime(state.elapsedMs);
        }
        ui.winDetail.textContent = mistakes === 0 ? 'Flawless — no collisions.' : mistakes + (mistakes === 1 ? ' collision.' : ' collisions.');

        ui.winModal.show();
    }

    // Points come from the server's result (it replayed the taps and counted the hints itself).
    function renderPoints(result) {
        const box = document.getElementById('winPoints');
        if (!box || typeof result.points !== 'number') {
            return;
        }

        // A fixed amount per kind (Easy 1, Normal 4, Hard 10, Challenge 20), only the first time you win a board.
        const format = n => n.toLocaleString();
        const plural = n => (n === 1 ? ' point' : ' points');
        const badge = document.querySelector('.ao-kind-badge');
        const kind = badge ? badge.textContent.trim() : '';

        document.getElementById('winPointsValue').textContent = '+' + format(result.pointsGained) + plural(result.pointsGained);

        const notes = [];
        notes.push(result.pointsGained > 0
            ? (kind ? kind + ' win' : 'Win')
            : 'Already counted — each board scores once');
        notes.push('Total: ' + format(result.totalPoints) + plural(result.totalPoints));
        document.getElementById('winPointsNote').textContent = notes.join(' · ');

        box.classList.remove('d-none');
    }

    // ---------- Clock ----------
    // Only counts while the page is visible, so switching tabs doesn't add time.

    function currentElapsed() {
        return state.elapsedMs + (state.runningSince === null ? 0 : performance.now() - state.runningSince);
    }

    function startClock() {
        if (state.runningSince !== null || state.finished || document.hidden) {
            return;
        }
        state.runningSince = performance.now();
        state.timerId = window.setInterval(renderClock, 250);
        renderClock();
    }

    function stopClock() {
        state.elapsedMs = currentElapsed();
        state.runningSince = null;
        window.clearInterval(state.timerId);
        state.timerId = null;
        renderClock();
    }

    function resetClock() {
        stopClock();
        state.elapsedMs = 0;
        renderClock();
    }

    function renderClock() {
        if (ui.timer) {
            ui.timer.textContent = formatTime(currentElapsed());
        }
    }

    /** 0:07, 12:45, 1:02:09 */
    function formatTime(ms) {
        const total = Math.floor(ms / 1000);
        const hours = Math.floor(total / 3600);
        const minutes = Math.floor((total % 3600) / 60);
        const seconds = String(total % 60).padStart(2, '0');
        return hours > 0 ? hours + ':' + String(minutes).padStart(2, '0') + ':' + seconds : minutes + ':' + seconds;
    }

    document.addEventListener('visibilitychange', () => {
        if (document.hidden) {
            if (state.runningSince !== null) {
                stopClock();
            }
        } else if (state.level && !state.finished) {
            startClock();
        }
    });

    async function hint() {
        if (state.finished || state.hintsLeft <= 0 || !state.board || state.board.isCleared) {
            return;
        }

        let arrowId = null;
        try {
            const result = await api('POST', '/hint', { remainingArrowIds: state.board.remainingIds() });
            arrowId = result.arrowId;
        } catch (error) {
            // No connection? The local engine works just as well here (any free arrow is safe).
            arrowId = state.board.freeIds()[0] || null;
        }

        if (arrowId === null || !state.elements.has(arrowId)) {
            return;
        }

        state.hintsLeft--;
        state.hintsUsed++;
        clearHint();
        state.hintedId = arrowId;
        const element = state.elements.get(arrowId);
        element.classList.add('is-hint');
        if (state.viewport) {
            state.viewport.reveal(state.board.get(arrowId).cells); // it might be off screen if you're zoomed in
        }
        element.focus({ preventScroll: true });
        renderCounters();
    }

    function clearHint() {
        if (state.hintedId !== null && state.elements.has(state.hintedId)) {
            state.elements.get(state.hintedId).classList.remove('is-hint');
        }
        state.hintedId = null;
    }

    // ---------- Animation ----------
    // Arrows slide along their own path like a train, so bent bodies follow the head around corners.

    function animateExit(element, arrow, distance) {
        element.classList.add('is-leaving');
        const travel = (distance + 1) * CELL;
        Renderer.slide(element, travel, reduceMotion ? 1 : 160 + (distance + 1) * 42, 'accelerate')
            .then(() => element.remove());
    }

    function animateCollision(element, arrow, distance, blocker) {
        element.classList.add('is-blocked');
        if (blocker && blocker !== element) {
            blocker.classList.add('is-blocker');
        }

        const reach = distance * CELL + 7; // push a bit into the other arrow so the crash is easy to see
        const duration = reduceMotion ? 1 : 110 + distance * 40;
        Renderer.slide(element, reach, duration, 'accelerate')
            .then(() => Renderer.slide(element, 0, duration + 60, 'decelerate'))
            .then(() => {
                element.classList.remove('is-blocked');
                if (blocker) {
                    blocker.classList.remove('is-blocker');
                }
            });

        if (!reduceMotion) {
            ui.board.classList.remove('is-shaking');
            void ui.board.offsetWidth; // restart the CSS animation
            ui.board.classList.add('is-shaking');
        }
        if (navigator.vibrate) {
            navigator.vibrate(60);
        }
    }

    // ---------- Rendering helpers ----------

    function renderLives(justLost) {
        const hearts = [];
        for (let i = 0; i < state.level.maxLives; i++) {
            const heart = document.createElement('i');
            const lost = i >= state.lives;
            heart.className = 'bi ' + (lost ? 'bi-heart is-lost' : 'bi-heart-fill');
            if (justLost && i === state.lives) {
                heart.classList.add('is-just-lost');
            }
            heart.setAttribute('aria-hidden', 'true');
            hearts.push(heart);
        }
        ui.lives.replaceChildren(...hearts);
        ui.lives.setAttribute('aria-label', state.lives + ' of ' + state.level.maxLives + ' lives remaining');
    }

    function renderCounters() {
        ui.hintCount.textContent = String(state.hintsLeft);
        ui.hintButton.disabled = state.hintsLeft <= 0;
    }

    function renderStars(stars) {
        const icons = [];
        for (let i = 1; i <= 3; i++) {
            const star = document.createElement('i');
            star.className = 'bi ' + (i <= stars ? 'bi-star-fill is-earned' : 'bi-star');
            star.setAttribute('aria-hidden', 'true');
            icons.push(star);
        }
        ui.winStars.replaceChildren(...icons);
        ui.winStars.setAttribute('aria-label', stars + ' of 3 stars');
    }

    function showBoardMessage(message) {
        const box = document.createElement('div');
        box.className = 'ao-board-loading text-center p-3';
        box.textContent = message; // textContent, so it's never treated as HTML
        ui.board.replaceChildren(box);
    }

    // ---------- Input ----------

    function arrowIdFromEvent(event) {
        const group = event.target.closest('.ao-arrow');
        return group ? Number(group.dataset.id) : null;
    }

    ui.board.addEventListener('click', event => {
        const id = arrowIdFromEvent(event);
        if (id !== null) {
            tap(id);
        }
    });

    ui.board.addEventListener('keydown', event => {
        if (event.key !== 'Enter' && event.key !== ' ') {
            return;
        }
        const id = arrowIdFromEvent(event);
        if (id !== null) {
            event.preventDefault();
            tap(id);
        }
    });

    document.addEventListener('keydown', event => {
        if (event.target.closest('input, textarea, select') || event.ctrlKey || event.metaKey || event.altKey) {
            return;
        }
        if (anyDialogOpen()) {
            return; // no shortcuts behind an open dialog (e.g. R while "Are you sure?" is up)
        }
        if (event.key === 'h' || event.key === 'H') {
            hint();
        } else if (event.key === 'r' || event.key === 'R') {
            requestRestart(false);
        } else if (state.viewport && (event.key === '+' || event.key === '=')) {
            state.viewport.zoomIn();
        } else if (state.viewport && (event.key === '-' || event.key === '_')) {
            state.viewport.zoomOut();
        } else if (state.viewport && event.key === '0') {
            state.viewport.fit();
        }
    });

    function onViewport(button, action) {
        if (button) {
            button.addEventListener('click', () => {
                if (state.viewport) {
                    state.viewport[action]();
                }
            });
        }
    }

    onViewport(ui.zoomInButton, 'zoomIn');
    onViewport(ui.zoomOutButton, 'zoomOut');
    onViewport(ui.zoomFitButton, 'fit');

    // Optional dot grid. The browser remembers the choice (if storage works).
    const GRID_KEY = 'arrowout.showGrid';
    const gridButton = document.getElementById('gridButton');

    function setGrid(show) {
        ui.board.classList.toggle('show-grid', show);
        if (gridButton) {
            gridButton.setAttribute('aria-pressed', String(show));
        }
        try {
            window.localStorage.setItem(GRID_KEY, show ? '1' : '0');
        } catch (e) {
            // Private mode or storage blocked: the button still works, it just won't be remembered.
        }
    }

    function toggleGrid() {
        setGrid(!ui.board.classList.contains('show-grid'));
    }

    if (gridButton) {
        let saved = false;
        try {
            saved = window.localStorage.getItem(GRID_KEY) === '1';
        } catch (e) {
            saved = false;
        }
        setGrid(saved);
        gridButton.addEventListener('click', toggleGrid);
        document.addEventListener('keydown', event => {
            if ((event.key === 'g' || event.key === 'G') && !event.target.closest('input, textarea, select')
                && !event.ctrlKey && !event.metaKey && !event.altKey) {
                toggleGrid();
            }
        });
    }

    function restart() {
        if (!state.level) {
            return;
        }
        ui.winModal.hide();
        ui.failModal.hide();
        start();
    }

    // ---------- Dialogs ----------
    // Bootstrap only shows one modal at a time: close the current one, wait until it's gone, then open the next.

    const winElement = document.getElementById('winModal');
    const confirmElement = document.getElementById('confirmModal');
    const confirmDialog = confirmElement ? new bootstrap.Modal(confirmElement) : null;

    function anyDialogOpen() {
        return document.querySelector('.modal.show') !== null;
    }

    function hideThen(modal, element) {
        return new Promise(resolve => {
            if (!element || !element.classList.contains('show')) {
                resolve();
                return;
            }
            element.addEventListener('hidden.bs.modal', () => resolve(), { once: true });
            modal.hide();
        });
    }

    // Yes/No dialog. Only resolves true if they actually click "Yes".
    function askConfirm(title, message) {
        return new Promise(resolve => {
            const yes = document.getElementById('confirmYes');
            const no = document.getElementById('confirmNo');
            let answer = false;
            const onYes = () => { answer = true; confirmDialog.hide(); };
            const onNo = () => confirmDialog.hide();

            document.getElementById('confirmTitle').textContent = title;
            document.getElementById('confirmMessage').textContent = message;
            yes.addEventListener('click', onYes);
            no.addEventListener('click', onNo);
            confirmElement.addEventListener('hidden.bs.modal', () => {
                yes.removeEventListener('click', onYes);
                no.removeEventListener('click', onNo);
                resolve(answer);
            }, { once: true });
            confirmElement.addEventListener('shown.bs.modal', () => no.focus(), { once: true });
            confirmDialog.show();
        });
    }

    // Restart / Replay: ask first if they'd lose something.
    async function requestRestart(fromWinDialog) {
        if (!state.level) {
            return;
        }

        const untouched = state.taps.length === 0 && !state.finished;
        if (untouched || !confirmDialog) {
            restart(); // nothing to lose
            return;
        }

        await hideThen(ui.winModal, fromWinDialog ? winElement : null);

        const playing = !state.finished;
        if (playing) {
            stopClock(); // thinking about it doesn't count as play time
        }

        const yes = await askConfirm(
            fromWinDialog ? 'Play this board again?' : 'Restart this board?',
            fromWinDialog
                ? 'You will start the same labyrinth from the beginning.'
                : 'Your progress on this board will be lost.');

        if (yes) {
            restart();
        } else if (fromWinDialog) {
            ui.winModal.show();
        } else if (playing) {
            startClock();
        }
    }

    ui.hintButton.addEventListener('click', hint);
    ui.restartButton.addEventListener('click', () => requestRestart(false));
    document.getElementById('retryButton').addEventListener('click', restart); // they lost anyway, so no need to ask
    document.getElementById('replayButton').addEventListener('click', () => requestRestart(true));

    load();
}());
