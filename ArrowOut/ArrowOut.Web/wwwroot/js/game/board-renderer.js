// Draws the board as SVG, for both the game and the level editor. Everything is built with
// createElementNS/textContent and never innerHTML, so data from the API can't inject markup.
//
// Each arrow is a line from the tail through every cell to the head, plus an arrowhead.
// The line is drawn on a longer "track" (the body plus the straight path past the head), and
// moving the arrow just slides a dash of the same length along that track. That way the body
// follows the head around corners like a train, which is exactly how the rules work too.
(function (root) {
    'use strict';

    const SVG_NS = 'http://www.w3.org/2000/svg';
    const CELL = 60;
    const HALF = CELL / 2;
    const TAIL_OVERHANG = 17; // body starts slightly before the tail cell centre
    const HEAD_TIP = 22;      // arrow tip sits ahead of the head cell centre
    const PENCIL_MAX_ARROWS = 120; // more arrows than this and the pencil filter makes panning laggy
    const Engine = root.ArrowOutEngine;

    function el(name, attributes) {
        const node = document.createElementNS(SVG_NS, name);
        for (const [key, value] of Object.entries(attributes || {})) {
            node.setAttribute(key, String(value));
        }
        return node;
    }

    function center(cell) {
        return { x: cell.x * CELL + HALF, y: cell.y * CELL + HALF };
    }

    function directionLabel(direction) {
        return Engine.DirectionNames[direction].toLowerCase();
    }

    // Track = a bit before the tail, then every cell centre up to the head, then a point far ahead of the head.
    function buildTrack(arrow, boardSide) {
        const cells = arrow.cells.slice().reverse(); // tail first
        const points = cells.map(center);
        const d = Engine.Deltas[arrow.direction];

        // Which way the tail points (for a one-cell arrow that's just the head direction).
        const t0 = points[0];
        const t1 = points.length > 1 ? points[1] : { x: t0.x + d.x, y: t0.y + d.y };
        const tx = Math.sign(t1.x - t0.x);
        const ty = Math.sign(t1.y - t0.y);
        points.unshift({ x: t0.x - tx * TAIL_OVERHANG, y: t0.y - ty * TAIL_OVERHANG });

        const head = points[points.length - 1];
        const reach = (boardSide + arrow.length + 3) * CELL;
        points.push({ x: head.x + d.x * reach, y: head.y + d.y * reach });

        const lengths = [0];
        for (let i = 1; i < points.length; i++) {
            lengths.push(lengths[i - 1] + Math.hypot(points[i].x - points[i - 1].x, points[i].y - points[i - 1].y));
        }

        return {
            points: points,
            lengths: lengths,
            headArc: TAIL_OVERHANG + (arrow.length - 1) * CELL, // how far along the track the head centre is when the arrow hasn't moved
            d: 'M' + points.map(p => p.x + ' ' + p.y).join(' L')
        };
    }

    // Position and angle (in degrees) at some distance along the track.
    function pointAt(track, arc) {
        const { points, lengths } = track;
        let i = 1;
        while (i < lengths.length - 1 && lengths[i] < arc) {
            i++;
        }
        const a = points[i - 1];
        const b = points[i];
        const segment = lengths[i] - lengths[i - 1] || 1;
        const t = Math.min(1, Math.max(0, (arc - lengths[i - 1]) / segment));
        return {
            x: a.x + (b.x - a.x) * t,
            y: a.y + (b.y - a.y) * t,
            angle: Math.atan2(b.y - a.y, b.x - a.x) * 180 / Math.PI
        };
    }

    // Moves the arrow `offset` units along its track.
    function setOffset(group, offset) {
        const state = group.__ao;
        // The line keeps going into the open "V" head, like a hand-drawn arrow.
        const bodyLength = state.track.headArc + HEAD_TIP - 4;
        const dashArray = bodyLength + ' ' + (state.trackLength + 10);
        // The line and its faint second pencil stroke are the same path, so move both.
        for (const layer of state.layers) {
            layer.setAttribute('stroke-dashoffset', String(-offset));
            layer.setAttribute('stroke-dasharray', dashArray);
        }
        const p = pointAt(state.track, state.track.headArc + offset);
        state.head.setAttribute('transform', 'translate(' + p.x + ' ' + p.y + ') rotate(' + p.angle + ')');
        state.offset = offset;
    }

    const easings = {
        linear: t => t,
        accelerate: t => t * t,
        inOut: t => (t < 0.5 ? 2 * t * t : 1 - Math.pow(-2 * t + 2, 2) / 2),
        decelerate: t => 1 - (1 - t) * (1 - t)
    };

    // Animates the arrow along its track. The promise resolves when it's done.
    function slide(group, toOffset, duration, easing) {
        const from = group.__ao.offset || 0;
        const ease = easings[easing] || easings.inOut;
        return new Promise(resolve => {
            if (duration <= 1) {
                setOffset(group, toOffset);
                resolve();
                return;
            }
            const start = performance.now();
            function frame(now) {
                const t = Math.min(1, (now - start) / duration);
                setOffset(group, from + (toOffset - from) * ease(t));
                if (t < 1) {
                    requestAnimationFrame(frame);
                } else {
                    resolve();
                }
            }
            requestAnimationFrame(frame);
        });
    }

    // Builds one arrow: a click area for each cell, the line (plus a faint second pencil stroke)
    // and the "V" head.
    function createArrow(arrow, boardSide) {
        const track = buildTrack(arrow, boardSide);
        const group = el('g', {
            'class': 'ao-arrow',
            'data-id': arrow.id,
            'tabindex': 0,
            'role': 'button',
            'aria-label': 'Arrow pointing ' + directionLabel(arrow.direction) + ', ' + arrow.length + ' cells'
        });

        for (const cell of arrow.cells) {
            group.appendChild(el('rect', {
                'class': 'ao-arrow-hit', x: cell.x * CELL + 2, y: cell.y * CELL + 2, width: CELL - 4, height: CELL - 4, rx: 10
            }));
        }

        const body = el('path', { 'class': 'ao-arrow-body', d: track.d });
        // A second stroke slightly to the side, like the pencil went over the line twice.
        const sketch = el('path', { 'class': 'ao-arrow-sketch', d: track.d, transform: 'translate(1.5 -1)' });
        const tip = HEAD_TIP - 2;
        const head = el('g', { 'class': 'ao-arrow-head' });
        head.appendChild(el('path', { d: 'M-6 -14 L' + tip + ' 0 L-6 14' }));
        head.appendChild(el('path', { 'class': 'ao-arrow-sketch', d: 'M-5 -15.5 L' + (tip - 1) + ' 1 L-7 13' }));
        group.appendChild(body);
        group.appendChild(sketch);
        group.appendChild(head);

        group.__ao = {
            track: track,
            trackLength: track.lengths[track.lengths.length - 1],
            layers: [body, sketch],
            head: head,
            offset: 0
        };
        setOffset(group, 0);
        return group;
    }

    // The pencil filter: wobble the lines a little with noise, then rub some grain out of them.
    function createPencilFilter() {
        const defs = el('defs');
        const filter = el('filter', { id: 'ao-pencil', x: '-5%', y: '-5%', width: '110%', height: '110%' });
        filter.appendChild(el('feTurbulence', { type: 'fractalNoise', baseFrequency: 0.06, numOctaves: 2, seed: 3, result: 'wobble' }));
        filter.appendChild(el('feDisplacementMap', { 'in': 'SourceGraphic', in2: 'wobble', scale: 3, xChannelSelector: 'R', yChannelSelector: 'G', result: 'drawn' }));
        filter.appendChild(el('feTurbulence', { type: 'fractalNoise', baseFrequency: 0.3, numOctaves: 1, seed: 8, result: 'noise' }));
        // Turn the noise into a mask that keeps about 80-100% of each pixel, like graphite on paper.
        filter.appendChild(el('feColorMatrix', { 'in': 'noise', type: 'matrix', values: '0 0 0 0 0  0 0 0 0 0  0 0 0 0 0  0 0 0 -.5 1.3', result: 'grain' }));
        filter.appendChild(el('feComposite', { 'in': 'drawn', in2: 'grain', operator: 'in' }));
        defs.appendChild(filter);
        return defs;
    }

    // Draws a board into the container and returns what the game and editor need to work with it.
    // Pass options.cells = true to get clickable cells (the editor uses that).
    function render(container, width, height, arrows, options) {
        const opts = options || {};
        const side = Math.max(width, height);
        const svg = el('svg', {
            viewBox: '0 0 ' + width * CELL + ' ' + height * CELL,
            preserveAspectRatio: 'xMidYMid meet'
        });

        const cellLayer = el('g', { 'class': 'ao-cells' });
        const arrowLayer = el('g', { 'class': 'ao-arrows' });

        if (opts.cells) {
            for (let y = 0; y < height; y++) {
                for (let x = 0; x < width; x++) {
                    cellLayer.appendChild(el('rect', {
                        'class': 'ao-cell', x: x * CELL + 1, y: y * CELL + 1, width: CELL - 2, height: CELL - 2, rx: 8,
                        'data-x': x, 'data-y': y
                    }));
                }
            }
        }

        // Graph-paper lines along the cell borders (the arrows go through the middle of the squares).
        // It's all one path, so it's cheap even on an 80x80 board. CSS shows or hides it.
        let gridPath = '';
        for (let x = 0; x <= width; x++) {
            gridPath += 'M' + x * CELL + ' 0V' + height * CELL;
        }
        for (let y = 0; y <= height; y++) {
            gridPath += 'M0 ' + y * CELL + 'H' + width * CELL;
        }
        const gridLayer = el('path', { 'class': 'ao-grid-lines', d: gridPath, 'aria-hidden': 'true' });

        const elements = new Map();
        for (const arrow of arrows) {
            const node = createArrow(arrow, side);
            elements.set(arrow.id, node);
            arrowLayer.appendChild(node);
        }

        // Pencil grain and wobble (cartoon.css turns it on). The filter gets recalculated on every pan
        // and every animation frame, so only small boards get it, otherwise it gets laggy.
        if (arrows.length <= PENCIL_MAX_ARROWS) {
            svg.appendChild(createPencilFilter());
            svg.classList.add('is-sketchy');
        }

        svg.appendChild(cellLayer);
        svg.appendChild(gridLayer);
        svg.appendChild(arrowLayer);

        container.replaceChildren(svg);
        return { svg: svg, elements: elements, arrowLayer: arrowLayer };
    }

    // Which cell was clicked (for drawing in the editor).
    function cellFromEvent(svg, event, width, height) {
        const rect = svg.getBoundingClientRect();
        const scale = Math.min(rect.width / (width * CELL), rect.height / (height * CELL));
        const offsetX = (rect.width - width * CELL * scale) / 2;
        const offsetY = (rect.height - height * CELL * scale) / 2;
        const x = Math.floor((event.clientX - rect.left - offsetX) / (CELL * scale));
        const y = Math.floor((event.clientY - rect.top - offsetY) / (CELL * scale));
        return x >= 0 && y >= 0 && x < width && y < height ? { x: x, y: y } : null;
    }

    root.ArrowOutRenderer = Object.freeze({
        render: render,
        createArrow: createArrow,
        slide: slide,
        setOffset: setOffset,
        cellFromEvent: cellFromEvent,
        CELL: CELL
    });
}(window));
