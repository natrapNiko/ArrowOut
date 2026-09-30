// Zoom and pan for the big boards. Only the SVG viewBox changes, so the arrows stay sharp and
// the tap handling in game.js doesn't need to know about it. The view always has the same shape
// as the container, so a tall phone screen shows a tall slice of the board. Mouse wheel and
// pinch zoom around your finger or pointer, dragging pans, and a press that turned into a drag
// or pinch never counts as a tap.
(function (root) {
    'use strict';

    const DRAG_THRESHOLD = 6;     // how many px a press can move before it counts as a drag
    const MIN_VISIBLE_CELLS = 8;  // max zoom still shows at least this many cells across
    const MARGIN_CELLS = 2;       // you can pan at least this many cells past each edge,
    const MARGIN_SHARE = 0.06;    // or this much of the board size if that's more, so there's space around it when zoomed out
    const START_CELL_PX = 16;     // starting zoom: cells this big on screen (small, but you can still tap them)
    const MIN_START_CELL_PX = 12; // unless the whole board already fits at this size
    const WHEEL_SPEED = 0.0015;
    const STEP = 1.5;             // zoom factor of the +/- buttons

    // Adds zoom and pan to a board SVG. Pass `initialView` (from getView) to keep the previous zoom,
    // e.g. when a restart draws the board again.
    function attach(svg, width, height, cell, initialView) {
        const fullW = width * cell;
        const fullH = height * cell;
        const minW = Math.min(fullW, MIN_VISIBLE_CELLS * cell);
        // Some space around the board so the arrows on the edge don't touch the edge of the screen.
        const margin = Math.max(MARGIN_CELLS * cell, MARGIN_SHARE * Math.max(fullW, fullH));
        const pointers = new Map();
        let aspect = measureAspect();
        let view = fitView();
        let dragged = false;
        let pinch = null;

        svg.setAttribute('preserveAspectRatio', 'xMidYMid meet');

        // Height / width of the element on screen. The view box always has this shape.
        function measureAspect() {
            const rect = svg.getBoundingClientRect();
            return rect.width > 0 && rect.height > 0 ? rect.height / rect.width : fullH / fullW;
        }

        // Zoomed all the way out: the whole board plus the margin, centred.
        function maxW() {
            return Math.max(fullW + 2 * margin, (fullH + 2 * margin) / aspect);
        }

        function fitView() {
            const w = maxW();
            return { w: w, x: (fullW - w) / 2, y: (fullH - w * aspect) / 2 };
        }

        // Stops the view from leaving the board (plus margin). If it's wider than that, centre it.
        function clampAxis(start, size, full) {
            const span = full + 2 * margin;
            return size >= span ? (full - size) / 2 : Math.min(full + margin - size, Math.max(-margin, start));
        }

        function clamp(v) {
            const w = Math.min(maxW(), Math.max(minW, v.w));
            const h = w * aspect;
            return { w: w, x: clampAxis(v.x, w, fullW), y: clampAxis(v.y, h, fullH) };
        }

        function setView(v) {
            view = clamp(v);
            svg.setAttribute('viewBox', view.x + ' ' + view.y + ' ' + view.w + ' ' + (view.w * aspect));
            svg.classList.toggle('is-zoomed', view.w < maxW() - 0.5);
        }

        // Screen point to board units. Goes through the CTM, so the empty bars at the sides are handled.
        function toBoard(clientX, clientY) {
            const ctm = svg.getScreenCTM();
            if (!ctm) {
                return center();
            }
            const p = new DOMPoint(clientX, clientY).matrixTransform(ctm.inverse());
            return { x: p.x, y: p.y };
        }

        function center() {
            return { x: view.x + view.w / 2, y: view.y + (view.w * aspect) / 2 };
        }

        // factor > 1 zooms in. The spot under `at` stays where it is on screen.
        function zoomAt(factor, at) {
            const w = Math.min(maxW(), Math.max(minW, view.w / factor));
            const k = w / view.w;
            setView({ w: w, x: at.x - (at.x - view.x) * k, y: at.y - (at.y - view.y) * k });
        }

        function panByPixels(dx, dy) {
            const ctm = svg.getScreenCTM();
            if (!ctm || !ctm.a) {
                return;
            }
            setView({ w: view.w, x: view.x - dx / ctm.a, y: view.y - dy / ctm.d });
        }

        // Starting view: zoomed in enough that cells are easy to tap, centred on the board.
        function startView() {
            const pxWide = svg.getBoundingClientRect().width;
            const fit = fitView();
            if (!pxWide || (pxWide / fit.w) * cell >= MIN_START_CELL_PX) {
                return fit;
            }
            const w = (pxWide / START_CELL_PX) * cell;
            return { w: w, x: (fullW - w) / 2, y: (fullH - w * aspect) / 2 };
        }

        function pinchInfo() {
            const [a, b] = Array.from(pointers.values());
            return { dist: Math.hypot(a.x - b.x, a.y - b.y) || 1, midX: (a.x + b.x) / 2, midY: (a.y + b.y) / 2 };
        }

        svg.addEventListener('wheel', event => {
            event.preventDefault();
            const lines = event.deltaMode === 1 ? 33 : 1; // Firefox sometimes reports lines instead of pixels
            zoomAt(Math.exp(-event.deltaY * lines * WHEEL_SPEED), toBoard(event.clientX, event.clientY));
        }, { passive: false });

        svg.addEventListener('pointerdown', event => {
            if (event.pointerType === 'mouse' && event.button !== 0) {
                return;
            }
            pointers.set(event.pointerId, { x: event.clientX, y: event.clientY, startX: event.clientX, startY: event.clientY });
            if (pointers.size === 1) {
                dragged = false;
            } else if (pointers.size === 2) {
                dragged = true; // a pinch is never a tap
                pinch = pinchInfo();
            }
        });

        svg.addEventListener('pointermove', event => {
            const p = pointers.get(event.pointerId);
            if (!p) {
                return;
            }
            const prevX = p.x;
            const prevY = p.y;
            p.x = event.clientX;
            p.y = event.clientY;

            if (pointers.size >= 2 && pinch) {
                const now = pinchInfo();
                zoomAt(now.dist / pinch.dist, toBoard(now.midX, now.midY));
                panByPixels(now.midX - pinch.midX, now.midY - pinch.midY);
                pinch = now;
                return;
            }

            if (!dragged) {
                if (Math.hypot(p.x - p.startX, p.y - p.startY) < DRAG_THRESHOLD) {
                    return;
                }
                dragged = true;
                // Only capture once it's really a drag. Capturing earlier would break the click for a normal tap.
                svg.setPointerCapture(event.pointerId);
                svg.classList.add('is-panning');
            }
            panByPixels(p.x - prevX, p.y - prevY);
        });

        function release(event) {
            pointers.delete(event.pointerId);
            if (pointers.size < 2) {
                pinch = null;
            }
            if (pointers.size === 0) {
                svg.classList.remove('is-panning');
            }
        }

        svg.addEventListener('pointerup', release);
        svg.addEventListener('pointercancel', release);

        // Capture phase, so this runs before the game's click handler on the board.
        svg.addEventListener('click', event => {
            if (dragged) {
                event.stopPropagation();
                event.preventDefault();
                dragged = false;
            }
        }, true);

        // Turning the phone or resizing the window changes the shape. Keep the same centre and zoom.
        if (window.ResizeObserver) {
            new ResizeObserver(() => {
                const c = center();
                aspect = measureAspect();
                setView({ w: view.w, x: c.x - view.w / 2, y: c.y - (view.w * aspect) / 2 });
            }).observe(svg);
        }

        setView(initialView || startView());

        return {
            zoomIn: () => zoomAt(STEP, center()),
            zoomOut: () => zoomAt(1 / STEP, center()),
            fit: () => setView(fitView()),
            getView: () => ({ x: view.x, y: view.y, w: view.w }),

            // Pans (same zoom) so these cells are on screen, e.g. when a hint points at an arrow off screen.
            reveal(cells) {
                if (!cells || cells.length === 0) {
                    return;
                }
                const xs = cells.map(c => c.x * cell + cell / 2);
                const ys = cells.map(c => c.y * cell + cell / 2);
                const minX = Math.min(...xs) - cell;
                const maxX = Math.max(...xs) + cell;
                const minY = Math.min(...ys) - cell;
                const maxY = Math.max(...ys) + cell;
                const h = view.w * aspect;
                if (minX >= view.x && maxX <= view.x + view.w && minY >= view.y && maxY <= view.y + h) {
                    return;
                }
                setView({ w: view.w, x: (minX + maxX) / 2 - view.w / 2, y: (minY + maxY) / 2 - h / 2 });
            }
        };
    }

    root.ArrowOutViewport = Object.freeze({ attach: attach });
}(window));
