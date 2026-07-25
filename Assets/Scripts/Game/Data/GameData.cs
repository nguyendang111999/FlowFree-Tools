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

    public static bool IsWin()
    {
        // TODO:
        // Player is win if all dots are connected
        return false;
    }
}