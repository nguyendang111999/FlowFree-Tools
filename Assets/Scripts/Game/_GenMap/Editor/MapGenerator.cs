using System;
using System.Collections.Generic;
using Microsoft.Z3;

namespace FlowFree.GenMap
{
    // Builds the Z3 model and runs a CEGAR + random-pin loop on a worker thread.
    // Uses no UnityEngine engine calls (only pure data), so it is safe off the main thread.
    public class MapGenerator
    {
        private const int MaxFailedAttempts = 500;

        private readonly GenConfig _config;

        /// <summary>
        /// Creates a generator bound to the given configuration.
        /// </summary>
        public MapGenerator(GenConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// Builds the Z3 model and produces maps until the requested count is reached or a stop is requested,
        /// reporting each accepted map and status update through the callbacks.
        /// </summary>
        /// <param name="shouldStop">Polled between and during solves to allow cancellation.</param>
        /// <param name="onMap">Invoked with each accepted map grid.</param>
        /// <param name="onStatus">Invoked with progress messages for the UI.</param>
        public void Run(Func<bool> shouldStop, Action<int[,]> onMap, Action<string> onStatus)
        {
            int w = _config.Width;
            int h = _config.Height;
            int n = _config.ColorCount;

            using (Context ctx = new Context())
            {
                IntExpr[,] c = new IntExpr[w, h];
                for (int x = 0; x < w; x++)
                {
                    for (int y = 0; y < h; y++)
                    {
                        c[x, y] = (IntExpr)ctx.MkIntConst($"c_{x}_{y}");
                    }
                }

                IntExpr one = ctx.MkInt(1);
                IntExpr zero = ctx.MkInt(0);

                // Same-color neighbor count per cell.
                ArithExpr[,] deg = new ArithExpr[w, h];
                for (int x = 0; x < w; x++)
                {
                    for (int y = 0; y < h; y++)
                    {
                        List<ArithExpr> terms = new List<ArithExpr>();
                        AddNeighborTerm(ctx, c, terms, x, y, x - 1, y, one, zero, w, h);
                        AddNeighborTerm(ctx, c, terms, x, y, x + 1, y, one, zero, w, h);
                        AddNeighborTerm(ctx, c, terms, x, y, x, y - 1, one, zero, w, h);
                        AddNeighborTerm(ctx, c, terms, x, y, x, y + 1, one, zero, w, h);
                        deg[x, y] = ctx.MkAdd(terms.ToArray());
                    }
                }

                Solver solver = ctx.MkSolver();
                Params p = ctx.MkParams();
                p.Add("timeout", (uint)_config.PerSolveTimeoutMs);
                p.Add("random_seed", (uint)_config.Seed);
                solver.Parameters = p;

                AssertBaseConstraints(ctx, solver, c, deg, w, h, n, one, zero, _config.MinLength, _config.MaxLength);

                System.Random rng = new System.Random(_config.Seed);
                int produced = 0;
                int attempts = 0;
                int failed = 0;

                while (produced < _config.MapCount && !shouldStop())
                {
                    bool accepted = false;
                    int[,] captured = null;

                    solver.Push();
                    try
                    {
                        AddRandomPins(ctx, solver, c, rng, w, h, n);

                        while (!shouldStop())
                        {
                            attempts++;
                            Status st = solver.Check();
                            if (st != Status.SATISFIABLE) break; // UNSAT or timeout: abandon these pins

                            int[,] grid = ReadGrid(solver.Model, c, w, h);
                            if (PathValidator.IsValid(grid, n))
                            {
                                accepted = true;
                                captured = grid;
                                break;
                            }

                            // Playable-but-looped or otherwise invalid model: forbid it and try again.
                            solver.Assert(BlockClause(ctx, c, grid, w, h));
                        }
                    }
                    finally
                    {
                        solver.Pop();
                    }

                    if (accepted)
                    {
                        produced++;
                        failed = 0;
                        onMap(captured);
                        if (!_config.ResetSolverPerMap)
                        {
                            solver.Assert(BlockClause(ctx, c, captured, w, h)); // enforce batch uniqueness
                        }
                        onStatus($"Accepted {produced}/{_config.MapCount} (solves: {attempts})");
                    }
                    else if (!shouldStop())
                    {
                        failed++;
                        onStatus($"Retrying... ({produced}/{_config.MapCount}, solves: {attempts})");
                        if (failed >= MaxFailedAttempts)
                        {
                            onStatus($"Stopped: no valid map found after {failed} attempts.");
                            break;
                        }
                    }
                }

                if (shouldStop())
                {
                    onStatus($"Stopped by user ({produced}/{_config.MapCount}).");
                }
                else if (produced >= _config.MapCount)
                {
                    onStatus($"Done: {produced}/{_config.MapCount} maps.");
                }
            }
        }

        /// <summary>
        /// Adds a 1/0 term to <paramref name="terms"/> that is 1 when the in-bounds neighbor shares the cell's color.
        /// </summary>
        private static void AddNeighborTerm(Context ctx, IntExpr[,] c, List<ArithExpr> terms,
            int x, int y, int nx, int ny, IntExpr one, IntExpr zero, int w, int h)
        {
            if (nx < 0 || nx >= w || ny < 0 || ny >= h) return;
            terms.Add((ArithExpr)ctx.MkITE(ctx.MkEq(c[nx, ny], c[x, y]), one, zero));
        }

        /// <summary>
        /// Asserts the fixed puzzle rules: full fill, degree 1 or 2 per cell, exactly two dots and a bounded
        /// length per color, and no solid 2x2 block of one color.
        /// </summary>
        private static void AssertBaseConstraints(Context ctx, Solver solver, IntExpr[,] c, ArithExpr[,] deg,
            int w, int h, int n, IntExpr one, IntExpr zero, int minLen, int maxLen)
        {
            IntExpr nColor = ctx.MkInt(n);

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    // Full fill: every cell has a color in 1..n.
                    solver.Assert(ctx.MkGe(c[x, y], one));
                    solver.Assert(ctx.MkLe(c[x, y], nColor));
                    // Endpoint (deg 1) or mid-path (deg 2); never isolated or branching.
                    solver.Assert(ctx.MkOr(ctx.MkEq(deg[x, y], ctx.MkInt(1)), ctx.MkEq(deg[x, y], ctx.MkInt(2))));
                }
            }

