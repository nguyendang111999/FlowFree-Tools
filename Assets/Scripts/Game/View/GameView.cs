using System.Collections.Generic;
using UnityEngine;

public class GameView : MonoBehaviour
{
    [SerializeField] private GameObject _cellPrefab;
    [SerializeField] private Camera _camera;
    [SerializeField] private float _cellSize = 1f;
    [SerializeField] private float _cameraPadding = 1f;
    [SerializeField] private int _startLevel = 1;
    [SerializeField] private float _autoAdvanceDelay = 0.75f;

    // Colors indexed by (colorId - 1). Extra ids fall back to a generated hue.
    [SerializeField]
    private Color[] _palette =
    {
        Color.red, Color.blue, Color.green, Color.yellow, Color.magenta,
        Color.cyan, new Color(1f, 0.5f, 0f), new Color(0.6f, 0.3f, 0.1f)
    };

    private Cell[,] _cells;
    private int _cols;
    private int _rows;
    private Vector2 _origin;

    private bool _dragging;
    private int _dragColor;
    private Vector2Int _lastCell;

    private void Start()
    {
        if (_camera == null) _camera = Camera.main;
        DisplayMap(_startLevel);
    }

    public bool DisplayMap(int level)
    {
        if (!GameData.InitializedMap(level))
        {
            Debug.LogWarning($"[GameView] Failed to load level {level}. Check Resources/Maps/{level}.csv.");
            return false;
        }

        ClearGrid();
        _cols = GameData.Cols;
        _rows = GameData.Rows;
        _cells = new Cell[_cols, _rows];

        _origin = new Vector2(
            transform.position.x - (_cols - 1) * _cellSize * 0.5f,
            transform.position.y - (_rows - 1) * _cellSize * 0.5f);

        for (int x = 0; x < _cols; x++)
        {
            for (int y = 0; y < _rows; y++)
            {
                Vector3 pos = new Vector3(_origin.x + x * _cellSize, _origin.y + y * _cellSize, 0f);
                GameObject go = Instantiate(_cellPrefab, pos, Quaternion.identity, transform);
                _cells[x, y] = go.GetComponent<Cell>();
            }
        }

        FitCamera();
        RefreshView();
        return true;
    }

    private void ClearGrid()
    {
        if (_cells == null) return;
        foreach (Cell cell in _cells)
        {
            if (cell != null) Destroy(cell.gameObject);
        }
        _cells = null;
    }

    private void FitCamera()
    {
        if (_camera == null || !_camera.orthographic) return;

        float halfHeight = _rows * _cellSize * 0.5f;
        float halfWidth = _cols * _cellSize * 0.5f / _camera.aspect;
        _camera.orthographicSize = Mathf.Max(halfHeight, halfWidth) + _cameraPadding;
        _camera.transform.position = new Vector3(transform.position.x, transform.position.y, _camera.transform.position.z);
    }

    private void RefreshView()
    {
        if (_cells == null) return;

        int[,] masks = BuildDirMasks();

        for (int x = 0; x < _cols; x++)
        {
            for (int y = 0; y < _rows; y++)
            {
                Cell cell = _cells[x, y];
                if (cell == null) continue;

                int colorId = GameData.PlayerFillData[x, y];
                if (colorId == 0)
                {
                    cell.Clear();
                    continue;
                }

                bool isDot = GameData.DotsData[x, y] != 0;
                cell.SetColor(GetColor(colorId));
                cell.DisplayShape(masks[x, y], isDot);
            }
        }
    }

    // Compute per-cell connection bits from each color's ordered path.
    private int[,] BuildDirMasks()
    {
        int[,] masks = new int[_cols, _rows];
        foreach (KeyValuePair<int, List<Vector2Int>> pair in GameData.Paths)
        {
            List<Vector2Int> path = pair.Value;
            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector2Int a = path[i];
                Vector2Int b = path[i + 1];
                Vector2Int d = b - a;

                if (d == Vector2Int.up) { masks[a.x, a.y] |= Cell.Up; masks[b.x, b.y] |= Cell.Down; }
                else if (d == Vector2Int.down) { masks[a.x, a.y] |= Cell.Down; masks[b.x, b.y] |= Cell.Up; }
                else if (d == Vector2Int.right) { masks[a.x, a.y] |= Cell.Right; masks[b.x, b.y] |= Cell.Left; }
                else if (d == Vector2Int.left) { masks[a.x, a.y] |= Cell.Left; masks[b.x, b.y] |= Cell.Right; }
            }
        }
        return masks;
    }

    private Color GetColor(int colorId)
    {
        int index = colorId - 1;
        if (_palette != null && index >= 0 && index < _palette.Length) return _palette[index];
        return Color.HSVToRGB((colorId * 0.17f) % 1f, 0.85f, 0.95f);
    }

    private void Update()
    {
        if (_cells == null || _camera == null) return;

        if (Input.GetMouseButtonDown(0))
        {
            if (TryGetCell(out Vector2Int cell))
            {
                int color = GameData.StartPathAt(cell);
                if (color != 0)
                {
                    _dragging = true;
                    _dragColor = color;
                    _lastCell = GameData.PathHead(color);
                    RefreshView();
                }
            }
        }
        else if (_dragging && Input.GetMouseButton(0))
        {
            if (TryGetCell(out Vector2Int cell) && cell != _lastCell)
            {
                if (GameData.TryExtend(_dragColor, cell))
                {
                    _lastCell = GameData.PathHead(_dragColor);
                    RefreshView();
                }
            }
        }
        else if (Input.GetMouseButtonUp(0) && _dragging)
        {
            _dragging = false;
            _dragColor = 0;
            if (GameData.IsWin()) OnWin();
        }
    }

    private bool TryGetCell(out Vector2Int cell)
    {
        Vector3 world = _camera.ScreenToWorldPoint(Input.mousePosition);
        int x = Mathf.RoundToInt((world.x - _origin.x) / _cellSize);
        int y = Mathf.RoundToInt((world.y - _origin.y) / _cellSize);
        cell = new Vector2Int(x, y);
        return x >= 0 && x < _cols && y >= 0 && y < _rows;
    }

    private void OnWin()
    {
        Debug.Log($"[GameView] Level {GameData.CurrentLevel} solved!");
        Invoke(nameof(AdvanceLevel), _autoAdvanceDelay);
    }

    private void AdvanceLevel()
    {
        DisplayMap(GameData.CurrentLevel + 1);
    }
}