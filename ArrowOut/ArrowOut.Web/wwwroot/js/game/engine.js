// The game rules in JavaScript, a copy of ArrowOut.Game (C#) so taps feel instant.
// The server still has the last word: every win is replayed there before it counts.
// The wrapper at the bottom lets the same file run in the browser (window.ArrowOutEngine) and in Node for the tests.
(function (root, factory) {
    'use strict';
    if (typeof module === 'object' && module.exports) {
        module.exports = factory();
    } else {
        root.ArrowOutEngine = factory();
    }
}(typeof self !== 'undefined' ? self : this, function () {
    'use strict';

    const Direction = Object.freeze({ Up: 0, Right: 1, Down: 2, Left: 3 });
    const DirectionNames = Object.freeze(['Up', 'Right', 'Down', 'Left']);
    const Deltas = Object.freeze([
        Object.freeze({ x: 0, y: -1 }),
        Object.freeze({ x: 1, y: 0 }),
        Object.freeze({ x: 0, y: 1 }),
        Object.freeze({ x: -1, y: 0 })
    ]);

    // Takes 0-3 or "Up"/"Right"/"Down"/"Left" (the API sends enums as strings).
    function normalizeDirection(value) {
        if (typeof value === 'number' && value >= 0 && value <= 3) {
            return value;
        }
        const index = DirectionNames.indexOf(String(value));
        if (index < 0) {
            throw new Error('Unknown direction: ' + value);
        }
        return index;
    }

    function straightCells(x, y, direction, length) {
        const d = Deltas[direction];
        const cells = [];
        for (let i = 0; i < length; i++) {
            cells.push({ x: x - d.x * i, y: y - d.y * i });
        }
        return cells;
    }

    // Cleans up an arrow from the API or the editor. Bent arrows have a `cells` list, head first,
    // straight ones can leave it out. Checks the same rules as ArrowPiece.cs (cells connected,
    // no cell twice, head lined up with the body).
    function normalizeArrow(raw) {
        const direction = normalizeDirection(raw.direction);
        const cells = Array.isArray(raw.cells) && raw.cells.length > 0
            ? raw.cells.map(c => ({ x: Number(c.x), y: Number(c.y) }))
            : straightCells(Number(raw.x), Number(raw.y), direction, Number(raw.length));

        if (cells.length < 1 || cells.length > 64) {
            throw new Error('Invalid arrow length: ' + cells.length);
        }

        const seen = new Set();
        cells.forEach((c, i) => {
            if (!Number.isInteger(c.x) || !Number.isInteger(c.y)) {
                throw new Error('Invalid cell');
            }
            const key = c.x + ',' + c.y;
            if (seen.has(key)) {
                throw new Error('Arrow path visits a cell twice');
            }
            seen.add(key);
            if (i > 0 && Math.abs(c.x - cells[i - 1].x) + Math.abs(c.y - cells[i - 1].y) !== 1) {
                throw new Error('Arrow path is not continuous');
            }
        });

        const d = Deltas[direction];
        if (cells.length > 1 && (cells[1].x !== cells[0].x - d.x || cells[1].y !== cells[0].y - d.y)) {
            throw new Error('Arrow head must continue its last segment');
        }

        return { id: Number(raw.id), x: cells[0].x, y: cells[0].y, direction: direction, length: cells.length, cells: cells };
    }

    // Head first.
    function cellsOf(arrow) {
        return arrow.cells;
    }

    class Board {
        constructor(width, height, arrows) {
            this.width = width;
            this.height = height;
            this.arrows = new Map();
            this.grid = new Array(width * height).fill(0);

            for (const raw of arrows) {
                const arrow = normalizeArrow(raw);
                if (this.arrows.has(arrow.id)) {
                    throw new Error('Duplicate arrow id ' + arrow.id);
                }
                for (const cell of cellsOf(arrow)) {
                    if (!this.inside(cell.x, cell.y)) {
                        throw new Error('Arrow ' + arrow.id + ' is outside the board');
                    }
                    if (this.grid[this.index(cell.x, cell.y)] !== 0) {
                        throw new Error('Arrow ' + arrow.id + ' overlaps another arrow');
                    }
                    this.grid[this.index(cell.x, cell.y)] = arrow.id;
                }
                this.arrows.set(arrow.id, arrow);
            }
        }

        index(x, y) { return y * this.width + x; }

        inside(x, y) { return x >= 0 && y >= 0 && x < this.width && y < this.height; }

        get count() { return this.arrows.size; }

        get isCleared() { return this.arrows.size === 0; }

        has(id) { return this.arrows.has(id); }

        get(id) { return this.arrows.get(id); }

        arrowAt(x, y) {
            if (!this.inside(x, y)) {
                return null;
            }
            const id = this.grid[this.index(x, y)];
            return id === 0 ? null : id;
        }

        remainingIds() { return Array.from(this.arrows.keys()).sort((a, b) => a - b); }

        // What a tap would do: { success, distance, blockerId }. Distance is in cells.
        peek(id) {
            const arrow = this.arrows.get(id);
            if (!arrow) {
                throw new Error('Arrow ' + id + ' is not on the board');
            }
            const d = Deltas[arrow.direction];
            let x = arrow.x + d.x;
            let y = arrow.y + d.y;
            let free = 0;
            while (this.inside(x, y)) {
                const occupant = this.grid[this.index(x, y)];
                if (occupant !== 0) {
                    return { success: false, distance: free, blockerId: occupant, arrowId: id };
                }
                free++;
                x += d.x;
                y += d.y;
            }
            return { success: true, distance: free + arrow.length, blockerId: null, arrowId: id };
        }

        // Taps an arrow. It's removed if it gets out.
        move(id) {
            const result = this.peek(id);
            if (result.success) {
                const arrow = this.arrows.get(id);
                for (const cell of cellsOf(arrow)) {
                    this.grid[this.index(cell.x, cell.y)] = 0;
                }
                this.arrows.delete(id);
            }
            return result;
        }

        freeIds() { return this.remainingIds().filter(id => this.peek(id).success); }

        clone() { return new Board(this.width, this.height, Array.from(this.arrows.values())); }

        // Taking free arrows out in any order always works, same reasoning as GreedySolver.cs.
        solve() {
            const working = this.clone();
            const order = [];
            let progressed = true;
            while (progressed && !working.isCleared) {
                progressed = false;
                for (const id of working.freeIds()) {
                    working.move(id);
                    order.push(id);
                    progressed = true;
                }
            }
            return { solvable: working.isCleared, order: order, stuck: working.remainingIds() };
        }
    }

    return Object.freeze({ Board, Direction, DirectionNames, Deltas, cellsOf, normalizeDirection, normalizeArrow });
}));