            for (int k = 1; k <= n; k++)
            {
                IntExpr color = ctx.MkInt(k);
                List<ArithExpr> endpointTerms = new List<ArithExpr>();
                List<ArithExpr> lengthTerms = new List<ArithExpr>();
                for (int x = 0; x < w; x++)
                {
                    for (int y = 0; y < h; y++)
                    {
                        BoolExpr isColor = ctx.MkEq(c[x, y], color);
                        lengthTerms.Add((ArithExpr)ctx.MkITE(isColor, one, zero));
                        BoolExpr isEndpoint = ctx.MkAnd(isColor, ctx.MkEq(deg[x, y], ctx.MkInt(1)));
                        endpointTerms.Add((ArithExpr)ctx.MkITE(isEndpoint, one, zero));
                    }
                }
                // Exactly two dots per color.
                solver.Assert(ctx.MkEq(ctx.MkAdd(endpointTerms.ToArray()), ctx.MkInt(2)));
                ArithExpr length = ctx.MkAdd(lengthTerms.ToArray());
                solver.Assert(ctx.MkGe(length, ctx.MkInt(minLen)));
                solver.Assert(ctx.MkLe(length, ctx.MkInt(maxLen)));
            }

            // Forbid any 2x2 block of a single color (kills the smallest degenerate loop).
            for (int x = 0; x < w - 1; x++)
            {
                for (int y = 0; y < h - 1; y++)
                {
                    solver.Assert(ctx.MkNot(ctx.MkAnd(
                        ctx.MkEq(c[x, y], c[x + 1, y]),
                        ctx.MkEq(c[x, y], c[x, y + 1]),
                        ctx.MkEq(c[x, y], c[x + 1, y + 1]))));
                }
            }
        }

        /// <summary>
        /// Pins one or two random cells to random colors so otherwise-deterministic solves yield varied maps.
        /// </summary>
        private static void AddRandomPins(Context ctx, Solver solver, IntExpr[,] c, System.Random rng, int w, int h, int n)
        {
            int pinCount = 1 + rng.Next(2); // 1 or 2 pins for variety between deterministic solves
            for (int i = 0; i < pinCount; i++)
            {
                int px = rng.Next(w);
                int py = rng.Next(h);
                int pc = 1 + rng.Next(n);
                solver.Assert(ctx.MkEq(c[px, py], ctx.MkInt(pc)));
            }
        }

        /// <summary>
        /// Reads the solved color assignment out of the model into a plain int grid.
        /// </summary>
        private static int[,] ReadGrid(Model model, IntExpr[,] c, int w, int h)
        {
            int[,] grid = new int[w, h];
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    grid[x, y] = ((IntNum)model.Evaluate(c[x, y], true)).Int;
                }
            }
            return grid;
        }

        /// <summary>
        /// Builds a clause that forbids this exact grid assignment, used to reject invalid or duplicate solutions.
        /// </summary>
        private static BoolExpr BlockClause(Context ctx, IntExpr[,] c, int[,] grid, int w, int h)
        {
            List<BoolExpr> diffs = new List<BoolExpr>();
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    diffs.Add(ctx.MkNot(ctx.MkEq(c[x, y], ctx.MkInt(grid[x, y]))));
                }
            }
            return ctx.MkOr(diffs.ToArray());
        }
    }
}
