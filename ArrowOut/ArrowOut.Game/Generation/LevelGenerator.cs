namespace ArrowOut.Game.Generation;

// Builds maze boards: bendy arrows covering the whole grid, always solvable.
//
// The trick: _snakes holds the arrows in reverse solving order. An arrow's "ray" is the
// straight line from its head to the edge, and it's only allowed to cross arrows that were
// added after it, because those get removed first. So a new arrow just needs an empty ray
// when it's placed. Anything can grow into that ray later.
//
// To fill every cell, heads go on the innermost free cells first and point at the nearest
// edge, so their rays go through outer cells that are still empty. Bodies wander around
// with bends. Leftover gaps get fixed at the end, either by growing a neighbour's tail into
// them or by adding a one-cell arrow somewhere in the order where it's allowed.
// Every finished board is double-checked with GreedySolver.
public sealed class LevelGenerator
{
    private static readonly Direction[] Directions = [Direction.Up, Direction.Right, Direction.Down, Direction.Left];

    private readonly Random _random;

    public LevelGenerator(Random random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    public IReadOnlyList<ArrowPiece> Generate(GeneratorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.EnsureValid();

        var solver = new Solving.GreedySolver();
        const int PreferredAttempts = 6;
        List<Snake>? best = null;
        var bestScore = int.MaxValue;
        var fullBoards = 0;

        for (var attempt = 0; attempt < settings.MaxAttempts; attempt++)
        {
            var build = new Build(settings, _random);
            var snakes = build.Run();
            if (snakes.Count == 0)
            {
                continue;
            }

            var score = (build.Gaps * 1000) + build.Stubs;
            var pieces = ToPieces(snakes);
            if (!solver.Solve(Board.Create(settings.Width, settings.Height, pieces)).IsSolvable)
            {
                continue; // shouldn't happen, but just in case
            }

            if (score < bestScore)
            {
                best = snakes;
                bestScore = score;
            }

            // Keep the best of a few tries (fewer one-cell stubs looks more like a maze).
            if (settings.TargetFill < 1.0 || (build.Gaps == 0 && ++fullBoards >= PreferredAttempts) || bestScore == 0)
            {
                break;
            }
        }

        if (best is null)
        {
            throw new InvalidOperationException("The generator could not build a board.");
        }

        return Renumber(Shuffle(ToPieces(best)));
    }

    private static List<ArrowPiece> ToPieces(List<Snake> snakes) =>
        snakes.Select((s, i) => new ArrowPiece(i + 1, s.Cells, s.Direction)).ToList();

    private List<ArrowPiece> Shuffle(List<ArrowPiece> arrows)
    {
        // Shuffle so the ids don't give away the solving order.
        var copy = arrows.ToList();
        for (var i = copy.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (copy[i], copy[j]) = (copy[j], copy[i]);
        }

        return copy;
    }

    private static List<ArrowPiece> Renumber(List<ArrowPiece> arrows) =>
        arrows.Select((a, i) => a.WithId(i + 1)).ToList();

    private sealed class Snake(GridPoint head, Direction direction, HashSet<GridPoint> ray)
    {
        public List<GridPoint> Cells { get; } = [head];

        public Direction Direction { get; } = direction;

        public HashSet<GridPoint> Ray { get; } = ray;
    }

    // One attempt at building a board. All the working state lives here.
    private sealed class Build(GeneratorSettings settings, Random random)
    {
        private readonly int _width = settings.Width;
        private readonly int _height = settings.Height;
        private readonly int[,] _owner = CreateGrid(settings.Width, settings.Height);
        private readonly List<Snake> _snakes = [];
        private int _filled;

        public int Gaps => (_width * _height) - _filled;

        public int Stubs => _snakes.Count(s => s.Cells.Count == 1);

        public List<Snake> Run()
        {
            var target = (int)Math.Ceiling(_width * _height * settings.TargetFill);

            while (_filled < target && TryPlaceSnake())
            {
            }

            if (settings.TargetFill >= 1.0)
            {
                RepairGaps();
                AbsorbStubs();

                if (settings.HugWalls)
                {
                    MergeShortArrows(maxLength: 3);
                }
            }

            return _snakes;
        }

        private static int[,] CreateGrid(int width, int height)
        {
            var grid = new int[width, height];
            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    grid[x, y] = -1;
                }
            }

            return grid;
        }

        private bool Inside(GridPoint p) => BoardValidator.IsInside(_width, _height, p);

