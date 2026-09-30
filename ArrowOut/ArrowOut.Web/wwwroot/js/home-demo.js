// The little maze on the home page that plays itself. A free arrow lights up and slides out
// along its path (same engine and renderer as the real game). When the board is empty the next
// one pops in. It pauses when it's off screen or the tab is in the background, and stays still
// for people who turned on reduced motion.
(function () {
    'use strict';

    const host = document.getElementById('heroDemo');
    const Engine = window.ArrowOutEngine;
    const Renderer = window.ArrowOutRenderer;
    if (!host || !Engine || !Renderer) {
        return;
    }

    let boards;
    try {
        boards = JSON.parse(host.dataset.boards);
    } catch (e) {
        return; // don't break the page if the data is missing
    }
    if (!Array.isArray(boards) || boards.length === 0) {
        return;
    }

    const CELL = Renderer.CELL;
    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const wait = ms => new Promise(resolve => window.setTimeout(resolve, ms));

    let visible = true;
    let resumeWaiters = [];

    function setVisible(value) {
        visible = value && !document.hidden;
        if (visible) {
            resumeWaiters.forEach(resolve => resolve());
            resumeWaiters = [];
        }
    }

    // Resolves right away if the demo is on screen, otherwise when it comes back into view.
    function whenVisible() {
        return visible ? Promise.resolve() : new Promise(resolve => resumeWaiters.push(resolve));
    }

    if ('IntersectionObserver' in window) {
        new IntersectionObserver(entries => setVisible(entries[0].isIntersecting)).observe(host);
    }
    document.addEventListener('visibilitychange', () => setVisible(!document.hidden));

    function draw(data) {
        const board = new Engine.Board(data.width, data.height, data.arrows);
        const rendered = Renderer.render(host, data.width, data.height, Array.from(board.arrows.values()));
        rendered.svg.setAttribute('aria-hidden', 'true');
        rendered.svg.setAttribute('focusable', 'false');
        rendered.elements.forEach(element => element.removeAttribute('tabindex')); // it's just decoration, not buttons
        return { board, elements: rendered.elements };
    }

    async function play() {
        for (let round = 0; ; round++) {
            const { board, elements } = draw(boards[round % boards.length]);
            host.classList.remove('is-leaving-round');
            host.classList.add('is-entering-round');
            await wait(600);
            host.classList.remove('is-entering-round');

            while (!board.isCleared) {
                await whenVisible();

                // Any free arrow works. Picking a random one makes every round look different.
                const free = board.freeIds();
                const id = free[Math.floor(Math.random() * free.length)];
                const element = elements.get(id);
                const result = board.move(id);

                element.classList.add('is-hint');
                await wait(380);
                element.classList.remove('is-hint');
                element.classList.add('is-leaving');
                Renderer.slide(element, (result.distance + 1) * CELL, 260 + result.distance * 55, 'accelerate')
                    .then(() => element.remove());
                await wait(430);
            }

            host.classList.add('is-leaving-round');
            await wait(900);
        }
    }

    if (reduceMotion) {
        draw(boards[0]); // just a still maze for people who don't want animations
    } else {
        play();
    }
}());
