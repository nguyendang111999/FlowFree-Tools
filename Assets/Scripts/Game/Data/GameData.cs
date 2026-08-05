using System;
using System.Collections.Generic;
using UnityEngine;

public static class GameData
{
    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    private static int _currentLevel;
    public static int CurrentLevel
    {
        get => _currentLevel;
        set => _currentLevel = value;
    }

    // Store cells data filled by the player, 0: empty, 1-n: filled.
    // Dots cells are pre-filled here and can't be replaced/overwritten by the player.
    private static int[,] _playerFillData;
    public static int[,] PlayerFillData => _playerFillData;

    // Store dots coordinates loaded from map, 0: empty, 1-n: filled. Use this to first display the map.
    private static int[,] _dotsData;
    public static int[,] DotsData => _dotsData;

    // Store each cell's color, loaded from the map file. Use to check win condition.
    private static int[,] _result;
    public static int[,] Result => _result;

    // Store each color's full solution: every cell belonging to that color, loaded from the map file.
    private static Dictionary<int, List<Vector2Int>> _solutions = new Dictionary<int, List<Vector2Int>>();
    public static Dictionary<int, List<Vector2Int>> Solutions => _solutions;

    // Store each color's current progress: the cells connected so far by the player, in order.
    private static Dictionary<int, List<Vector2Int>> _paths = new Dictionary<int, List<Vector2Int>>();
    public static Dictionary<int, List<Vector2Int>> Paths => _paths;

    public static int Cols => _result?.GetLength(0) ?? 0;
    public static int Rows => _result?.GetLength(1) ?? 0;

    public static bool InitializedMap(int level)
    {
        ClearMap();
        CurrentLevel = level;

        // Load data from Resources/Maps
        TextAsset mapAsset = Resources.Load<TextAsset>($"Maps/{level}");
        if (mapAsset == null)
        {
            return false;
        }

        string[] lines = mapAsset.text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        int rows = lines.Length;
        int cols = lines[0].Split(',').Length;

        _result = new int[cols, rows];
        _dotsData = new int[cols, rows];
        _playerFillData = new int[cols, rows];

        // CSV first line is the top row, but coordinate (0,0) is the bottom-left cell.
        for (int row = 0; row < rows; row++)
        {
            string[] values = lines[row].Split(',');
            int y = rows - 1 - row;
            for (int x = 0; x < cols; x++)
            {
                _result[x, y] = int.Parse(values[x].Trim());
            }
        }

        // Fetch the solutions from loaded data: group cells by color, each group is one color's solution.
        // A cell is a dot/tip of a path if it has at most 1 orthogonal neighbor sharing its color.
        for (int x = 0; x < cols; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                int color = _result[x, y];
                if (color <= 0) continue;

                if (!_solutions.TryGetValue(color, out List<Vector2Int> cells))
                {
                    cells = new List<Vector2Int>();
                    _solutions[color] = cells;
                }
                cells.Add(new Vector2Int(x, y));
            }
        }

        foreach (KeyValuePair<int, List<Vector2Int>> pair in _solutions)
        {
            int color = pair.Key;
            List<Vector2Int> cells = pair.Value;

            List<Vector2Int> tips = new List<Vector2Int>();
            foreach (Vector2Int cell in cells)
            {
                if (CountSameColorNeighbors(cell, color, cols, rows) <= 1)
                {
                    tips.Add(cell);
                }
            }
            if (tips.Count != 2)
            {
                tips = new List<Vector2Int> { cells[0], cells[cells.Count - 1] };
            }

            foreach (Vector2Int tip in tips)
            {
                _dotsData[tip.x, tip.y] = color;
                _playerFillData[tip.x, tip.y] = color;
            }

            // No progress made by the player yet for this color.
            _paths[color] = new List<Vector2Int>();
        }