        private bool IsEmpty(GridPoint p) => Inside(p) && _owner[p.X, p.Y] < 0;

        private int Depth(GridPoint p) => Math.Min(Math.Min(p.X, p.Y), Math.Min(_width - 1 - p.X, _height - 1 - p.Y));

        private int FreeNeighbours(GridPoint p) => Directions.Count(d => IsEmpty(p.Step(d)));

        private int RayLength(GridPoint head, Direction direction) => direction switch
        {
            Direction.Up => head.Y,
            Direction.Right => _width - 1 - head.X,
            Direction.Down => _height - 1 - head.Y,
            _ => head.X,
        };

        // clear[d][x, y] is true if every cell after (x, y) up to the edge in direction d is empty.
        // The first index is the Direction value.
        private bool[][,] ClearRays()
        {
            var up = new bool[_width, _height];
            var right = new bool[_width, _height];
            var down = new bool[_width, _height];
            var left = new bool[_width, _height];

            for (var x = 0; x < _width; x++)
            {
                up[x, 0] = true;
                for (var y = 1; y < _height; y++)
                {
                    up[x, y] = up[x, y - 1] && _owner[x, y - 1] < 0;
                }

                down[x, _height - 1] = true;
                for (var y = _height - 2; y >= 0; y--)
                {
                    down[x, y] = down[x, y + 1] && _owner[x, y + 1] < 0;
                }
            }

            for (var y = 0; y < _height; y++)
            {
                left[0, y] = true;
                for (var x = 1; x < _width; x++)
                {
                    left[x, y] = left[x - 1, y] && _owner[x - 1, y] < 0;
                }

                right[_width - 1, y] = true;
                for (var x = _width - 2; x >= 0; x--)
                {
                    right[x, y] = right[x + 1, y] && _owner[x + 1, y] < 0;
                }
            }

            return [up, right, down, left];
        }

        private HashSet<GridPoint> RayOf(GridPoint head, Direction direction)
        {
            var ray = new HashSet<GridPoint>();
            for (var c = head.Step(direction); Inside(c); c = c.Step(direction))
            {
                ray.Add(c);
            }

            return ray;
        }

        private bool TryPlaceSnake()
        {
            // Possible heads are empty cells with an empty ray. Deeper cells go first so the outer
            // rings stay free for later rays, and an empty cell behind the head gets a bonus (fewer stubs).
            // The "is the ray empty" info comes from four sweeps over the whole grid instead of checking
            // each candidate on its own, which is a lot faster on big boards. It visits candidates in the
            // same order with the same random numbers as the slow way, so the boards come out identical.
            var clear = ClearRays();

            // Tangle 0: heads point at the nearest edge (short rays), so the board clears from the edges.
            // Tangle 1: long rays across the board are preferred and positions are a lot more random,
            // so arrows point everywhere and block each other in long chains.
            // These weights only change the look. Every board is still solvable.
            var tangle = settings.Tangle;
            var depthWeight = 10 * (1 - (0.9 * tangle));
            var rayWeight = -0.5 + (3.0 * tangle);
            var noise = 6 + (30 * tangle);

            var found = false;
            var bestScore = double.MinValue;
            GridPoint bestHead = default;
            Direction bestDirection = default;

            for (var x = 0; x < _width; x++)
            {
                for (var y = 0; y < _height; y++)
                {
                    var head = new GridPoint(x, y);
                    if (!IsEmpty(head))
                    {
                        continue;
                    }

                    foreach (var direction in Directions)
                    {
                        if (!clear[(int)direction][x, y])
                        {
                            continue;
                        }

                        var behind = head.Step(direction.Opposite());
                        var behindFree = IsEmpty(behind) ? 15 + (FreeNeighbours(behind) * 3) : -25;
                        var score = (Depth(head) * depthWeight) + behindFree + (RayLength(head, direction) * rayWeight) + (random.NextDouble() * noise);
                        if (score > bestScore)
                        {
                            (found, bestScore, bestHead, bestDirection) = (true, score, head, direction);
                        }
                    }
                }
            }

            if (!found)
            {
                return false;
            }

            var snake = new Snake(bestHead, bestDirection, RayOf(bestHead, bestDirection));
            Grow(snake, random.Next(settings.MinLength, settings.MaxLength + 1));
            Commit(snake);
            return true;
        }

