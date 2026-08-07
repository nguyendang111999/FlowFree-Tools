using System.Collections.Generic;
using UnityEngine;

namespace FlowFree.GenMap
{
    // Confirms the SAT model is actually playable: every color is exactly one simple path.
    // Local Z3 constraints allow a valid path plus a separate closed loop, so this is the safety net.
    public static class PathValidator
    {
        private static readonly Vector2Int[] Dirs =
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };

        /// <summary>
        /// Returns true only when the grid is fully filled and every color forms exactly one simple path.
        /// </summary>
        public static bool IsValid(int[,] grid, int colorCount)
        {
            int cols = grid.GetLength(0);
            int rows = grid.GetLength(1);

            Dictionary<int, List<Vector2Int>> byColor = new Dictionary<int, List<Vector2Int>>();
            for (int x = 0; x < cols; x++)
            {
                for (int y = 0; y < rows; y++)
                {
                    int c = grid[x, y];
                    if (c <= 0) return false; // full-fill required, no empty cells
                    if (!byColor.TryGetValue(c, out List<Vector2Int> list))
                    {
                        list = new List<Vector2Int>();
                        byColor[c] = list;
                    }
                    list.Add(new Vector2Int(x, y));
                }
            }

            if (byColor.Count != colorCount) return false;

            foreach (KeyValuePair<int, List<Vector2Int>> pair in byColor)
            {
                if (!IsSimplePath(grid, pair.Key, pair.Value, cols, rows)) return false;
            }
            return true;
        }

        /// <summary>
        /// Confirms one color's cells form a single non-branching path with exactly two endpoints and no separate loop.
        /// </summary>
        private static bool IsSimplePath(int[,] grid, int color, List<Vector2Int> cells, int cols, int rows)
        {
            List<Vector2Int> endpoints = new List<Vector2Int>();
            foreach (Vector2Int cell in cells)
            {
                int deg = Degree(grid, color, cell, cols, rows);
                if (deg == 0 || deg > 2) return false;
                if (deg == 1) endpoints.Add(cell);
            }
            if (endpoints.Count != 2) return false;

            // Walk the path from one endpoint; a valid path visits every cell and ends at the other endpoint.
            HashSet<Vector2Int> visited = new HashSet<Vector2Int> { endpoints[0] };
            Vector2Int current = endpoints[0];
            Vector2Int prev = new Vector2Int(-1, -1);
            while (true)
            {
                bool moved = false;
                foreach (Vector2Int d in Dirs)
                {
                    Vector2Int nb = current + d;
                    if (nb.x < 0 || nb.x >= cols || nb.y < 0 || nb.y >= rows) continue;
                    if (grid[nb.x, nb.y] != color) continue;
                    if (nb == prev || visited.Contains(nb)) continue;
                    prev = current;
                    current = nb;
                    visited.Add(current);
                    moved = true;
                    break;
                }
                if (!moved) break;
            }

            return visited.Count == cells.Count && current == endpoints[1];
        }

        /// <summary>
        /// Counts how many of the cell's four orthogonal neighbors share the given color.
        /// </summary>
        private static int Degree(int[,] grid, int color, Vector2Int cell, int cols, int rows)
        {
            int count = 0;
            foreach (Vector2Int d in Dirs)
            {
                Vector2Int nb = cell + d;
                if (nb.x < 0 || nb.x >= cols || nb.y < 0 || nb.y >= rows) continue;
                if (grid[nb.x, nb.y] == color) count++;
            }
            return count;
        }
    }
}
