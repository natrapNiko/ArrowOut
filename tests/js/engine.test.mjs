// Run with: node --test tests/js
// Checks that the browser engine follows exactly the same rules as the C# one (ArrowOut.Game).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';

const require = createRequire(import.meta.url);
const { Board, normalizeDirection } = require('../../ArrowOut/ArrowOut.Web/wwwroot/js/game/engine.js');

const queue = () => new Board(5, 3, [
    { id: 1, x: 1, y: 1, direction: 'Right', length: 1 },
    { id: 2, x: 2, y: 1, direction: 'Right', length: 1 },
    { id: 3, x: 3, y: 1, direction: 'Right', length: 1 }
]);

test('directions accept names and numbers', () => {
    assert.equal(normalizeDirection('Left'), 3);
    assert.equal(normalizeDirection(2), 2);
    assert.throws(() => normalizeDirection('Sideways'));
});

test('exit distance includes the arrow length', () => {
    const board = new Board(5, 5, [{ id: 1, x: 2, y: 2, direction: 'Up', length: 2 }]);
    const result = board.move(1);
    assert.equal(result.success, true);
    assert.equal(result.distance, 4);
    assert.equal(board.isCleared, true);
});

test('collision reports blocker and free cells, board unchanged', () => {
    const board = new Board(5, 3, [
        { id: 1, x: 0, y: 1, direction: 'Right', length: 1 },
        { id: 2, x: 3, y: 1, direction: 'Up', length: 1 }
    ]);
    const result = board.move(1);
    assert.deepEqual([result.success, result.blockerId, result.distance], [false, 2, 2]);
    assert.equal(board.count, 2);
});

test('solver finds order and detects jams', () => {
    assert.deepEqual(queue().solve().order, [3, 2, 1]);
    const jam = new Board(4, 3, [
        { id: 1, x: 0, y: 1, direction: 'Right', length: 1 },
        { id: 2, x: 3, y: 1, direction: 'Left', length: 1 }
    ]);
    assert.deepEqual(jam.solve(), { solvable: false, order: [], stuck: [1, 2] });
});

test('invalid boards are rejected', () => {
    assert.throws(() => new Board(3, 3, [{ id: 1, x: 0, y: 0, direction: 'Right', length: 2 }]));
    assert.throws(() => new Board(4, 4, [
        { id: 1, x: 2, y: 1, direction: 'Right', length: 2 },
        { id: 2, x: 1, y: 1, direction: 'Up', length: 1 }
    ]));
    assert.throws(() => new Board(4, 4, [{ id: 1, x: 0, y: 0, direction: 'Up', length: 9 }]));
});

test('clone is independent', () => {
    const board = queue();
    const clone = board.clone();
    clone.move(3);
    assert.equal(board.count, 3);
    assert.equal(clone.count, 2);
});

test('bent arrows: only the ray ahead of the head matters', () => {
    const board = new Board(4, 4, [
        { id: 1, direction: 'Up', cells: [{ x: 1, y: 2 }, { x: 1, y: 3 }, { x: 2, y: 3 }, { x: 3, y: 3 }, { x: 3, y: 2 }] },
        { id: 2, x: 0, y: 0, direction: 'Left', length: 1 }
    ]);
    const result = board.move(1);
    assert.deepEqual([result.success, result.distance], [true, 7]);
});

test('bent arrows: broken paths are rejected', () => {
    assert.throws(() => new Board(4, 4, [{ id: 1, direction: 'Right', cells: [{ x: 1, y: 1 }, { x: 1, y: 2 }] }]));
    assert.throws(() => new Board(4, 4, [{ id: 1, direction: 'Up', cells: [{ x: 1, y: 1 }, { x: 3, y: 1 }] }]));
});