        private void Grow(Snake snake, int targetLength)
        {
            // The first body cell has to be right behind the head, otherwise the arrowhead would point sideways.
            var behind = snake.Cells[0].Step(snake.Direction.Opposite());
            if (targetLength < 2 || !IsEmpty(behind) || snake.Ray.Contains(behind))
            {
                return;
            }

            snake.Cells.Add(behind);
            var heading = snake.Direction.Opposite(); // which way the body is growing

            while (snake.Cells.Count < targetLength)
            {
                var last = snake.Cells[^1];
                var options = Directions
                    .Where(d => d != heading.Opposite())
                    .Select(d => (Direction: d, Cell: last.Step(d)))
                    .Where(o => IsEmpty(o.Cell) && !snake.Cells.Contains(o.Cell) && !snake.Ray.Contains(o.Cell))
                    .ToList();

                if (options.Count == 0)
                {
                    break;
                }

                (Direction Direction, GridPoint Cell) pick;
                if (settings.HugWalls)
                {
                    pick = PickHugging(snake, options, heading);
                }
                else
                {
                    var straight = options.Where(o => o.Direction == heading).ToList();
                    var turns = options.Where(o => o.Direction != heading).ToList();
                    var takeTurn = turns.Count > 0 && (straight.Count == 0 || random.NextDouble() < settings.BendChance);
                    pick = takeTurn ? turns[random.Next(turns.Count)] : straight[0];
                }

                snake.Cells.Add(pick.Cell);
                heading = pick.Direction;
            }
        }

        // Pick the neighbour with the fewest free cells around it. That makes bodies run along walls
        // and other arrows, instead of cutting the empty space into little pockets that end up as
        // one- or two-cell stubs. On a tie it goes straight, and BendChance sometimes forces a turn
        // so it doesn't get boring.
        private (Direction Direction, GridPoint Cell) PickHugging(Snake snake, List<(Direction Direction, GridPoint Cell)> options, Direction heading)
        {
            var turns = options.Where(o => o.Direction != heading).ToList();
            if (turns.Count > 0 && random.NextDouble() < settings.BendChance * 0.25)
            {
                return turns[random.Next(turns.Count)];
            }

            return options.MinBy(o =>
                Directions.Count(d =>
                {
                    var next = o.Cell.Step(d);
                    return IsEmpty(next) && !snake.Cells.Contains(next) && !snake.Ray.Contains(next);
                })
                + (o.Direction == heading ? 0 : 0.5)
                + (random.NextDouble() * 0.4));
        }

        private void Commit(Snake snake)
        {
            var index = _snakes.Count;
            _snakes.Add(snake);
            foreach (var cell in snake.Cells)
            {
                _owner[cell.X, cell.Y] = index;
                _filled++;
            }
        }

        private void RepairGaps()
        {
            bool progressed;
            do
            {
                progressed = false;
                for (var x = 0; x < _width; x++)
                {
                    for (var y = 0; y < _height; y++)
                    {
                        var gap = new GridPoint(x, y);
                        if (IsEmpty(gap) && (TryExtendTail(gap) || TryInsertSingle(gap)))
                        {
                            progressed = true;
                        }
                    }
                }
            }
            while (progressed && Gaps > 0);
        }

        // Adds the gap to a neighbouring tail, as long as no later arrow's ray (or its own) goes through it.
        private bool TryExtendTail(GridPoint gap) => ExtendTail(gap) is not null;

        // Same as TryExtendTail but returns the arrow that grew, or null.
        private Snake? ExtendTail(GridPoint gap)
        {
            var order = Enumerable.Range(0, _snakes.Count).OrderBy(_ => random.Next()).ToList();
            var lastRayOverGap = LastIndexWithGapInRay(gap);
            foreach (var i in order)
            {
                var snake = _snakes[i];
                var tail = snake.Cells[^1];
                if (Math.Abs(tail.X - gap.X) + Math.Abs(tail.Y - gap.Y) != 1
                    || snake.Cells.Count >= ArrowPiece.MaxLength
                    || snake.Ray.Contains(gap)
                    || (snake.Cells.Count == 1 && gap != tail.Step(snake.Direction.Opposite())))
                {
                    continue;
                }

                if (i < lastRayOverGap)
                {
                    continue; // a later arrow's ray goes through the gap
                }

                snake.Cells.Add(gap);
                _owner[gap.X, gap.Y] = i;
                _filled++;
                return snake;
            }

            return null;
        }

