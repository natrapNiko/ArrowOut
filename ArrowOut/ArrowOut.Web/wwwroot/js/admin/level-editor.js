// Admin grid editor. Keeps the hidden ArrowsJson field up to date and runs the solver in the
// browser so you see right away if the level works. The server checks everything again anyway.
//
// How drawing works: press on an empty cell and drag through empty neighbours to draw a bendy
// arrow. The cell where you let go is the head, pointing the way you last moved. Dragging back
// one cell undoes it. A normal click places a straight arrow using the direction and length
// from the toolbar. Clicking an existing arrow deletes it.
(function () {
    'use strict';

    const editor = document.getElementById('levelEditor');
    if (!editor) {
        return;
    }

    const Engine = window.ArrowOutEngine;
    const Renderer = window.ArrowOutRenderer;
    const CELL = Renderer.CELL;
    const SVG_NS = 'http://www.w3.org/2000/svg';
    const MAX_SIZE = 96;
    const MIN_SIZE = 3;

    const form = document.getElementById('levelForm');
    const boardHost = document.getElementById('editorBoard');
    const json = document.getElementById('arrowsJson');
    const widthInput = form.querySelector('[data-editor-width]');
    const heightInput = form.querySelector('[data-editor-height]');
    const lengthInput = document.getElementById('editorLength');
    const status = document.getElementById('editorStatus');
    const stats = document.getElementById('editorStats');
    const csrf = form.querySelector('input[name="__RequestVerificationToken"]');

    let direction = Engine.Direction.Up;
    let arrows = parseArrows(json.value);
    let rendered = null;
    let draft = null; // { cells: [{x,y}] tail-first, line: SVGPolylineElement }

    function parseArrows(value) {
        try {
            const parsed = typeof value === 'string' ? JSON.parse(value || '[]') : value;
            return Array.isArray(parsed)
                ? parsed.map((a, i) => Engine.normalizeArrow({
                    id: i + 1, x: a.x, y: a.y, direction: a.direction, length: a.length, cells: a.cells
                }))
                : [];
        } catch (e) {
            return [];
        }
    }

    function clampSize(input) {
        const value = Number(input.value);
        return Number.isInteger(value) ? Math.min(MAX_SIZE, Math.max(MIN_SIZE, value)) : MIN_SIZE;
    }

    function size() {
        return { width: clampSize(widthInput), height: clampSize(heightInput) };
    }

    function renumber() {
        arrows = arrows.map((a, i) => Object.assign({}, a, { id: i + 1 }));
    }

    // Builds the board and quietly drops arrows that don't fit anymore (e.g. after making the grid smaller).
    function buildBoard() {
        const { width, height } = size();
        const kept = [];
        for (const arrow of arrows) {
            try {
                new Engine.Board(width, height, kept.concat([arrow])); // throws if it doesn't fit
                kept.push(arrow);
            } catch (e) {
                // Off the grid or overlapping after a resize, so drop it.
            }
        }
        arrows = kept;
        renumber();
        return new Engine.Board(width, height, arrows);
    }

    function isStraight(arrow) {
        const d = Engine.Deltas[arrow.direction];
        return arrow.cells.every((c, i) => c.x === arrow.x - d.x * i && c.y === arrow.y - d.y * i);
    }

    function serialise() {
        return JSON.stringify(arrows.map(a => {
            const item = { x: a.x, y: a.y, direction: Engine.DirectionNames[a.direction], length: a.length };
            if (!isStraight(a)) {
                item.cells = a.cells.map(c => ({ x: c.x, y: c.y }));
            }
            return item;
        }));
    }

    function sync() {
        const board = buildBoard();
        json.value = serialise();

        const { width, height } = size();
        rendered = Renderer.render(boardHost, width, height, arrows, { cells: true });

        const solution = board.solve();
        rendered.elements.forEach((element, id) => element.classList.toggle('is-stuck', solution.stuck.includes(id)));

        const cells = arrows.reduce((sum, a) => sum + a.length, 0);
        const gaps = width * height - cells;

        if (arrows.length === 0) {
            setStatus('Empty board', 'text-muted');
        } else if (!solution.solvable) {
            setStatus('✗ Jammed: ' + solution.stuck.length + ' arrow(s) can never leave', 'text-danger');
        } else if (gaps > 0) {
            setStatus('✓ Solvable · ' + gaps + ' empty cell(s) left', 'text-warning');
        } else {
            setStatus('✓ Solvable · board fully filled', 'text-success');
        }

        const bent = arrows.filter(a => !isStraight(a)).length;
        stats.textContent = arrows.length + ' arrows (' + bent + ' bent) · ' + Math.round(cells * 100 / (width * height)) + '% filled';
    }

    function setStatus(text, css) {
        status.textContent = text;
        status.className = 'fw-semibold ' + css;
    }

    function tryAdd(candidate) {
        const { width, height } = size();
        try {
            const arrow = Engine.normalizeArrow(Object.assign({ id: arrows.length + 1 }, candidate));
            new Engine.Board(width, height, arrows.concat([arrow]));
            arrows.push(arrow);
            sync();
            return true;
        } catch (e) {
            setStatus('That arrow would leave the board or overlap another arrow.', 'text-danger');
            return false;
        }
    }

    function remove(id) {
        arrows = arrows.filter(a => a.id !== id);
        sync();
    }

    function occupied(cell) {
        return arrows.some(a => a.cells.some(c => c.x === cell.x && c.y === cell.y));
    }

    // ---------- Drawing ----------

    function drawDraft() {
        const points = draft.cells.map(c => (c.x * CELL + CELL / 2) + ',' + (c.y * CELL + CELL / 2)).join(' ');
        draft.line.setAttribute('points', points);
    }

    function finishDraft() {
        const cells = draft.cells;
        draft.line.remove();
        draft = null;

        if (cells.length === 1) {
            const length = Math.min(8, Math.max(1, Number(lengthInput.value) || 1));
            tryAdd({ x: cells[0].x, y: cells[0].y, direction: direction, length: length });
            return;
        }

        const last = cells[cells.length - 1];
        const previous = cells[cells.length - 2];
        const step = { x: last.x - previous.x, y: last.y - previous.y };
        const headDirection = Engine.Deltas.findIndex(d => d.x === step.x && d.y === step.y);
        const headFirst = cells.slice().reverse();
        tryAdd({ x: last.x, y: last.y, direction: headDirection, cells: headFirst });
    }

    boardHost.addEventListener('pointerdown', event => {
        const arrowNode = event.target.closest('.ao-arrow');
        if (arrowNode) {
            remove(Number(arrowNode.dataset.id));
            return;
        }

        const { width, height } = size();
        const cell = Renderer.cellFromEvent(rendered.svg, event, width, height);
        if (!cell || occupied(cell)) {
            return;
        }

        event.preventDefault();
        const line = document.createElementNS(SVG_NS, 'polyline');
        line.setAttribute('class', 'ao-draft');
        rendered.svg.appendChild(line);
        draft = { cells: [cell], line: line };
        drawDraft();
        boardHost.setPointerCapture(event.pointerId);
    });

    boardHost.addEventListener('pointermove', event => {
        if (!draft) {
            return;
        }

        const { width, height } = size();
        const cell = Renderer.cellFromEvent(rendered.svg, event, width, height);
        if (!cell) {
            return;
        }

        const cells = draft.cells;
        const last = cells[cells.length - 1];
        if (cell.x === last.x && cell.y === last.y) {
            return;
        }

        const previous = cells[cells.length - 2];
        if (previous && previous.x === cell.x && previous.y === cell.y) {
            cells.pop(); // dragging back undoes the last step
        } else if (Math.abs(cell.x - last.x) + Math.abs(cell.y - last.y) === 1
            && !occupied(cell)
            && !cells.some(c => c.x === cell.x && c.y === cell.y)
            && cells.length < 64) {
            cells.push(cell);
        }

        drawDraft();
    });

    boardHost.addEventListener('pointerup', () => {
        if (draft) {
            finishDraft();
        }
    });

    boardHost.addEventListener('pointercancel', () => {
        if (draft) {
            draft.line.remove();
            draft = null;
        }
    });

    // ---------- Toolbar ----------

    editor.querySelectorAll('[data-direction]').forEach(button => {
        button.addEventListener('click', () => {
            direction = Engine.normalizeDirection(button.dataset.direction);
            editor.querySelectorAll('[data-direction]').forEach(b => {
                const active = b === button;
                b.classList.toggle('active', active);
                b.setAttribute('aria-pressed', String(active));
            });
        });
    });

    widthInput.addEventListener('change', sync);
    heightInput.addEventListener('change', sync);

    document.getElementById('editorClear').addEventListener('click', () => {
        arrows = [];
        sync();
    });

    document.getElementById('editorGenerate').addEventListener('click', async () => {
        const { width, height } = size();
        const button = document.getElementById('editorGenerate');
        button.disabled = true;
        try {
            const response = await fetch(editor.dataset.generateUrl, {
                method: 'POST',
                credentials: 'same-origin',
                headers: { 'Content-Type': 'application/json', 'Accept': 'application/json', 'X-CSRF-TOKEN': csrf ? csrf.value : '' },
                body: JSON.stringify({ width: width, height: height, fill: 1.0, maxLength: Math.max(3, Math.min(10, Math.max(width, height) - 1)) })
            });
            if (!response.ok) {
                throw new Error('Generation failed (' + response.status + ')');
            }
            arrows = parseArrows(await response.json());
            sync();
        } catch (error) {
            setStatus(error.message, 'text-danger');
        } finally {
            button.disabled = false;
        }
    });

    sync();
}());