        return _result != null;
    }

    private static int CountSameColorNeighbors(Vector2Int cell, int color, int cols, int rows)
    {
        int count = 0;
        foreach (Vector2Int dir in Directions)
        {
            Vector2Int neighbor = cell + dir;
            if (neighbor.x < 0 || neighbor.x >= cols || neighbor.y < 0 || neighbor.y >= rows) continue;
            if (_result[neighbor.x, neighbor.y] == color) count++;
        }
        return count;
    }

    public static void ClearMap()
    {
        _playerFillData = null;
        _dotsData = null;
        _result = null;
        _solutions.Clear();
        _paths.Clear();
    }

    // Begin (or grab) a path at the given cell. Returns the color being drawn, or 0 if the cell is not a valid start.
    public static int StartPathAt(Vector2Int cell)
    {
        if (_playerFillData == null || !InBounds(cell)) return 0;

        int dotColor = _dotsData[cell.x, cell.y];
        if (dotColor != 0)
        {
            ResetPath(dotColor);
            _paths[dotColor].Add(cell);
            return dotColor;
        }

        int fill = _playerFillData[cell.x, cell.y];
        if (fill != 0 && _paths.TryGetValue(fill, out List<Vector2Int> path))
        {
            int idx = path.IndexOf(cell);
            if (idx >= 0)
            {
                TrimPathAfter(fill, idx);
                return fill;
            }
        }
        return 0;
    }

    // Try to grow/shrink the active color's path into an adjacent cell. Returns true if the path changed.
    public static bool TryExtend(int color, Vector2Int cell)
    {
        if (_playerFillData == null || !InBounds(cell)) return false;
        if (!_paths.TryGetValue(color, out List<Vector2Int> path) || path.Count == 0) return false;

        Vector2Int head = path[path.Count - 1];
        if (cell == head || !IsAdjacent(head, cell)) return false;

        int idx = path.IndexOf(cell);
        if (idx >= 0)
        {
            TrimPathAfter(color, idx);
            return true;
        }

        // Once the head sits on the terminal dot the path is finished and can only be shortened.
        if (path.Count > 1 && _dotsData[head.x, head.y] != 0) return false;

        int dot = _dotsData[cell.x, cell.y];
        if (dot != 0)
        {
            if (dot != color) return false;
            AppendCell(color, cell);
            return true;
        }

        int occupant = _playerFillData[cell.x, cell.y];
        if (occupant != 0 && occupant != color && _paths.TryGetValue(occupant, out List<Vector2Int> other))
        {
            int oi = other.IndexOf(cell);
            if (oi >= 1) TrimPathAfter(occupant, oi - 1);
        }

        AppendCell(color, cell);
        return true;
    }

    public static Vector2Int PathHead(int color)
    {
        if (_paths.TryGetValue(color, out List<Vector2Int> path) && path.Count > 0)
        {
            return path[path.Count - 1];
        }
        return new Vector2Int(-1, -1);
    }

    private static void AppendCell(int color, Vector2Int cell)
    {
        _playerFillData[cell.x, cell.y] = color;
        _paths[color].Add(cell);
    }

    private static void ResetPath(int color)
    {
        if (_paths.TryGetValue(color, out List<Vector2Int> path))
        {
            foreach (Vector2Int cell in path)
            {
                if (_dotsData[cell.x, cell.y] == 0) _playerFillData[cell.x, cell.y] = 0;
            }
            path.Clear();
        }
        else
        {
            _paths[color] = new List<Vector2Int>();
        }
    }

    // Remove every cell after index keepIdx, clearing fill for non-dot cells.
    private static void TrimPathAfter(int color, int keepIdx)
    {
        List<Vector2Int> path = _paths[color];
        for (int i = path.Count - 1; i > keepIdx; i--)
        {
            Vector2Int cell = path[i];
            if (_dotsData[cell.x, cell.y] == 0) _playerFillData[cell.x, cell.y] = 0;
            path.RemoveAt(i);
        }
    }

    private static bool InBounds(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < Cols && cell.y >= 0 && cell.y < Rows;
    }

    private static bool IsAdjacent(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1;
    }

    public static bool IsWin()
    {
        if (_playerFillData == null) return false;

        for (int x = 0; x < Cols; x++)
        {
            for (int y = 0; y < Rows; y++)
            {
                if (_playerFillData[x, y] == 0) return false;
            }
        }

        foreach (int color in _solutions.Keys)
        {
            if (!_paths.TryGetValue(color, out List<Vector2Int> path) || path.Count < 2) return false;

            Vector2Int a = path[0];
            Vector2Int b = path[path.Count - 1];
            if (a == b) return false;
            if (_dotsData[a.x, a.y] != color || _dotsData[b.x, b.y] != color) return false;
        }
        return true;
    }
}