        // Puts a one-cell arrow at position p in the order, where its ray misses every arrow before p
        // and no arrow after p has the gap in its ray.
        private bool TryInsertSingle(GridPoint gap)
        {
            // Nothing at index >= p can have the gap in its ray, so p has to be > lastRayOverGap.
            var lastRayOverGap = LastIndexWithGapInRay(gap);

            foreach (var direction in Directions.OrderBy(_ => random.Next()))
            {
                var ray = RayOf(gap, direction);
                for (var p = 0; p <= _snakes.Count; p++)
                {
                    // The ray has to miss every arrow before p. We check one more arrow each time p goes up.
                    if (p > 0 && _snakes[p - 1].Cells.Any(ray.Contains))
                    {
                        break; // it only gets worse for bigger p
                    }

                    if (p <= lastRayOverGap)
                    {
                        continue;
                    }

                    _snakes.Insert(p, new Snake(gap, direction, ray));
                    Reindex();
                    _filled++;
                    return true;
                }
            }

            return false;
        }

        // Merges one-cell arrows into a neighbour's tail where that's allowed. Taking the stub away
        // also takes away its ray, so this can't make the board unsolvable.
        private void AbsorbStubs()
        {
            bool progressed;
            do
            {
                progressed = false;
                for (var s = _snakes.Count - 1; s >= 0; s--)
                {
                    if (_snakes[s].Cells.Count != 1)
                    {
                        continue;
                    }

                    var cell = _snakes[s].Cells[0];
                    var stub = _snakes[s];
                    _snakes.RemoveAt(s);
                    _owner[cell.X, cell.Y] = -1;
                    _filled--;
                    Reindex();

                    if (TryExtendTail(cell))
                    {
                        progressed = true;
                        continue;
                    }

                    _snakes.Insert(s, stub); // couldn't merge it, put it back
                    _filled++;
                    Reindex();
                }
            }
            while (progressed);
        }

        // Gets rid of short arrows by giving each of their cells to a neighbouring tail (same rule
        // as TryExtendTail). Removing an arrow only removes its own ray, so the board stays
        // solvable. If even one cell can't be given away, the arrow is put back exactly how it was.
        private void MergeShortArrows(int maxLength)
        {
            bool progressed;
            do
            {
                progressed = false;
                for (var s = _snakes.Count - 1; s >= 0; s--)
                {
                    if (s >= _snakes.Count || _snakes[s].Cells.Count > maxLength)
                    {
                        continue;
                    }

                    var victim = _snakes[s];
                    _snakes.RemoveAt(s);
                    foreach (var cell in victim.Cells)
                    {
                        _owner[cell.X, cell.Y] = -1;
                        _filled--;
                    }

                    Reindex();

                    // Tails grow one cell at a time, so keep going until nothing else sticks.
                    var extended = new List<Snake>();
                    var pending = victim.Cells.ToList();
                    bool attached;
                    do
                    {
                        attached = false;
                        foreach (var cell in pending.ToList())
                        {
                            if (ExtendTail(cell) is not { } grown)
                            {
                                continue;
                            }

                            extended.Add(grown);
                            pending.Remove(cell);
                            attached = true;
                        }
                    }
                    while (attached && pending.Count > 0);

                    if (pending.Count == 0)
                    {
                        progressed = true;
                        continue;
                    }

                    // Undo: take the borrowed cells back off the tails (they're always the newest ones) and restore the arrow.
                    for (var e = extended.Count - 1; e >= 0; e--)
                    {
                        var tail = extended[e].Cells[^1];
                        extended[e].Cells.RemoveAt(extended[e].Cells.Count - 1);
                        _owner[tail.X, tail.Y] = -1;
                        _filled--;
                    }

                    _snakes.Insert(s, victim);
                    foreach (var cell in victim.Cells)
                    {
                        _filled++;
                    }

                    Reindex();
                }
            }
            while (progressed);
        }

        // Index of the last arrow whose ray covers this cell, or -1.
        private int LastIndexWithGapInRay(GridPoint cell)
        {
            for (var j = _snakes.Count - 1; j >= 0; j--)
            {
                if (_snakes[j].Ray.Contains(cell))
                {
                    return j;
                }
            }

            return -1;
        }

        private void Reindex()
        {
            for (var i = 0; i < _snakes.Count; i++)
            {
                foreach (var cell in _snakes[i].Cells)
                {
                    _owner[cell.X, cell.Y] = i;
                }
            }
        }
    }
}
